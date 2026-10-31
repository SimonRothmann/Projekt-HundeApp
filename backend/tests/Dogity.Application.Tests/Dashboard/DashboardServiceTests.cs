using System.Reflection;
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
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Dashboard;

/// <summary>
/// Testet die gebündelte Startseite. Sie ersetzt eine Reihe einzelner Abrufe
/// und darf nicht mehr zeigen als diese: eigene aktive Hunde, aktive Ziele mit
/// Plan, noch suchbare Spuren (Fährte 24, Trail 48 Stunden nach dem Legen).
/// </summary>
public class DashboardServiceTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = DateOnly.FromDateTime(Jetzt.UtcDateTime);

    private sealed class FesteUhr(DateTimeOffset? jetzt = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt ?? Jetzt;
    }

    private static (DashboardService Dienst, ApplicationDbContext Db) Erstelle() => Erstelle(new FakeUserLookupService());

    private static (DashboardService Dienst, ApplicationDbContext Db) Erstelle(FakeUserLookupService nutzer, TimeProvider? uhr = null)
    {
        var db = InMemoryDbContext.Create();
        uhr ??= new FesteUhr();
        var mastery = new ExerciseMasteryService(db);
        var dienst = new DashboardService(
            new DogService(db, nutzer, new FakeNotificationService()),
            new PreferenceService(db),
            new GoalService(db, uhr, new FakeNotificationService(), mastery),
            new TrainingService(db, new FakeNotificationService(), nutzer, mastery, new FakeWeatherEnrichmentService(), TimeProvider.System),
            new GpsTrackService(db, new FakeWeatherEnrichmentService()),
            new GroupService(db, nutzer, new TrainerRoleService(db, nutzer), new FakeNotificationService(), TimeProvider.System),
            new ClubService(db, nutzer, new FakeNotificationService(), new TrainerRoleService(db, nutzer)),
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

    /// <summary>Eine Spur mit einem automatischen Punkt zum Ende des Legens (ohne <paramref name="legeEnde"/>: ohne Punkt).</summary>
    private static Guid Faehrte(ApplicationDbContext db, Guid nutzer, Guid hund, DateOnly datum, DateTimeOffset? legeEnde = null, GpsTrackKind art = GpsTrackKind.Faehrte)
    {
        var einheit = new TrainingSession { UserId = nutzer, DogId = hund, Date = datum, DurationMinutes = 10 };
        var track = new GpsTrack { TrainingSessionId = einheit.Id, Kind = art };
        db.TrainingSessions.Add(einheit);
        db.GpsTracks.Add(track);
        if (legeEnde is { } ende)
            db.GpsPoints.Add(new GpsPoint { TrackId = track.Id, Latitude = 52, Longitude = 9, Timestamp = ende });
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
    public async Task NimmtDieNochSuchbarenSpuren_AuchVonGesternUndVorgestern()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        // Fährte: 24 Stunden nach dem Legen. Abends gelegt (vor 15 Stunden) bleibt sie auch nach Mitternacht.
        var gesternAbend = Faehrte(db, ich, hund, Heute.AddDays(-1), Jetzt.AddHours(-15));
        var heute = Faehrte(db, ich, hund, Heute, Jetzt.AddHours(-1));
        Faehrte(db, ich, hund, Heute.AddDays(-1), Jetzt.AddHours(-25));
        // Trail: 48 Stunden.
        var trail = Faehrte(db, ich, hund, Heute.AddDays(-1), Jetzt.AddHours(-30), GpsTrackKind.Mantrailing);
        Faehrte(db, ich, hund, Heute.AddDays(-2), Jetzt.AddHours(-49), GpsTrackKind.Mantrailing);
        // Ohne Zeitstempel lässt sich die Frist nicht prüfen.
        Faehrte(db, ich, hund, Heute);
        // Eine Einheit ohne Fährte am selben Tag bringt keinen Eintrag.
        db.TrainingSessions.Add(new TrainingSession { UserId = ich, DogId = hund, Date = Heute, DurationMinutes = 30 });
        await db.SaveChangesAsync();

        var stand = (await dienst.GetAsync(ich)).Value!;

        Assert.Equivalent(new[] { gesternAbend, heute, trail }, Assert.Single(stand.Dogs).TracksToday.Select(t => t.Id));
    }

    [Fact]
    public async Task EineFaehrteGenauAnDerFristgrenze_IstNochSuchbar()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        var genau = Faehrte(db, ich, hund, Heute.AddDays(-1), Jetzt - Ablauffristen.Faehrte);
        Faehrte(db, ich, hund, Heute.AddDays(-1), Jetzt - Ablauffristen.Faehrte - TimeSpan.FromSeconds(1));
        await db.SaveChangesAsync();

        var stand = (await dienst.GetAsync(ich)).Value!;

        Assert.Equal(genau, Assert.Single(Assert.Single(stand.Dogs).TracksToday).Id);
    }

    [Fact]
    public async Task HeuteIstDerOrtszeitTag_AuchNachMitternachtInUtcNochNicht()
    {
        // 23:30 UTC im Sommer = 01:30 Uhr am Folgetag deutscher Zeit: Die Fährte,
        // die der Recorder jetzt auf diesen Tag datiert, gehört zu "heute".
        var nachts = new DateTimeOffset(2026, 7, 14, 23, 30, 0, TimeSpan.Zero);
        var (dienst, db) = Erstelle(new FakeUserLookupService(), new FesteUhr(nachts));
        var ich = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        var vonHeute = Faehrte(db, ich, hund, new DateOnly(2026, 7, 15), nachts.AddMinutes(-20));
        Faehrte(db, ich, hund, new DateOnly(2026, 7, 14), nachts.AddHours(-37));
        await db.SaveChangesAsync();

        var stand = (await dienst.GetAsync(ich)).Value!;

        Assert.Equal(vonHeute, Assert.Single(Assert.Single(stand.Dogs).TracksToday).Id);
    }

    // ------------------------------------------------- Fehler von Teilabfragen

    /// <summary>
    /// Leitet alle Aufrufe an <see cref="Ziel"/> weiter, außer <see cref="Methode"/>: Die
    /// scheitert mit einem Fehlschlag - so lässt sich der Ausfall einer Teilabfrage nachstellen.
    /// </summary>
    private class AusfallProxy<T> : DispatchProxy where T : class
    {
        public T Ziel { get; set; } = null!;
        public string Methode { get; set; } = "";

        protected override object? Invoke(MethodInfo? methode, object?[]? argumente)
        {
            if (methode!.Name != Methode)
            {
                try { return methode.Invoke(Ziel, argumente); }
                catch (TargetInvocationException e) { throw e.InnerException!; }
            }
            var ergebnisTyp = methode.ReturnType.GetGenericArguments()[0];
            var fehlschlag = ergebnisTyp
                .GetMethod("Failure", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, [typeof(string[])])!
                .Invoke(null, [new[] { "Ausfall." }]);
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(ergebnisTyp).Invoke(null, [fehlschlag]);
        }
    }

    private static T MitAusfall<T>(T echt, string methode) where T : class
    {
        var proxy = DispatchProxy.Create<T, AusfallProxy<T>>();
        var ausfall = (AusfallProxy<T>)(object)proxy;
        ausfall.Ziel = echt;
        ausfall.Methode = methode;
        return proxy;
    }

    [Theory]
    [InlineData("preferences", "GetEffectiveDogSportsAsync")]
    [InlineData("goals", "GetByDogAsync")]
    [InlineData("trainings", "GetByDogAsync")]
    [InlineData("tracks", "GetByTrainingSessionAsync")]
    [InlineData("trainings", "GetOpenFeedbackAsync")]
    public async Task EinFehlerEinerTeilabfrage_WirdWeitergereicht_StattAlsLeereListeZuErscheinen(string dienstName, string methode)
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var mastery = new ExerciseMasteryService(db);
        IPreferenceService preferences = new PreferenceService(db);
        IGoalService goals = new GoalService(db, new FesteUhr(), new FakeNotificationService(), mastery);
        ITrainingService trainings = new TrainingService(db, new FakeNotificationService(), nutzer, mastery, new FakeWeatherEnrichmentService(), TimeProvider.System);
        IGpsTrackService tracks = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        switch (dienstName)
        {
            case "preferences": preferences = MitAusfall(preferences, methode); break;
            case "goals": goals = MitAusfall(goals, methode); break;
            case "trainings": trainings = MitAusfall(trainings, methode); break;
            default: tracks = MitAusfall(tracks, methode); break;
        }
        var dienst = new DashboardService(
            new DogService(db, nutzer, new FakeNotificationService()), preferences, goals, trainings, tracks,
            new GroupService(db, nutzer, new TrainerRoleService(db, nutzer), new FakeNotificationService(), TimeProvider.System),
            new ClubService(db, nutzer, new FakeNotificationService(), new TrainerRoleService(db, nutzer)),
            new FesteUhr());
        var ich = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        Faehrte(db, ich, hund, Heute, Jetzt.AddHours(-1));
        await db.SaveChangesAsync();

        var ergebnis = await dienst.GetAsync(ich);

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Ausfall.", Assert.Single(ergebnis.Errors));
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

    [Fact]
    public async Task Gruppeneinladungen_NurDieEigenenOffenenAlsMitglied()
    {
        var (dienst, db) = Erstelle();
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        var verein = new Dogity.Domain.Community.Club { Name = "Testverein" };
        var eingeladen = new Dogity.Domain.Community.Group { ClubId = verein.Id, TrainerId = Guid.NewGuid(), Name = "Dienstagsgruppe" };
        var aktiv = new Dogity.Domain.Community.Group { ClubId = verein.Id, TrainerId = Guid.NewGuid(), Name = "Aktive Gruppe" };
        var fremd = new Dogity.Domain.Community.Group { ClubId = verein.Id, TrainerId = Guid.NewGuid(), Name = "Fremde Einladung" };
        db.Clubs.Add(verein);
        db.Groups.AddRange(eingeladen, aktiv, fremd);
        db.GroupMembers.Add(new Dogity.Domain.Community.GroupMember { GroupId = eingeladen.Id, UserId = ich, Status = Dogity.Domain.Community.GroupMemberStatus.Invited });
        db.GroupMembers.Add(new Dogity.Domain.Community.GroupMember { GroupId = aktiv.Id, UserId = ich, Status = Dogity.Domain.Community.GroupMemberStatus.Active });
        db.GroupMembers.Add(new Dogity.Domain.Community.GroupMember { GroupId = fremd.Id, UserId = andere, Status = Dogity.Domain.Community.GroupMemberStatus.Invited });
        await db.SaveChangesAsync();

        var einladungen = (await dienst.GetAsync(ich)).Value!.GroupInvitations;

        var eintrag = Assert.Single(einladungen);
        Assert.Equal(eingeladen.Id, eintrag.GroupId);
        Assert.Equal("Dienstagsgruppe", eintrag.GroupName);
        Assert.Equal("Testverein", eintrag.ClubName);
    }
}
