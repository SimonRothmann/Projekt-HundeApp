using Dogity.Application.Community;
using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Einzelne Übung aus einem eingetragenen Training löschen (Paket K3): Rechte
/// wie beim Ändern der Übung, die Einheit bleibt, Wiedervorlage-Zustand und
/// Zähler der Trainer:in stimmen danach.
/// </summary>
public class UebungLoeschenTests
{
    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.Today);

    private sealed record Aufbau(
        ApplicationDbContext Db,
        TrainingService Trainings,
        Guid Besitzerin,
        Guid Trainerin,
        Guid DogId,
        Guid SitzId,
        Guid PlatzId);

    private static async Task<Aufbau> BaueAsync()
    {
        var db = InMemoryDbContext.Create();
        var trainings = new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService(), TimeProvider.System);

        var besitzerin = Guid.NewGuid();
        var trainerin = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        var sport = new Sport { Code = "TEST", Name = "Testsport" };
        var sitz = new Exercise { SportId = sport.Id, Name = "Sitz" };
        var platz = new Exercise { SportId = sport.Id, Name = "Platz" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = besitzerin, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = dog.Id, TrainerId = trainerin, MemberId = besitzerin, StartDate = Heute });
        db.Sports.Add(sport);
        db.Exercises.AddRange(sitz, platz);
        await db.SaveChangesAsync();

        return new Aufbau(db, trainings, besitzerin, trainerin, dog.Id, sitz.Id, platz.Id);
    }

    private static async Task<TrainingSessionDto> TrageEinAsync(Aufbau a, Guid von, params (Guid Uebung, int Bewertung, bool Erfolg)[] uebungen)
    {
        var ergebnis = await a.Trainings.CreateAsync(von, new CreateTrainingSessionRequest(
            a.DogId, Heute, 10, "Notiz",
            uebungen.Select(u => new CreateTrainingExerciseRequest(u.Uebung, u.Bewertung, ExerciseDifficulty.Beginner, u.Erfolg, null, null)).ToList()));
        Assert.True(ergebnis.Succeeded, string.Join(", ", ergebnis.Errors));
        return ergebnis.Value!;
    }

    [Fact]
    public async Task Besitzerin_LoeschtUebung_Weg_UndMasteryNeuGerechnet()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin, (a.SitzId, 5, true), (a.PlatzId, 3, true));
        var sitz = training.Exercises.Single(e => e.ExerciseName == "Sitz");

        var mastery = await a.Db.ExerciseMasteries.SingleAsync(m => m.ExerciseId == a.SitzId);
        Assert.Equal(1, mastery.SessionCount);

        var ergebnis = await a.Trainings.DeleteExerciseAsync(a.Besitzerin, sitz.Id);

        Assert.True(ergebnis.Succeeded);
        var rest = Assert.Single(ergebnis.Value!.Exercises);
        Assert.Equal("Platz", rest.ExerciseName);
        Assert.Empty(await a.Db.TrainingExercises.Where(e => e.Id == sitz.Id).ToListAsync());
        Assert.NotNull((await a.Db.TrainingExercises.IgnoreQueryFilters().SingleAsync(e => e.Id == sitz.Id)).DeletedAt);

        mastery = await a.Db.ExerciseMasteries.SingleAsync(m => m.ExerciseId == a.SitzId);
        Assert.Equal(0, mastery.SessionCount);
        Assert.Null(mastery.LastTrainedAt);
        // Die andere Übung bleibt unberührt.
        Assert.Equal(1, (await a.Db.ExerciseMasteries.SingleAsync(m => m.ExerciseId == a.PlatzId)).SessionCount);
    }

    [Fact]
    public async Task LetzteUebung_EinheitBleibtMitDauerUndNotiz()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin, (a.SitzId, 4, true));

        var ergebnis = await a.Trainings.DeleteExerciseAsync(a.Besitzerin, Assert.Single(training.Exercises).Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(ergebnis.Value!.Exercises);
        Assert.Equal(training.Id, ergebnis.Value.Id);
        var einheit = await a.Db.TrainingSessions.SingleAsync();
        Assert.Null(einheit.DeletedAt);
        Assert.Equal(10, einheit.DurationMinutes);
        Assert.Equal("Notiz", einheit.Notes);
    }

    [Fact]
    public async Task FremdePerson_UndTrainerinOhneBearbeitungsrecht_Bekommen404()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin, (a.SitzId, 4, true));
        var uebungId = Assert.Single(training.Exercises).Id;

        Assert.True((await a.Trainings.DeleteExerciseAsync(Guid.NewGuid(), uebungId)).IsNotFound);
        // Eintrag der Familie: die Trainer:in gibt Feedback, ändert aber nichts.
        Assert.True((await a.Trainings.DeleteExerciseAsync(a.Trainerin, uebungId)).IsNotFound);

        Assert.Single(await a.Db.TrainingExercises.ToListAsync());
    }

    [Fact]
    public async Task Trainerin_LoeschtUebungInIhremEigenenTraining()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Trainerin, (a.SitzId, 4, true), (a.PlatzId, 4, true));

        var ergebnis = await a.Trainings.DeleteExerciseAsync(a.Trainerin, training.Exercises[0].Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Single(ergebnis.Value!.Exercises);
    }

    [Fact]
    public async Task SchonGeloeschteUebung_Bekommt404()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin, (a.SitzId, 4, true), (a.PlatzId, 4, true));
        var uebungId = training.Exercises[0].Id;

        Assert.True((await a.Trainings.DeleteExerciseAsync(a.Besitzerin, uebungId)).Succeeded);
        Assert.True((await a.Trainings.DeleteExerciseAsync(a.Besitzerin, uebungId)).IsNotFound);
    }

    [Fact]
    public async Task TrainerBewertung_VerschwindetMitUebung_RueckmeldungsZaehlerStimmt()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin, (a.SitzId, 4, true), (a.PlatzId, 4, true));
        var sitz = training.Exercises.Single(e => e.ExerciseName == "Sitz");
        var platz = training.Exercises.Single(e => e.ExerciseName == "Platz");
        var zaehler = new TrainerOpenCountsService(a.Db);

        // Nur "Sitz" bewertet: das Training ist noch offen (Platz fehlt).
        Assert.True((await a.Trainings.SetExerciseTrainerRatingAsync(a.Trainerin, sitz.Id, 5, "fein")).Succeeded);
        Assert.Equal(1, (await zaehler.GetAsync(a.Trainerin)).Value!.SessionsToRate);

        // Die unbewertete Übung weg: alle übrigen sind bewertet, nichts mehr offen.
        Assert.True((await a.Trainings.DeleteExerciseAsync(a.Besitzerin, platz.Id)).Succeeded);
        Assert.Equal(0, (await zaehler.GetAsync(a.Trainerin)).Value!.SessionsToRate);
        Assert.Empty((await a.Trainings.GetSessionsToRateAsync(a.Trainerin)).Value!);

        // Die bewertete Übung weg: die Bewertung verschwindet mit ihr, die
        // Einheit ohne Übungen und ohne Feedback ist wieder offen.
        Assert.True((await a.Trainings.DeleteExerciseAsync(a.Besitzerin, sitz.Id)).Succeeded);
        var offen = Assert.Single((await a.Trainings.GetSessionsToRateAsync(a.Trainerin)).Value!);
        Assert.Empty(offen.Exercises);
        Assert.Equal(1, (await zaehler.GetAsync(a.Trainerin)).Value!.SessionsToRate);
    }
}
