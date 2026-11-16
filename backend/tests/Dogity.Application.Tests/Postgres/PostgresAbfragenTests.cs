using Dogity.Application.Community;
using Dogity.Application.Dashboard;
using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Tracking;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Postgres;

/// <summary>
/// Die wichtigsten Abfragewege gegen ein echtes Postgres (siehe <see cref="PostgresDatenbank"/>).
/// Geprüft wird, dass das SQL läuft und die Zeilen nicht vervielfacht - die Fachlogik
/// steht in den In-Memory-Tests daneben.
/// </summary>
public class PostgresAbfragenTests(PostgresDatenbank postgres) : IClassFixture<PostgresDatenbank>
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jetzt = T0.AddHours(3);
    private static readonly DateOnly Tag = new(2026, 1, 1);
    private const double BaseLat = 50.0;
    private const double BaseLon = 8.0;
    private const double MetersPerDegreeLat = 111320.0;

    private static double LatAt(double meters) => BaseLat + meters / MetersPerDegreeLat;

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private async Task<(ApplicationDbContext Db, Guid Nutzer, Guid Hund)> AufbauenAsync()
    {
        var db = postgres.NeuerKontext();
        var nutzer = Guid.NewGuid();
        var hund = new Dog { Name = "Rex" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return (db, nutzer, hund.Id);
    }

    /// <summary>Fünf Linienpunkte entlang von 80 m, dazu drei Gegenstände und ein Leckerlipot - wie beim Legen im Gelände.</summary>
    private static CreateGpsTrackRequest Legung(Guid hund)
    {
        var punkte = Enumerable.Range(0, 5)
            .Select(i => new CreateGpsPointRequest(LatAt(i * 20), BaseLon, T0.AddMinutes(i), 5))
            .ToList();
        punkte.Add(new CreateGpsPointRequest(LatAt(20), BaseLon, T0.AddSeconds(30), 5, GpsPointType.Manual, null, GpsMarkerType.Article));
        punkte.Add(new CreateGpsPointRequest(LatAt(30), BaseLon, T0.AddSeconds(90), 5, GpsPointType.Manual, null, GpsMarkerType.TreatPot));
        punkte.Add(new CreateGpsPointRequest(LatAt(40), BaseLon, T0.AddSeconds(150), 5, GpsPointType.Manual, null, GpsMarkerType.Article));
        punkte.Add(new CreateGpsPointRequest(LatAt(60), BaseLon, T0.AddSeconds(210), 5, GpsPointType.Manual, null, GpsMarkerType.Article));
        return new CreateGpsTrackRequest(null, 80, null, null, null, null, null, punkte,
            DogId: hund, Date: Tag, DurationMinutes: 30);
    }

    private static CreateGpsWalkRunRequest Ablauf() =>
        new(80, null, Enumerable.Range(0, 5)
            .Select(i => new CreateGpsWalkPointRequest(LatAt(i * 20), BaseLon, T0.AddHours(1).AddMinutes(i), 5))
            .ToList());

    [PostgresFact]
    public async Task Migrationen_LassenKeinenUnterschiedZumModellZurueck()
    {
        await using var db = postgres.NeuerKontext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        // Eine Modelländerung ohne Migration fiele sonst erst beim Deploy auf (oder gar nicht).
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Faehrte_AnlegenAuswertenUndLaden_ZeilenWerdenNichtVervielfacht()
    {
        var (db, nutzer, hund) = await AufbauenAsync();
        await using var _ = db;
        var dienst = new GpsTrackService(db, new FakeWeatherEnrichmentService());

        var spur = (await dienst.CreateAsync(nutzer, Legung(hund))).Value!;
        var lauf = await dienst.AddWalkRunAsync(nutzer, spur.Id, Ablauf());
        Assert.True(lauf.Succeeded, string.Join(" ", lauf.Errors));
        var ausgewertet = await dienst.EvaluateWalkRunAsync(nutzer, spur.Id, lauf.Value!.Id);
        Assert.True(ausgewertet.Succeeded, string.Join(" ", ausgewertet.Errors));

        // Frischer Kontext: gelesen wird aus der Datenbank, nicht aus dem Merker des Anlegens.
        await using var lesen = postgres.NeuerKontext();
        var spuren = (await new GpsTrackService(lesen, new FakeWeatherEnrichmentService())
            .GetByTrainingSessionAsync(nutzer, spur.TrainingSessionId)).Value!;

        var geladen = Assert.Single(spuren);
        Assert.Equal(9, geladen.Points.Count);
        Assert.Equal(9, geladen.Points.Select(p => p.Id).Distinct().Count());
        var geladenerLauf = Assert.Single(geladen.WalkRuns);
        Assert.Equal(5, geladenerLauf.Points.Count);
        Assert.Equal(3, geladenerLauf.ArticlesTotal);
        Assert.NotNull(geladenerLauf.EvaluatedAt);
    }

    [PostgresFact]
    public async Task Trainingstag_AnlegenUndLaden()
    {
        var (db, nutzer, hund) = await AufbauenAsync();
        await using var _ = db;
        var sport = new Sport { Code = "PGTEST", Name = "Testsport" };
        var uebung = new Exercise { SportId = sport.Id, Name = "Fußarbeit" };
        db.Sports.Add(sport);
        db.Exercises.Add(uebung);
        await db.SaveChangesAsync();
        var dienst = new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(),
            new ExerciseMasteryService(db), new FakeWeatherEnrichmentService(), TimeProvider.System);

        var angelegt = await dienst.CreateAsync(nutzer, new CreateTrainingSessionRequest(hund, Tag, 20, "Notiz",
            [new CreateTrainingExerciseRequest(uebung.Id, 4, ExerciseDifficulty.Beginner, true, null),
             new CreateTrainingExerciseRequest(null, 3, ExerciseDifficulty.Beginner, false, null, FreeTextLabel: "Spaßübung")]));
        Assert.True(angelegt.Succeeded, string.Join(" ", angelegt.Errors));

        await using var lesen = postgres.NeuerKontext();
        var einheiten = (await new TrainingService(lesen, new FakeNotificationService(), new FakeUserLookupService(),
            new ExerciseMasteryService(lesen), new FakeWeatherEnrichmentService(), TimeProvider.System)
            .GetByDogAsync(nutzer, hund)).Value!;

        var einheit = Assert.Single(einheiten);
        Assert.Equal(2, einheit.Exercises.Count);
    }

    [PostgresFact]
    public async Task Ziele_Laden_MitPlan()
    {
        var (db, nutzer, hund) = await AufbauenAsync();
        await using var _ = db;
        var sport = new Sport { Code = "PGZIEL", Name = "Zielsport" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();
        var ziele = new GoalService(db, new FesteUhr(), new FakeNotificationService(), new ExerciseMasteryService(db));

        var angelegt = await ziele.CreateAsync(nutzer, new CreateGoalRequest(hund, sport.Id, null, Tag.AddMonths(2), null, IsCustom: true));
        Assert.True(angelegt.Succeeded, string.Join(" ", angelegt.Errors));

        await using var lesen = postgres.NeuerKontext();
        var geladen = (await new GoalService(lesen, new FesteUhr(), new FakeNotificationService(), new ExerciseMasteryService(lesen))
            .GetByDogAsync(nutzer, hund)).Value!;

        var ziel = Assert.Single(geladen);
        Assert.NotNull(ziel.TrainingPlan);
    }

    [PostgresFact]
    public async Task Startseite_MitZielTrainingUndFaehrte()
    {
        var (db, nutzer, hund) = await AufbauenAsync();
        await using var _ = db;
        var sport = new Sport { Code = "PGDASH", Name = "Dashboardsport" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();

        var uhr = new FesteUhr();
        var lookup = new FakeUserLookupService();
        var mastery = new ExerciseMasteryService(db);
        var dienst = new DashboardService(
            new DogService(db, lookup, new FakeNotificationService()),
            new PreferenceService(db),
            new GoalService(db, uhr, new FakeNotificationService(), mastery),
            new TrainingService(db, new FakeNotificationService(), lookup, mastery, new FakeWeatherEnrichmentService(), TimeProvider.System),
            new GpsTrackService(db, new FakeWeatherEnrichmentService()),
            new GroupService(db, lookup, new TrainerRoleService(db, lookup), new FakeNotificationService(), TimeProvider.System),
            new ClubService(db, lookup, new FakeNotificationService(), new TrainerRoleService(db, lookup)),
            uhr);

        var ziel = await new GoalService(db, uhr, new FakeNotificationService(), mastery)
            .CreateAsync(nutzer, new CreateGoalRequest(hund, sport.Id, null, Tag.AddMonths(2), null, IsCustom: true));
        Assert.True(ziel.Succeeded, string.Join(" ", ziel.Errors));
        var spur = await new GpsTrackService(db, new FakeWeatherEnrichmentService()).CreateAsync(nutzer, Legung(hund));
        Assert.True(spur.Succeeded, string.Join(" ", spur.Errors));

        var startseite = await dienst.GetAsync(nutzer);

        Assert.True(startseite.Succeeded, string.Join(" ", startseite.Errors));
        var eintrag = Assert.Single(startseite.Value!.Dogs);
        Assert.Equal("Rex", eintrag.Dog.Name);
        Assert.Single(eintrag.ActiveGoals);
        Assert.Single(eintrag.TracksToday);
    }
}
