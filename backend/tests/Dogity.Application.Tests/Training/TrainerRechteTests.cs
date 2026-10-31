using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Tracking;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Betreuende Trainer:innen ändern und löschen nur Trainings, die sie selbst
/// eingetragen haben; Einträge der Familie sehen sie und geben Feedback
/// (Entscheidung des Betreibers 2026-10-29, siehe TrainingsRechte). Vorher
/// durften sie jedes Training eines betreuten Hundes löschen und jede
/// Bewertung ändern - ohne Doku und ohne Test.
/// </summary>
public class TrainerRechteTests
{
    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.Today);

    private sealed record Aufbau(
        ApplicationDbContext Db,
        TrainingService Trainings,
        GpsTrackService Faehrten,
        Guid Besitzerin,
        Guid Mitbesitzer,
        Guid Trainerin,
        Guid DogId,
        Guid UebungId);

    private static async Task<Aufbau> BaueAsync()
    {
        var db = InMemoryDbContext.Create();
        var trainings = new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService(), TimeProvider.System);
        var faehrten = new GpsTrackService(db, new FakeWeatherEnrichmentService());

        var besitzerin = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        var trainerin = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        var sport = new Sport { Code = "TEST", Name = "Testsport" };
        var uebung = new Exercise { SportId = sport.Id, Name = "Sitz" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = besitzerin, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = mitbesitzer, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = dog.Id, TrainerId = trainerin, MemberId = besitzerin, StartDate = Heute });
        db.Sports.Add(sport);
        db.Exercises.Add(uebung);
        await db.SaveChangesAsync();

        return new Aufbau(db, trainings, faehrten, besitzerin, mitbesitzer, trainerin, dog.Id, uebung.Id);
    }

    private static async Task<TrainingSessionDto> TrageEinAsync(Aufbau a, Guid von, DateOnly? tag = null, int bewertung = 3)
    {
        var ergebnis = await a.Trainings.CreateAsync(von, new CreateTrainingSessionRequest(
            a.DogId, tag ?? Heute, 10, "Notiz",
            [new CreateTrainingExerciseRequest(a.UebungId, bewertung, ExerciseDifficulty.Beginner, true, null, null)]));
        Assert.True(ergebnis.Succeeded, string.Join(", ", ergebnis.Errors));
        return ergebnis.Value!;
    }

    [Fact]
    public async Task Trainerin_KannTrainingDerFamilie_WederAendernNochLoeschen()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin);
        var uebung = Assert.Single(training.Exercises);

        Assert.True((await a.Trainings.DeleteAsync(a.Trainerin, training.Id)).IsNotFound);
        Assert.True((await a.Trainings.UpdateExerciseAsync(a.Trainerin, uebung.Id, new UpdateTrainingExerciseRequest(1, false, "anders"))).IsNotFound);
        Assert.True((await a.Trainings.UpdateExerciseNotesAsync(a.Trainerin, uebung.Id, "anders")).IsNotFound);
        Assert.True((await a.Trainings.UpdateSessionNotesAsync(a.Trainerin, training.Id, "anders")).IsNotFound);
        Assert.True((await a.Trainings.SetSessionContextAsync(a.Trainerin, training.Id, new UpdateSessionContextRequest(null, null, null, "anders"))).IsNotFound);
        Assert.True((await a.Trainings.MoveTrainingDayAsync(a.Trainerin, training.Id, Heute.AddDays(-1))).IsNotFound);

        var gespeichert = await a.Db.TrainingSessions.Include(s => s.Exercises).SingleAsync();
        Assert.Equal(Heute, gespeichert.Date);
        Assert.Equal("Notiz", gespeichert.Notes);
        Assert.Null(gespeichert.LocationName);
        var e = Assert.Single(gespeichert.Exercises);
        Assert.Equal(3, e.Rating);
        Assert.True(e.Success);
        Assert.Null(e.Notes);
    }

    [Fact]
    public async Task Trainerin_GibtZuEintraegenDerFamilie_WeiterFeedback()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin);

        Assert.True((await a.Trainings.SetFeedbackAsync(a.Trainerin, training.Id, new SetFeedbackRequest("Gut gemacht"))).Succeeded);
        Assert.True((await a.Trainings.SetExerciseTrainerRatingAsync(a.Trainerin, Assert.Single(training.Exercises).Id, 4, null)).Succeeded);
    }

    [Fact]
    public async Task Trainerin_AendertUndLoeschtIhrEigenesTraining()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Trainerin);

        Assert.True((await a.Trainings.UpdateExerciseAsync(a.Trainerin, Assert.Single(training.Exercises).Id, new UpdateTrainingExerciseRequest(5, true, null))).Succeeded);
        Assert.True((await a.Trainings.UpdateSessionNotesAsync(a.Trainerin, training.Id, "korrigiert")).Succeeded);
        Assert.True((await a.Trainings.DeleteAsync(a.Trainerin, training.Id)).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Familie_AendertUndLoeschtAuchEintraegeDerTrainerin(bool mitbesitzer)
    {
        var a = await BaueAsync();
        var familie = mitbesitzer ? a.Mitbesitzer : a.Besitzerin;
        var training = await TrageEinAsync(a, a.Trainerin);

        Assert.True((await a.Trainings.UpdateExerciseAsync(familie, Assert.Single(training.Exercises).Id, new UpdateTrainingExerciseRequest(2, false, null))).Succeeded);
        Assert.True((await a.Trainings.DeleteAsync(familie, training.Id)).Succeeded);
    }

    [Fact]
    public async Task EintragDerTrainerin_LandetNichtInDerTageseinheitDerFamilie()
    {
        var a = await BaueAsync();
        var familie = await TrageEinAsync(a, a.Besitzerin);

        var trainerin = await TrageEinAsync(a, a.Trainerin);
        // Mitbesitzer:innen teilen sich die Einheit des Tages mit der Besitzerin.
        var mitbesitzer = await TrageEinAsync(a, a.Mitbesitzer);
        // Und umgekehrt: Die Familie hängt nichts an die Einheit der Trainerin.
        var zweiteTrainerin = await TrageEinAsync(a, a.Trainerin);

        Assert.NotEqual(familie.Id, trainerin.Id);
        Assert.Equal(familie.Id, mitbesitzer.Id);
        Assert.Equal(trainerin.Id, zweiteTrainerin.Id);
        Assert.Equal(2, await a.Db.TrainingSessions.CountAsync());
    }

    [Fact]
    public async Task TagVerschieben_DurchTrainerin_NimmtDieEinheitDerFamilieNichtMit()
    {
        var a = await BaueAsync();
        var familie = await TrageEinAsync(a, a.Besitzerin);
        var eigene = await TrageEinAsync(a, a.Trainerin);

        Assert.True((await a.Trainings.MoveTrainingDayAsync(a.Trainerin, eigene.Id, Heute.AddDays(-1))).Succeeded);

        Assert.Equal(Heute, (await a.Db.TrainingSessions.SingleAsync(s => s.Id == familie.Id)).Date);
        Assert.Equal(Heute.AddDays(-1), (await a.Db.TrainingSessions.SingleAsync(s => s.Id == eigene.Id)).Date);
    }

    [Fact]
    public async Task Tagebuch_SagtJeTraining_ObDieAnsehendePersonBearbeitenDarf()
    {
        var a = await BaueAsync();
        var familie = await TrageEinAsync(a, a.Besitzerin);
        var eigene = await TrageEinAsync(a, a.Trainerin);

        var sichtTrainerin = (await a.Trainings.GetByDogAsync(a.Trainerin, a.DogId)).Value!;
        var sichtFamilie = (await a.Trainings.GetByDogAsync(a.Mitbesitzer, a.DogId)).Value!;

        Assert.False(sichtTrainerin.Single(s => s.Id == familie.Id).DarfBearbeiten);
        Assert.True(sichtTrainerin.Single(s => s.Id == eigene.Id).DarfBearbeiten);
        Assert.All(sichtFamilie, s => Assert.True(s.DarfBearbeiten));
        Assert.False((await a.Trainings.GetByIdAsync(a.Trainerin, familie.Id)).Value!.DarfBearbeiten);
    }

    [Fact]
    public async Task Faehrte_DerFamilie_KannDieTrainerinNichtLoeschen()
    {
        var a = await BaueAsync();
        var training = await TrageEinAsync(a, a.Besitzerin);
        var faehrte = new GpsTrack { TrainingSessionId = training.Id };
        a.Db.GpsTracks.Add(faehrte);
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Faehrten.DeleteAsync(a.Trainerin, faehrte.Id)).IsNotFound);
        Assert.True((await a.Faehrten.DeleteAsync(a.Mitbesitzer, faehrte.Id)).Succeeded);
    }

    [Fact]
    public async Task Faehrte_DerTrainerin_LandetInIhrerEigenenTageseinheit()
    {
        var a = await BaueAsync();
        var familie = await TrageEinAsync(a, a.Besitzerin);

        CreateGpsPointRequest[] punkte = [new(48.9, 8.4, DateTimeOffset.UtcNow, 5)];

        var ergebnis = await a.Faehrten.CreateAsync(a.Trainerin, new CreateGpsTrackRequest(
            null, null, null, null, null, null, null, punkte, DogId: a.DogId, Date: Heute));

        Assert.True(ergebnis.Succeeded, string.Join(", ", ergebnis.Errors));
        Assert.NotEqual(familie.Id, ergebnis.Value!.TrainingSessionId);
        Assert.True((await a.Faehrten.CreateAsync(a.Trainerin, new CreateGpsTrackRequest(
            familie.Id, null, null, null, null, null, null, punkte))).IsNotFound);
    }

    [Fact]
    public async Task PlanProtokoll_SagtJeEintrag_ObDieAnsehendePersonBearbeitenDarf()
    {
        var a = await BaueAsync();
        var sportId = (await a.Db.Sports.SingleAsync()).Id;
        var ziel = new Goal { DogId = a.DogId, SportId = sportId, TargetDate = Heute.AddMonths(3) };
        var plan = new TrainingPlan { GoalId = ziel.Id };
        var planZiel = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, ExerciseId = a.UebungId, RepetitionsTarget = 3 };
        a.Db.Goals.Add(ziel);
        a.Db.TrainingPlans.Add(plan);
        a.Db.TrainingPlanItems.Add(planZiel);
        await a.Db.SaveChangesAsync();

        async Task<Guid> TrageZumPlanEinAsync(Guid von)
        {
            var ergebnis = await a.Trainings.CreateAsync(von, new CreateTrainingSessionRequest(
                a.DogId, Heute, 10, null,
                [new CreateTrainingExerciseRequest(a.UebungId, 3, ExerciseDifficulty.Beginner, true, null, planZiel.Id)]));
            return Assert.Single(ergebnis.Value!.Exercises).Id;
        }
        var vonFamilie = await TrageZumPlanEinAsync(a.Besitzerin);
        var vonTrainerin = await TrageZumPlanEinAsync(a.Trainerin);

        var ziele = new GoalService(a.Db, TimeProvider.System, new FakeNotificationService(), new ExerciseMasteryService(a.Db));
        IReadOnlyList<TrainingPlanItemLogDto> Protokoll(IReadOnlyList<GoalDto> g) =>
            Assert.Single(Assert.Single(g).TrainingPlan!.Items).Logs;

        var sichtTrainerin = Protokoll((await ziele.GetByDogAsync(a.Trainerin, a.DogId)).Value!);
        var sichtFamilie = Protokoll((await ziele.GetByDogAsync(a.Mitbesitzer, a.DogId)).Value!);

        Assert.False(sichtTrainerin.Single(l => l.TrainingExerciseId == vonFamilie).DarfBearbeiten);
        Assert.True(sichtTrainerin.Single(l => l.TrainingExerciseId == vonTrainerin).DarfBearbeiten);
        Assert.Equal(2, sichtFamilie.Count);
        Assert.All(sichtFamilie, l => Assert.True(l.DarfBearbeiten));
    }
}
