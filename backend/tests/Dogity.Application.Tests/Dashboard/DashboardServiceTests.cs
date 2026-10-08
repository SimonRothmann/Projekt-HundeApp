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

    private static (DashboardService Dienst, ApplicationDbContext Db) Erstelle() => Erstelle(new FakeUserLookupService());

    private static (DashboardService Dienst, ApplicationDbContext Db) Erstelle(FakeUserLookupService nutzer)
    {
        var db = InMemoryDbContext.Create();
        var uhr = new FesteUhr();
        var mastery = new ExerciseMasteryService(db);
        var dienst = new DashboardService(
            new DogService(db, nutzer, new FakeNotificationService()),
            new PreferenceService(db),
            new GoalService(db, uhr, new FakeNotificationService(), mastery),
            new TrainingService(db, new FakeNotificationService(), nutzer, mastery, new FakeWeatherEnrichmentService()),
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

    private static TrainingSession Feedback(ApplicationDbContext db, Guid besitzer, Guid hund, Guid? trainer, string text, DateTimeOffset am,
        FeedbackReaction? reaktion = null, string? rueckfrage = null)
    {
        var einheit = new TrainingSession
        {
            UserId = besitzer, DogId = hund, Date = Heute, DurationMinutes = 10,
            TrainerFeedback = text, FeedbackByTrainerId = trainer, FeedbackAt = am,
            OwnerReaction = reaktion, OwnerReply = rueckfrage
        };
        db.TrainingSessions.Add(einheit);
        return einheit;
    }

    [Fact]
    public async Task OffenesFeedback_ZeigtUnbeantwortetesNeuestesZuerstMitTrainerName()
    {
        var nutzer = new FakeUserLookupService();
        var trainer = Guid.NewGuid();
        nutzer.Register(trainer, "t@example.org", "Tina", "Trainer");
        var (dienst, db) = Erstelle(nutzer);
        var ich = Guid.NewGuid();
        var bello = Hund(db, ich, "Bello");
        var emma = Hund(db, ich, "Emma");
        var aelter = Feedback(db, ich, bello, trainer, "Schön gearbeitet", Jetzt.AddDays(-3));
        var neuer = Feedback(db, ich, emma, trainer, "Bleib dran", Jetzt.AddDays(-1));
        await db.SaveChangesAsync();

        var offen = (await dienst.GetAsync(ich)).Value!.OpenFeedback;

        Assert.Equal([neuer.Id, aelter.Id], offen.Select(f => f.SessionId));
        Assert.Equal("Emma", offen[0].DogName);
        Assert.Equal("Tina Trainer", offen[0].TrainerName);
        Assert.Equal("Bleib dran", offen[0].Feedback);
    }

    [Fact]
    public async Task OffenesFeedback_BeantwortetesUndFeedbackOhneTextFaelltWeg()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var bello = Hund(db, ich, "Bello");
        Feedback(db, ich, bello, null, "Danke gesagt", Jetzt, reaktion: FeedbackReaction.Thanks);
        Feedback(db, ich, bello, null, "Rückfrage gestellt", Jetzt, rueckfrage: "Wie meinst du das?");
        Feedback(db, ich, bello, null, "", Jetzt);
        var offenes = Feedback(db, ich, bello, null, "Noch offen", Jetzt);
        await db.SaveChangesAsync();

        var offen = (await dienst.GetAsync(ich)).Value!.OpenFeedback;

        var eintrag = Assert.Single(offen);
        Assert.Equal(offenes.Id, eintrag.SessionId);
        // Trainer:in ohne auffindbares Konto: kein Name statt eines Platzhalters.
        Assert.Null(eintrag.TrainerName);
    }

    [Fact]
    public async Task OffenesFeedback_FremderHundUndBetreuterHundZaehlenNicht()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var fremder = Guid.NewGuid();
        Hund(db, ich, "Bello");
        var nachbarshund = Hund(db, fremder, "Nachbarshund");
        Feedback(db, fremder, nachbarshund, null, "Nicht für mich", Jetzt);
        // Als Trainer:in betreut: erscheint nicht in den eigenen Hunden und ist keine Aufgabe.
        db.TrainerAssignments.Add(new Dogity.Domain.Community.TrainerAssignment { TrainerId = ich, MemberId = fremder, DogId = nachbarshund });
        await db.SaveChangesAsync();

        var offen = (await dienst.GetAsync(ich)).Value!.OpenFeedback;

        Assert.Empty(offen);
    }

    [Fact]
    public async Task OffenesFeedback_MitbesitzerSiehtFeedbackDesGemeinsamenHundes()
    {
        var (dienst, db) = Erstelle();
        var besitzerin = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        var bello = Hund(db, besitzerin, "Bello");
        db.DogOwners.Add(new DogOwner { DogId = bello, UserId = mitbesitzer, Role = DogOwnerRole.Owner });
        var einheit = Feedback(db, besitzerin, bello, null, "Gemeinsam", Jetzt);
        await db.SaveChangesAsync();

        var offen = (await dienst.GetAsync(mitbesitzer)).Value!.OpenFeedback;

        Assert.Equal(einheit.Id, Assert.Single(offen).SessionId);
    }
}
