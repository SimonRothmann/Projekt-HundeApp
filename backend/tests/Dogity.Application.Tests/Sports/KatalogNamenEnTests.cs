using Dogity.Application.Planning;
using Dogity.Application.Sports;
using Dogity.Application.Stats;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Der englische Katalogname (NameEn) von der Admin-Eingabe bis in die
/// Antworten, die Namen aus dem Katalog liefern: Katalog, Tagebuch, Plan,
/// Statistik. Freitext-Einträge und Übungen ohne englischen Namen liefern null.
/// </summary>
public class KatalogNamenEnTests
{
    private static readonly string Zu101 = new('x', 101);
    private static readonly string Zu151 = new('x', 151);

    // ---- Admin-Editor ----

    [Fact]
    public async Task UpdateSport_SetztUndLoeschtEnglischenNamen()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "IGP1", Name = "IGP 1" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();
        var dienst = new RegulationManagementService(db);

        var gesetzt = await dienst.UpdateSportAsync(Guid.NewGuid(), true, sport.Id, new UpdateSportRequest("IGP 1", null, "  Utility Dog 1 "));
        Assert.Equal("Utility Dog 1", gesetzt.Value!.NameEn);

        var geleert = await dienst.UpdateSportAsync(Guid.NewGuid(), true, sport.Id, new UpdateSportRequest("IGP 1", null, "   "));
        Assert.Null(geleert.Value!.NameEn);
        Assert.Null((await db.Sports.SingleAsync()).NameEn);
    }

    [Fact]
    public async Task UpdateSport_ZuLangerEnglischerName_WirdAbgelehntUndNichtGespeichert()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "IGP1", Name = "IGP 1", NameEn = "Alt" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();

        var ergebnis = await new RegulationManagementService(db).UpdateSportAsync(Guid.NewGuid(), true, sport.Id, new UpdateSportRequest("IGP 1", null, Zu101));

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Alt", (await db.Sports.SingleAsync()).NameEn);
    }

    [Fact]
    public async Task UpdateSport_GlobalAlsNichtAdmin_LaesstEnglischenNamenUnveraendert()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "IGP1", Name = "IGP 1", NameEn = "Alt" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();

        var ergebnis = await new RegulationManagementService(db).UpdateSportAsync(Guid.NewGuid(), false, sport.Id, new UpdateSportRequest("IGP 1", null, "Neu"));

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Alt", (await db.Sports.SingleAsync()).NameEn);
    }

    [Fact]
    public async Task UpdateRegulation_SetztEnglischenNamen_ZuLangUndNichtAdminScheitern()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "UPR", Name = "UPr" };
        db.Sports.Add(sport);
        var ordnung = new Regulation { SportId = sport.Id, Name = "FCI-UPr 1" };
        db.Regulations.Add(ordnung);
        await db.SaveChangesAsync();
        var dienst = new RegulationManagementService(db);

        var gesetzt = await dienst.UpdateRegulationAsync(Guid.NewGuid(), true, ordnung.Id, new UpdateRegulationRequest("FCI-UPr 1", null, null, null, "FCI-UPr 1 – Obedience"));
        Assert.Equal("FCI-UPr 1 – Obedience", gesetzt.Value!.NameEn);

        var zuLang = await dienst.UpdateRegulationAsync(Guid.NewGuid(), true, ordnung.Id, new UpdateRegulationRequest("FCI-UPr 1", null, null, null, Zu101));
        Assert.False(zuLang.Succeeded);

        var fremd = await dienst.UpdateRegulationAsync(Guid.NewGuid(), false, ordnung.Id, new UpdateRegulationRequest("FCI-UPr 1", null, null, null, "Hijack"));
        Assert.False(fremd.Succeeded);
        Assert.Equal("FCI-UPr 1 – Obedience", (await db.Regulations.SingleAsync()).NameEn);
    }

    [Fact]
    public async Task Uebung_AnlegenUndAendern_MitEnglischemNamen_UndGrenze()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "UPR", Name = "UPr" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();
        var dienst = new ExerciseManagementService(db);
        var admin = Guid.NewGuid();

        var angelegt = await dienst.CreateAsync(admin, true,
            new CreateExerciseRequest(sport.Id, "Freifolge", null, ExerciseDifficulty.Beginner, null, null, null, "Free heeling"));
        Assert.Equal("Free heeling", angelegt.Value!.NameEn);

        var geaendert = await dienst.UpdateAsync(admin, true, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, "Heeling off leash"));
        Assert.Equal("Heeling off leash", geaendert.Value!.NameEn);

        // 150 Zeichen sind erlaubt, 151 nicht - dieselbe Grenze wie beim deutschen Namen.
        var genau = await dienst.UpdateAsync(admin, true, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, new string('x', 150)));
        Assert.True(genau.Succeeded);
        var zuLang = await dienst.UpdateAsync(admin, true, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, Zu151));
        Assert.False(zuLang.Succeeded);
        var zuLangNeu = await dienst.CreateAsync(admin, true,
            new CreateExerciseRequest(sport.Id, "Neu", null, ExerciseDifficulty.Beginner, null, null, null, Zu151));
        Assert.False(zuLangNeu.Succeeded);

        var ohneRecht = await dienst.UpdateAsync(Guid.NewGuid(), false, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, "Hijack"));
        Assert.False(ohneRecht.Succeeded);
        Assert.Equal(new string('x', 150), (await db.Exercises.SingleAsync()).NameEn);
    }

    // ---- Katalog-Antworten ----

    [Fact]
    public async Task Katalog_LiefertEnglischeNamenVonSportartOrdnungUndUebung()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "UPR", Name = "UPr", NameEn = "Obedience" };
        db.Sports.Add(sport);
        var mit = new Exercise { SportId = sport.Id, Name = "Freifolge", NameEn = "Free heeling" };
        var ohne = new Exercise { SportId = sport.Id, Name = "Eigene Übung" };
        db.Exercises.AddRange(mit, ohne);
        var ordnung = new Regulation { SportId = sport.Id, Name = "FCI-UPr 1", NameEn = "FCI-UPr 1 – Obedience" };
        db.Regulations.Add(ordnung);
        var version = new RegulationVersion { RegulationId = ordnung.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        db.RegulationVersions.Add(version);
        db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = version.Id, ExerciseId = mit.Id, MaxPoints = 15, SortOrder = 0 });
        await db.SaveChangesAsync();
        var dienst = new SportCatalogService(db);

        Assert.Equal("Obedience", (await dienst.GetSportsAsync()).Value!.Single().NameEn);
        var uebungen = (await dienst.GetExercisesAsync(sport.Id, Guid.NewGuid())).Value!;
        Assert.Equal("Free heeling", uebungen.Single(u => u.Name == "Freifolge").NameEn);
        Assert.Null(uebungen.Single(u => u.Name == "Eigene Übung").NameEn);
        Assert.Equal("FCI-UPr 1 – Obedience", (await dienst.GetRegulationsAsync(sport.Id)).Value!.Single().NameEn);

        var detail = (await dienst.GetRegulationDetailAsync(ordnung.Id)).Value!;
        Assert.Equal("FCI-UPr 1 – Obedience", detail.Regulation.NameEn);
        Assert.Equal("Free heeling", detail.Exercises.Single().ExerciseNameEn);
    }

    // ---- Tagebuch, Plan, Statistik ----

    [Fact]
    public async Task Tagebuch_LiefertEnglischenUebungsnamen_BeiFreitextNull()
    {
        var db = InMemoryDbContext.Create();
        var halter = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = halter, Role = DogOwnerRole.Owner });
        var sport = new Sport { Code = "UPR", Name = "UPr" };
        var uebung = new Exercise { SportId = sport.Id, Name = "Freifolge", NameEn = "Free heeling" };
        db.Sports.Add(sport);
        db.Exercises.Add(uebung);
        await db.SaveChangesAsync();
        var dienst = new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService());

        var ergebnis = await dienst.CreateAsync(halter, new CreateTrainingSessionRequest(hund.Id, DateOnly.FromDateTime(DateTime.Today), 10, null,
        [
            new CreateTrainingExerciseRequest(uebung.Id, 4, ExerciseDifficulty.Beginner, true, null, null, null),
            new CreateTrainingExerciseRequest(null, 3, ExerciseDifficulty.Beginner, true, null, null, "Kopfarbeit"),
        ]));

        Assert.True(ergebnis.Succeeded);
        var eintraege = ergebnis.Value!.Exercises;
        Assert.Equal("Free heeling", eintraege.Single(e => e.ExerciseId == uebung.Id).ExerciseNameEn);
        Assert.Equal("Freifolge", eintraege.Single(e => e.ExerciseId == uebung.Id).ExerciseName);
        Assert.Null(eintraege.Single(e => e.ExerciseId is null).ExerciseNameEn);
    }

    [Fact]
    public async Task Statistik_LiefertEnglischenNamenDerKatalogUebung()
    {
        var db = InMemoryDbContext.Create();
        var halter = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = halter });
        var sport = new Sport { Code = "UPR", Name = "UPr" };
        var uebung = new Exercise { SportId = sport.Id, Name = "Freifolge", NameEn = "Free heeling" };
        db.Sports.Add(sport);
        db.Exercises.Add(uebung);
        var einheit = new TrainingSession { UserId = halter, DogId = hund.Id, Date = DateOnly.FromDateTime(DateTime.Today), DurationMinutes = 10 };
        db.TrainingSessions.Add(einheit);
        await db.SaveChangesAsync();
        db.TrainingExercises.Add(new TrainingExercise { TrainingSessionId = einheit.Id, ExerciseId = uebung.Id, Rating = 4, Success = true });
        db.TrainingExercises.Add(new TrainingExercise { TrainingSessionId = einheit.Id, FreeTextLabel = "Kopfarbeit", Rating = 5, Success = true });
        await db.SaveChangesAsync();

        var stat = (await new StatsService(db).GetDogExerciseStatsAsync(halter, hund.Id)).Value!;

        Assert.Equal("Free heeling", stat.Single(s => s.ExerciseName == "Freifolge").ExerciseNameEn);
        Assert.Null(stat.Single(s => s.ExerciseName == "Kopfarbeit").ExerciseNameEn);
    }

    [Fact]
    public async Task Ziel_LiefertEnglischeNamenVonSportOrdnungUndPlanuebungen()
    {
        var db = InMemoryDbContext.Create();
        await SportCatalogSeeder.SeedAsync(new ServiceCollection().AddSingleton(db).BuildServiceProvider());
        var halter = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = halter, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        var sport = await db.Sports.SingleAsync(s => s.Code == "UPR");
        var ordnung = await db.Regulations.SingleAsync(r => r.Name == "FCI-UPr 1");
        var dienst = new GoalService(db, TimeProvider.System, new FakeNotificationService(), new ExerciseMasteryService(db));

        var ergebnis = await dienst.CreateAsync(halter, new CreateGoalRequest(hund.Id, sport.Id, ordnung.Id, DateOnly.FromDateTime(DateTime.Today.AddMonths(3)), null));

        Assert.True(ergebnis.Succeeded);
        var ziel = ergebnis.Value!;
        Assert.Equal("Obedience (FCI-UPr)", ziel.SportNameEn);
        Assert.Equal("FCI-UPr 1 – Obedience", ziel.RegulationNameEn);
        var imPlan = ziel.TrainingPlan!.Items.Where(i => i.ExerciseId is not null).ToList();
        Assert.NotEmpty(imPlan);
        Assert.All(imPlan, i => Assert.False(string.IsNullOrWhiteSpace(i.ExerciseNameEn), i.ExerciseName));
        Assert.Contains(imPlan, i => i.ExerciseName == "Freifolge" && i.ExerciseNameEn == "Free heeling");

        var gewichtbar = (await dienst.GetWeightableExercisesAsync(halter, ziel.Id)).Value!;
        Assert.Equal("Free heeling", gewichtbar.Single(g => g.ExerciseName == "Freifolge").ExerciseNameEn);
    }
}
