using Dogity.Application.Notifications;
using Dogity.Application.Reminders;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Reminders;

/// <summary>
/// Der Versand der Erinnerungen gegen eine Datenbank: wer sie bekommt, wer nicht, und dass
/// ein zweiter Lauf nichts doppelt schickt. Wann etwas fällig ist, prüfen die
/// ErinnerungsregelnTests ohne Datenbank; hier geht es um die Abfragen drumherum.
/// </summary>
public class ErinnerungServiceTests
{
    private sealed class Uhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Szene(ApplicationDbContext Db, FakeNotificationService Meldungen, Uhr Uhr, ErinnerungService Dienst);

    private static DateTimeOffset Utc(int tag, int stunde, int minute = 0, int monat = 10) =>
        new(2026, monat, tag, stunde, minute, 0, TimeSpan.Zero);

    // Donnerstag, 22.10.2026, 18:30 Uhr deutscher Zeit (CEST).
    private static Szene Aufbauen(DateTimeOffset? jetzt = null)
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var uhr = new Uhr(jetzt ?? Utc(22, 16, 30));
        return new Szene(db, meldungen, uhr, new ErinnerungService(db, meldungen, uhr, NullLogger<ErinnerungService>.Instance));
    }

    private static List<(Guid UserId, string Message, string? LinkPath)> Gesendet(Szene s) => s.Meldungen.Created;

    // ---------------------------------------------------------------- Termine

    private static async Task<(Guid Gruppe, Guid Termin)> TerminAsync(
        Szene s, DateTimeOffset beginn, GroupTrainingSessionStatus status = GroupTrainingSessionStatus.Planned)
    {
        var gruppe = new Group { Name = "Welpengruppe", TrainerId = Guid.NewGuid() };
        var termin = new GroupTrainingSession { ClubId = Guid.NewGuid(), GroupId = gruppe.Id, StartsAt = beginn, Status = status };
        s.Db.Groups.Add(gruppe);
        s.Db.GroupTrainingSessions.Add(termin);
        await s.Db.SaveChangesAsync();
        return (gruppe.Id, termin.Id);
    }

    private static async Task<Guid> MitgliedAsync(Szene s, Guid gruppe, GroupMemberStatus status = GroupMemberStatus.Active)
    {
        var id = Guid.NewGuid();
        s.Db.GroupMembers.Add(new GroupMember { GroupId = gruppe, UserId = id, Status = status });
        await s.Db.SaveChangesAsync();
        return id;
    }

    private static async Task AntwortAsync(Szene s, Guid termin, Guid person, bool kommt)
    {
        s.Db.GroupTrainingSessionResponses.Add(new GroupTrainingSessionResponse { GroupTrainingSessionId = termin, UserId = person, IsAttending = kommt });
        await s.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Termine_Zusage_KeineAntwort_Absage_Eingeladene()
    {
        var s = Aufbauen();
        var (gruppe, termin) = await TerminAsync(s, Utc(23, 16));
        var zugesagt = await MitgliedAsync(s, gruppe);
        var offen = await MitgliedAsync(s, gruppe);
        var abgesagt = await MitgliedAsync(s, gruppe);
        var eingeladen = await MitgliedAsync(s, gruppe, GroupMemberStatus.Invited);
        var anfrage = await MitgliedAsync(s, gruppe, GroupMemberStatus.Pending);
        await AntwortAsync(s, termin, zugesagt, true);
        await AntwortAsync(s, termin, abgesagt, false);

        var anzahl = await s.Dienst.VersendeFaelligeAsync();

        Assert.Equal(2, anzahl);
        Assert.Equal("Morgen 18:00: Welpengruppe - du hast zugesagt.", Assert.Single(Gesendet(s), g => g.UserId == zugesagt).Message);
        Assert.Equal("Morgen 18:00: Welpengruppe - kommst du?", Assert.Single(Gesendet(s), g => g.UserId == offen).Message);
        Assert.DoesNotContain(Gesendet(s), g => g.UserId == abgesagt || g.UserId == eingeladen || g.UserId == anfrage);
        Assert.All(Gesendet(s), g => Assert.Equal("/dashboard", g.LinkPath));
        // Glocke und Push, keine Mail: Erinnerungen sind nie "wichtig".
        Assert.Empty(s.Meldungen.Wichtige);
    }

    [Fact]
    public async Task Termine_ZweiterLauf_SchicktNichtsNochmal()
    {
        var s = Aufbauen();
        var (gruppe, _) = await TerminAsync(s, Utc(23, 16));
        await MitgliedAsync(s, gruppe);

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        s.Uhr.Jetzt = Utc(22, 16, 40);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        // Auch ein neuer Dienst auf derselben Datenbank (Neustart): der Merkzettel steht in der Datenbank.
        var neu = new ErinnerungService(s.Db, s.Meldungen, s.Uhr, NullLogger<ErinnerungService>.Instance);
        Assert.Equal(0, await neu.VersendeFaelligeAsync());

        Assert.Single(Gesendet(s));
        Assert.Equal(1, await s.Db.SentReminders.CountAsync());
    }

    [Fact]
    public async Task Termine_AbgesagterTermin_AndererTag_FremdeGruppe_KommtNichts()
    {
        var s = Aufbauen();
        var (abgesagtGruppe, _) = await TerminAsync(s, Utc(23, 16), GroupTrainingSessionStatus.Cancelled);
        var (uebermorgenGruppe, _) = await TerminAsync(s, Utc(24, 16));
        await MitgliedAsync(s, abgesagtGruppe);
        await MitgliedAsync(s, uebermorgenGruppe);
        // Eine Gruppe ohne Termin morgen
        var fremde = new Group { Name = "Fremde", TrainerId = Guid.NewGuid() };
        s.Db.Groups.Add(fremde);
        await s.Db.SaveChangesAsync();
        await MitgliedAsync(s, fremde.Id);

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        Assert.Empty(Gesendet(s));
    }

    [Fact]
    public async Task Termine_SchalterAus_KommtNichts_OhneZeileGiltAn()
    {
        var s = Aufbauen();
        var (gruppe, _) = await TerminAsync(s, Utc(23, 16));
        var aus = await MitgliedAsync(s, gruppe);
        var ohneZeile = await MitgliedAsync(s, gruppe);
        s.Db.UserPreferences.Add(new UserPreference { UserId = aus, ReminderSessions = false });
        await s.Db.SaveChangesAsync();

        await s.Dienst.VersendeFaelligeAsync();

        Assert.Equal(ohneZeile, Assert.Single(Gesendet(s)).UserId);
    }

    [Theory]
    [InlineData(22, 15, 30)] // 17:30 Uhr - vor 18 Uhr
    [InlineData(22, 20, 10)] // 22:10 Uhr - Ruhezeit
    [InlineData(22, 22, 30)] // 00:30 Uhr - Ruhezeit (der Termin ist dann "heute")
    public async Task Termine_VorAchtzehnUhrUndInDerRuhezeit_KommtNichts(int tag, int stunde, int minute)
    {
        var s = Aufbauen(Utc(tag, stunde, minute));
        var (gruppe, _) = await TerminAsync(s, Utc(23, 16));
        await MitgliedAsync(s, gruppe);

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Termine_ZeitumstellungWochenende_FolgetagHatFuenfundzwanzigStunden()
    {
        // Samstag 24.10., 18:30 Uhr: Der Sonntagstermin um 23:30 Uhr (22:30 UTC) ist "morgen".
        var s = Aufbauen(Utc(24, 16, 30));
        var (gruppe, _) = await TerminAsync(s, Utc(25, 22, 30));
        var (spaet, _) = await TerminAsync(s, Utc(25, 23, 30)); // schon Montag 00:30 Uhr
        var person = await MitgliedAsync(s, gruppe);
        await MitgliedAsync(s, spaet);

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        Assert.Equal("Morgen 23:30: Welpengruppe - kommst du?", Assert.Single(Gesendet(s), g => g.UserId == person).Message);
    }

    [Fact]
    public async Task Termine_GeloeschteGruppe_KommtNichts()
    {
        var s = Aufbauen();
        var (gruppe, _) = await TerminAsync(s, Utc(23, 16));
        await MitgliedAsync(s, gruppe);
        (await s.Db.Groups.FirstAsync(g => g.Id == gruppe)).DeletedAt = DateTimeOffset.UtcNow;
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    // ---------------------------------------------------------------- Fährten

    private sealed record FaehrteSzene(Guid Person, Guid Hund, Guid Training, Guid Faehrte);

    private static async Task<FaehrteSzene> FaehrteAsync(
        Szene s, DateTimeOffset ende, bool besitzer = true, string name = "Bello", Guid? person = null)
    {
        var user = person ?? Guid.NewGuid();
        var hund = new Dog { Name = name };
        s.Db.Dogs.Add(hund);
        if (besitzer) s.Db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = user });
        var training = new TrainingSession { UserId = user, DogId = hund.Id, Date = DateOnly.FromDateTime(ende.UtcDateTime), DurationMinutes = 30 };
        s.Db.TrainingSessions.Add(training);
        var faehrte = new GpsTrack { TrainingSessionId = training.Id, CreatedAt = ende };
        s.Db.GpsTracks.Add(faehrte);
        s.Db.GpsPoints.AddRange(
            new GpsPoint { TrackId = faehrte.Id, Timestamp = ende.AddMinutes(-20), PointType = GpsPointType.Automatic },
            new GpsPoint { TrackId = faehrte.Id, Timestamp = ende, PointType = GpsPointType.Automatic },
            // Ein Marker mit späterem Zeitstempel zählt nicht für das Ende des Legens.
            new GpsPoint { TrackId = faehrte.Id, Timestamp = ende.AddHours(5), PointType = GpsPointType.Manual });
        await s.Db.SaveChangesAsync();
        return new FaehrteSzene(user, hund.Id, training.Id, faehrte.Id);
    }

    [Fact]
    public async Task Faehrte_DreiStundenNachDemLegen_BekommtDieBesitzerinEineErinnerung()
    {
        // Ende 09:00 Uhr (07:00 UTC); jetzt 12:30 Uhr.
        var s = Aufbauen(Utc(22, 10, 30));
        var f = await FaehrteAsync(s, Utc(22, 7));

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());

        var meldung = Assert.Single(Gesendet(s));
        Assert.Equal(f.Person, meldung.UserId);
        Assert.Equal("Die Fährte von Bello ist noch nicht abgelaufen. Du kannst sie noch bis 23.10.2026, 09:00 Uhr ablaufen.", meldung.Message);
        Assert.Equal($"/dogs/{f.Hund}?eintrag={f.Training}", meldung.LinkPath);

        // Ein zweiter Lauf und ein späterer Tick: einmal je Fährte.
        s.Uhr.Jetzt = Utc(22, 10, 50);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Faehrte_ZuFruehZuSpaetOderAbgelaufen_KommtNichts()
    {
        var s = Aufbauen(Utc(22, 10, 30));
        await FaehrteAsync(s, Utc(22, 9));            // erst 1,5 Stunden her
        await FaehrteAsync(s, Utc(21, 6));            // 28,5 Stunden her: Frist vorbei
        var abgelaufen = await FaehrteAsync(s, Utc(22, 6));
        s.Db.GpsWalkRuns.Add(new GpsWalkRun { TrackId = abgelaufen.Faehrte });
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Faehrte_EinMantrailingTrail_BekommtKeineFaehrtenErinnerung()
    {
        // Ein Trail wird nach Stunden gesucht, nicht nach der Frist einer Fährte; die Meldung spräche von einer Fährte.
        var s = Aufbauen(Utc(22, 10, 30));
        var trail = await FaehrteAsync(s, Utc(22, 7));
        (await s.Db.GpsTracks.FirstAsync(t => t.Id == trail.Faehrte)).Kind = GpsTrackKind.Mantrailing;
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Faehrte_HundInTrainingspause_ArchiviertOderKeinZugang_KommtNichts()
    {
        var s = Aufbauen(Utc(22, 10, 30));
        var pausiert = await FaehrteAsync(s, Utc(22, 7), name: "Pausiert");
        s.Db.DogPauses.Add(new DogPause { DogId = pausiert.Hund, Reason = DogPauseReason.Injured, StartDate = new DateOnly(2026, 10, 20) });
        var archiviert = await FaehrteAsync(s, Utc(22, 7), name: "Archiv");
        (await s.Db.Dogs.FirstAsync(d => d.Id == archiviert.Hund)).ArchivedAt = DateTimeOffset.UtcNow;
        await FaehrteAsync(s, Utc(22, 7), besitzer: false, name: "Ohne Zugang");
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Faehrte_VorbeigegangenePause_StehtDerErinnerungNichtImWeg()
    {
        var s = Aufbauen(Utc(22, 10, 30));
        var f = await FaehrteAsync(s, Utc(22, 7));
        s.Db.DogPauses.Add(new DogPause { DogId = f.Hund, Reason = DogPauseReason.Sick, StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 10) });
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Faehrte_SchalterAus_KommtNichts_UndAndereSchalterZaehlenNicht()
    {
        var s = Aufbauen(Utc(22, 10, 30));
        var f = await FaehrteAsync(s, Utc(22, 7));
        s.Db.UserPreferences.Add(new UserPreference { UserId = f.Person, ReminderTracks = false });
        await s.Db.SaveChangesAsync();
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());

        // Nur der Schalter für Fährten zählt - die Termin-Erinnerung aus oder an ändert daran nichts.
        var g = await FaehrteAsync(s, Utc(22, 7));
        s.Db.UserPreferences.Add(new UserPreference { UserId = g.Person, ReminderSessions = false });
        await s.Db.SaveChangesAsync();
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    private static async Task<Guid> SportAsync(Szene s, string code)
    {
        var sport = new Dogity.Domain.Sports.Sport { Code = code, Name = code };
        s.Db.Sports.Add(sport);
        await s.Db.SaveChangesAsync();
        return sport.Id;
    }

    [Fact]
    public async Task Faehrte_SportartenDesMenschen_NurMitFaehrtenSportartKommtEine()
    {
        var s = Aufbauen(Utc(22, 10, 30));
        var faerte = await SportAsync(s, "FAERTE");
        var bh = await SportAsync(s, "BH");

        // Nur BH gewählt: Die App bietet dort keine Fährte an, also erinnert sie auch nicht.
        var ohne = await FaehrteAsync(s, Utc(22, 7), name: "Ohne");
        var ohnePref = new UserPreference { UserId = ohne.Person };
        s.Db.UserPreferences.Add(ohnePref);
        s.Db.UserSportSelections.Add(new UserSportSelection { UserPreferenceId = ohnePref.Id, SportId = bh });
        // BH und Fährte: ja.
        var mit = await FaehrteAsync(s, Utc(22, 7), name: "Mit");
        var mitPref = new UserPreference { UserId = mit.Person };
        s.Db.UserPreferences.Add(mitPref);
        s.Db.UserSportSelections.AddRange(
            new UserSportSelection { UserPreferenceId = mitPref.Id, SportId = bh },
            new UserSportSelection { UserPreferenceId = mitPref.Id, SportId = faerte });
        // Leere Auswahl: keine Einschränkung, ja.
        var leer = await FaehrteAsync(s, Utc(22, 7), name: "Leer");
        await s.Db.SaveChangesAsync();

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        Assert.DoesNotContain(Gesendet(s), g => g.UserId == ohne.Person);
        Assert.Contains(Gesendet(s), g => g.UserId == mit.Person);
        Assert.Contains(Gesendet(s), g => g.UserId == leer.Person);
    }

    [Fact]
    public async Task Faehrte_EigeneSportartenDesHundes_ErsetzenDieDesMenschen()
    {
        var s = Aufbauen(Utc(22, 10, 30));
        var faerte = await SportAsync(s, "FAERTE");
        var bh = await SportAsync(s, "BH");

        // Mensch macht Fährte, der Hund aber nur BH: für diesen Hund gibt es keine Fährte.
        var f = await FaehrteAsync(s, Utc(22, 7));
        var pref = new UserPreference { UserId = f.Person };
        s.Db.UserPreferences.Add(pref);
        s.Db.UserSportSelections.Add(new UserSportSelection { UserPreferenceId = pref.Id, SportId = faerte });
        (await s.Db.Dogs.FirstAsync(d => d.Id == f.Hund)).UsesOwnSports = true;
        s.Db.DogSportSelections.Add(new DogSportSelection { DogId = f.Hund, SportId = bh });
        await s.Db.SaveChangesAsync();
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());

        // Dem Hund die Fährte dazugegeben: jetzt kommt sie.
        s.Db.DogSportSelections.Add(new DogSportSelection { DogId = f.Hund, SportId = faerte });
        await s.Db.SaveChangesAsync();
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Faehrte_ModulFaehrteAbgewaehlt_KommtNichts()
    {
        var s = Aufbauen(Utc(22, 10, 30));
        var f = await FaehrteAsync(s, Utc(22, 7));
        var pref = new UserPreference { UserId = f.Person };
        s.Db.UserPreferences.Add(pref);
        s.Db.UserDisabledModules.Add(new UserDisabledModule { UserPreferenceId = pref.Id, ModuleKey = "faehrte" });
        // Ein anderes abgewähltes Modul ändert nichts.
        var g = await FaehrteAsync(s, Utc(22, 7));
        var prefG = new UserPreference { UserId = g.Person };
        s.Db.UserPreferences.Add(prefG);
        s.Db.UserDisabledModules.Add(new UserDisabledModule { UserPreferenceId = prefG.Id, ModuleKey = "wetter" });
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        Assert.Equal(g.Person, Assert.Single(Gesendet(s)).UserId);
    }

    [Fact]
    public async Task Faehrte_InDerRuhezeitFaellig_KommtAmNaechstenMorgen()
    {
        // Ende 19:00 Uhr (17:00 UTC): Um 22:30 Uhr Ruhezeit, um 08:10 Uhr am nächsten Morgen nach 13 Stunden die Erinnerung.
        var s = Aufbauen(Utc(22, 20, 30));
        await FaehrteAsync(s, Utc(22, 17));
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());

        s.Uhr.Jetzt = Utc(23, 6, 10);
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Faehrte_EnglischeEmpfaengerin_BekommtSieAufEnglischUndOhneMail()
    {
        // Mit dem echten NotificationService: Sprache aus den Einstellungen, Mail aktiv und trotzdem keine Mail.
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var mails = new FakeMailQueue();
        var pushs = new FakePushQueue();
        var echt = new NotificationService(
            db, lookup, new FakeNotificationChannels { EmailAktiv = true, PushPublicKey = "pub-key" },
            TestLinks.Mit(), TestAbmeldeToken.Neu(), mails, pushs, NullLogger<NotificationService>.Instance);
        var uhr = new Uhr(Utc(22, 10, 30));
        var dienst = new ErinnerungService(db, echt, uhr, NullLogger<ErinnerungService>.Instance);
        var s = new Szene(db, new FakeNotificationService(), uhr, dienst);

        var f = await FaehrteAsync(s, Utc(22, 7));
        lookup.Register(f.Person, "en@example.com", "Eve", "Smith");
        db.UserPreferences.Add(new UserPreference { UserId = f.Person, Locale = "en" });
        db.PushSubscriptions.Add(new PushSubscription { UserId = f.Person, Endpoint = "https://fcm.googleapis.com/x", P256dh = "a", Auth = "b" });
        await db.SaveChangesAsync();

        await dienst.VersendeFaelligeAsync();

        var glocke = Assert.Single(db.Notifications);
        Assert.Equal("The track for Bello has not been walked yet. You can still walk it until 09:00 on 23 Oct 2026.", glocke.Message);
        Assert.Single(pushs.Auftraege);
        Assert.Empty(mails.Mails);
    }

    // ---------------------------------------------------------------- Rückmeldungen

    private static async Task<Guid> TrainerinAsync(Szene s, int offene, bool bewertet = false)
    {
        var trainer = Guid.NewGuid();
        var besitzerin = Guid.NewGuid();
        var hund = new Dog { Name = "Anvertraut" };
        s.Db.Dogs.Add(hund);
        s.Db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainer, MemberId = besitzerin, DogId = hund.Id });
        for (var i = 0; i < offene; i++)
        {
            s.Db.TrainingSessions.Add(new TrainingSession
            {
                UserId = besitzerin, DogId = hund.Id, Date = new DateOnly(2026, 10, 20), DurationMinutes = 30,
                TrainerReviewedAt = bewertet ? DateTimeOffset.UtcNow : null,
            });
        }
        await s.Db.SaveChangesAsync();
        return trainer;
    }

    // Sonntag, 25.10.2026, 18:10 Uhr (CET = 17:10 UTC).
    private static readonly DateTimeOffset SonntagAbends = Utc(25, 17, 10);

    [Fact]
    public async Task Rueckmeldungen_Sonntag18Uhr_ZaehltWieDieTrainerUebersicht()
    {
        var s = Aufbauen(SonntagAbends);
        var trainer = await TrainerinAsync(s, offene: 3);

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());

        var meldung = Assert.Single(Gesendet(s));
        Assert.Equal(trainer, meldung.UserId);
        Assert.Equal("3 Trainings warten auf deine Rückmeldung.", meldung.Message);
        Assert.Equal("/trainer/bewerten", meldung.LinkPath);
    }

    [Fact]
    public async Task Rueckmeldungen_EinOffenesTraining_Einzahl()
    {
        var s = Aufbauen(SonntagAbends);
        await TrainerinAsync(s, offene: 1);

        await s.Dienst.VersendeFaelligeAsync();

        Assert.Equal("Ein Training wartet auf deine Rückmeldung.", Assert.Single(Gesendet(s)).Message);
    }

    [Fact]
    public async Task Rueckmeldungen_NullOffen_AbgehaktesZaehltNicht_KommtNichts()
    {
        var s = Aufbauen(SonntagAbends);
        await TrainerinAsync(s, offene: 0);
        await TrainerinAsync(s, offene: 2, bewertet: true);

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        Assert.Empty(await s.Db.SentReminders.ToListAsync());
    }

    [Fact]
    public async Task Rueckmeldungen_NichtGewaehlterTag_KommtNichts()
    {
        var s = Aufbauen(Utc(24, 17, 10)); // Samstag
        await TrainerinAsync(s, offene: 2);

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Rueckmeldungen_EigenerRhythmus_MehrereTage()
    {
        // Montag 09:10 Uhr (CET) und Mittwoch 09:10 Uhr; Dienstag nicht.
        var s = Aufbauen(Utc(26, 8, 10));
        var trainer = await TrainerinAsync(s, offene: 2);
        s.Db.UserPreferences.Add(new UserPreference { UserId = trainer, TrainerSummaryDays = 1 | 4, TrainerSummaryHour = 9 });
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        s.Uhr.Jetzt = Utc(27, 8, 10);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        s.Uhr.Jetzt = Utc(28, 8, 10);
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        // Und am Mittwoch nicht zweimal.
        s.Uhr.Jetzt = Utc(28, 8, 20);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());

        Assert.Equal(2, Gesendet(s).Count);
    }

    [Fact]
    public async Task Rueckmeldungen_UhrzeitNachDemVersandGeaendert_KeinZweiterVersandAmSelbenTag()
    {
        var s = Aufbauen(SonntagAbends);
        var trainer = await TrainerinAsync(s, offene: 2);
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync()); // 18 Uhr (Vorgabe)

        s.Db.UserPreferences.Add(new UserPreference { UserId = trainer, TrainerSummaryHour = 19 });
        await s.Db.SaveChangesAsync();
        s.Uhr.Jetzt = Utc(25, 18, 10); // 19:10 Uhr

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        // Nächsten Sonntag (neuer Tag, neuer Schlüssel) wieder.
        s.Uhr.Jetzt = Utc(25, 18, 10).AddDays(7);
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Rueckmeldungen_SchalterAus_KommtNichts()
    {
        var s = Aufbauen(SonntagAbends);
        var trainer = await TrainerinAsync(s, offene: 2);
        s.Db.UserPreferences.Add(new UserPreference { UserId = trainer, ReminderTrainerSummary = false });
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    // ---------------------------------------------------------------- Aufräumen

    [Fact]
    public async Task Aufraeumen_EntferntEintraegeNachSechzigTagen_AuchInDerRuhezeit()
    {
        var s = Aufbauen(Utc(22, 22, 30)); // 00:30 Uhr - Ruhezeit
        s.Db.SentReminders.AddRange(
            new SentReminder { UserId = Guid.NewGuid(), Type = ReminderType.TrackNotWalked, ReferenceId = Guid.NewGuid(), CreatedAt = Utc(22, 12).AddDays(-61) },
            new SentReminder { UserId = Guid.NewGuid(), Type = ReminderType.TrackNotWalked, ReferenceId = Guid.NewGuid(), CreatedAt = Utc(22, 12).AddDays(-59) });
        await s.Db.SaveChangesAsync();

        await s.Dienst.VersendeFaelligeAsync();

        Assert.Equal(1, await s.Db.SentReminders.CountAsync());
    }

    // ---------------------------------------------------------------- Fehlerverhalten

    [Fact]
    public async Task EinFehlerImVersand_BricktDenLaufNichtAb_ErscheintAberNicht()
    {
        // Der Versand selbst wirft: Der Dienst fängt das je Abschnitt ab (und loggt), der Lauf endet normal.
        var db = InMemoryDbContext.Create();
        var uhr = new Uhr(Utc(22, 16, 30));
        var dienst = new ErinnerungService(db, new WirftNotificationService(), uhr, NullLogger<ErinnerungService>.Instance);
        var s = new Szene(db, new FakeNotificationService(), uhr, dienst);
        var (gruppe, _) = await TerminAsync(s, Utc(23, 16));
        await MitgliedAsync(s, gruppe);

        var anzahl = await dienst.VersendeFaelligeAsync();

        Assert.Equal(0, anzahl);
    }

    private sealed class WirftNotificationService : INotificationService
    {
        public Task CreateAsync(Guid userId, Dogity.Application.Common.Meldung meldung, string? linkPath = null, CancellationToken ct = default, bool wichtig = false) =>
            throw new InvalidOperationException("Datenbank nicht erreichbar");
        public Task<bool> HatUngelesenAsync(Guid userId, Dogity.Application.Common.Meldung meldung, string linkPath, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Dogity.Application.Common.Result<IReadOnlyList<NotificationDto>>> GetMyNotificationsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dogity.Application.Common.Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dogity.Application.Common.Result> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dogity.Application.Common.Result> MarkAllReadAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
