using Dogity.Application.Dashboard;
using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Dashboard;

/// <summary>
/// Testet die gebündelte Startseite. Sie ersetzt eine Reihe einzelner Abrufe
/// und darf nicht mehr zeigen als diese: eigene aktive Hunde, aktive Ziele mit
/// Plan, heute gelegte Fährten.
/// </summary>
public class DashboardServiceTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = DateOnly.FromDateTime(Jetzt.UtcDateTime);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private static (DashboardService Dienst, ApplicationDbContext Db) Erstelle()
    {
        var db = InMemoryDbContext.Create();
        var uhr = new FesteUhr();
        var mastery = new ExerciseMasteryService(db);
        var dienst = new DashboardService(
            new DogService(db, new FakeUserLookupService()),
            new PreferenceService(db),
            new GoalService(db, uhr, new FakeNotificationService(), mastery),
            new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), mastery, new FakeWeatherEnrichmentService()),
            new GpsTrackService(db, new FakeWeatherEnrichmentService()),
            uhr);
        return (dienst, db);
    }

    private static Guid Hund(ApplicationDbContext db, Guid besitzer, string name, bool archiviert = false)
    {
        var hund = new Dog { Name = name, ArchivedAt = archiviert ? Jetzt : null };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer, Role = DogOwnerRole.Owner });
        return hund.Id;
    }

    private static Guid Ziel(ApplicationDbContext db, Guid hund, GoalStatus status, bool mitPlan)
    {
        var ziel = new Goal { DogId = hund, SportId = Guid.NewGuid(), TargetDate = Heute.AddMonths(2), Status = status };
        if (mitPlan)
            ziel.TrainingPlan = new TrainingPlan { GoalId = ziel.Id, Goal = ziel };
        db.Goals.Add(ziel);
        return ziel.Id;
    }

    private static Guid Faehrte(ApplicationDbContext db, Guid nutzer, Guid hund, DateOnly datum)
    {
        var einheit = new TrainingSession { UserId = nutzer, DogId = hund, Date = datum, DurationMinutes = 10 };
        var track = new GpsTrack { TrainingSessionId = einheit.Id };
        db.TrainingSessions.Add(einheit);
        db.GpsTracks.Add(track);
        return track.Id;
    }

    [Fact]
    public async Task ZeigtNurEigeneAktiveHunde()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var bello = Hund(db, ich, "Bello");
        Hund(db, ich, "Rentner", archiviert: true);
        Hund(db, Guid.NewGuid(), "Nachbarshund");
        await db.SaveChangesAsync();

        var stand = (await dienst.GetAsync(ich)).Value!;

        Assert.Equal(bello, Assert.Single(stand.Dogs).Dog.Id);
    }

    [Fact]
    public async Task NimmtNurAktiveZieleMitPlan()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        var mitPlan = Ziel(db, hund, GoalStatus.Active, mitPlan: true);
        Ziel(db, hund, GoalStatus.Active, mitPlan: false);
        Ziel(db, hund, GoalStatus.Achieved, mitPlan: true);
        await db.SaveChangesAsync();

        var stand = (await dienst.GetAsync(ich)).Value!;

        Assert.Equal(mitPlan, Assert.Single(Assert.Single(stand.Dogs).ActiveGoals).Id);
    }

    [Fact]
    public async Task NimmtNurHeuteGelegteFaehrten()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        var vonHeute = Faehrte(db, ich, hund, Heute);
        Faehrte(db, ich, hund, Heute.AddDays(-1));
        // Eine Einheit ohne Fährte am selben Tag bringt keinen Eintrag.
        db.TrainingSessions.Add(new TrainingSession { UserId = ich, DogId = hund, Date = Heute, DurationMinutes = 30 });
        await db.SaveChangesAsync();

        var stand = (await dienst.GetAsync(ich)).Value!;

        Assert.Equal(vonHeute, Assert.Single(Assert.Single(stand.Dogs).TracksToday).Id);
    }

    [Fact]
    public async Task OhneHund_IstDieListeLeer()
    {
        var (dienst, _) = Erstelle();

        var ergebnis = await dienst.GetAsync(Guid.NewGuid());

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(ergebnis.Value!.Dogs);
    }
}
