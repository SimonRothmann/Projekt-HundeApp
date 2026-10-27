using Dogity.Application.Common;
using Dogity.Application.Preferences;
using Dogity.Application.Reminders;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Reminders;

/// <summary>
/// Die Erinnerungen an Gesundheitseinträge: Fristen (Impfung gültig bis, Entwurmung nächste fällig) und der
/// Tierarzttermin am Vortag. Die Regeln (wann, welcher Text) ohne Datenbank, der Versand (wer, Merkzettel)
/// gegen eine Datenbank. Die Uhrzeiten im Test sind UTC; im November gilt in Deutschland UTC+1.
/// </summary>
public class GesundheitsErinnerungenTests
{
    private static readonly IReadOnlySet<ErinnerungsSchluessel> NichtsGesendet = new HashSet<ErinnerungsSchluessel>();

    private static DateTimeOffset Utc(int tag, int stunde, int minute = 0, int monat = 11) =>
        new(2026, monat, tag, stunde, minute, 0, TimeSpan.Zero);

    private static string De(FaelligeErinnerung e) => Uebersetzung.Rendere(e.Meldung, null);
    private static string En(FaelligeErinnerung e) => Uebersetzung.Rendere(e.Meldung, "en");

    // ---------------------------------------------------------------- Regeln

    private static GesundheitsAdressat Frist(
        DateOnly? faellig, DogHealthKind art = DogHealthKind.Vaccination, string titel = "Tollwut", bool aktuell = true,
        bool erlaubt = true, Guid? user = null, Guid? eintrag = null, Guid? hund = null) =>
        new(eintrag ?? Guid.NewGuid(), hund ?? Guid.NewGuid(), "Bello", art, titel, new DateOnly(2026, 1, 1), null,
            faellig, aktuell, user ?? Guid.NewGuid(), erlaubt);

    private static GesundheitsAdressat Termin(DateOnly datum, TimeOnly? uhrzeit = null, bool erlaubt = true, Guid? user = null, Guid? eintrag = null) =>
        new(eintrag ?? Guid.NewGuid(), Guid.NewGuid(), "Bello", DogHealthKind.VetAppointment, "Impfung", datum, uhrzeit,
            null, true, user ?? Guid.NewGuid(), erlaubt);

    // Mittwoch, 4.11.2026, 10:00 Uhr deutscher Zeit.
    private static readonly DateTimeOffset Vormittag = Utc(4, 9);
    private static readonly DateOnly Heute = new(2026, 11, 4);

    [Fact]
    public void Frist_VierzehnTageVorher_KommtEineErinnerung()
    {
        var a = Frist(Heute.AddDays(14));

        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [a], NichtsGesendet));

        Assert.Equal("Bello: Die Impfung Tollwut ist bis 18.11.2026 gültig.", De(e));
        Assert.Equal("Bello: The Tollwut vaccination is valid until 18 Nov 2026.", En(e));
        Assert.Equal(new ErinnerungsSchluessel(ReminderType.HealthDueAhead, a.UserId, a.EintragId, Heute.AddDays(14)), e.Schluessel);
        Assert.Equal($"/dogs/{a.HundId}?tab=gesundheit", e.Link);
    }

    [Fact]
    public void Frist_FuenfzehnTageVorher_NochNicht()
    {
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [Frist(Heute.AddDays(15))], NichtsGesendet));
    }

    [Fact]
    public void Frist_SpaetAngelegt_ZehnTageVorher_KommtTrotzdemEinmal()
    {
        // Wer den Eintrag erst zehn Tage vor Ablauf anlegt, soll nicht leer ausgehen.
        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [Frist(Heute.AddDays(10))], NichtsGesendet));

        Assert.Equal(ReminderType.HealthDueAhead, e.Schluessel.Typ);
    }

    [Fact]
    public void Frist_AmTagSelbst_KommtNurDieZweiteStufe()
    {
        var a = Frist(Heute);

        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [a], NichtsGesendet));

        Assert.Equal(ReminderType.HealthDueToday, e.Schluessel.Typ);
        Assert.Equal("Bello: Die Impfung Tollwut läuft heute ab.", De(e));
        Assert.Equal("Bello: The Tollwut vaccination expires today.", En(e));
    }

    [Fact]
    public void Frist_AmTagSelbst_AuchWennDieVorabStufeGesendetWurde_KommtDieZweite_UndNichtDieErste()
    {
        var a = Frist(Heute);
        var vorabGesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.HealthDueAhead, a.UserId, a.EintragId, Heute) };

        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [a], vorabGesendet));

        Assert.Equal(ReminderType.HealthDueToday, e.Schluessel.Typ);
    }

    [Fact]
    public void Frist_ZweitesMalAmTagSelbst_KommtNichtNochmal()
    {
        var a = Frist(Heute);
        var gesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.HealthDueToday, a.UserId, a.EintragId, Heute) };

        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [a], gesendet));
    }

    [Fact]
    public void Frist_VorabSchonGesendet_KommtNichtNochmal()
    {
        var a = Frist(Heute.AddDays(7));
        var gesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.HealthDueAhead, a.UserId, a.EintragId, Heute.AddDays(7)) };

        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [a], gesendet));
    }

    [Fact]
    public void Frist_NachtraeglichVerschoben_ErinnertZumNeuenDatumErneut()
    {
        var a = Frist(Heute.AddDays(5));
        // Gesendet wurde für den 20.11., das Datum steht jetzt auf dem 9.11.
        var gesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.HealthDueAhead, a.UserId, a.EintragId, Heute.AddDays(16)) };

        Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [a], gesendet));
    }

    [Fact]
    public void Frist_Abgelaufen_KommtNichtsMehr_OhneFristNichts()
    {
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [Frist(Heute.AddDays(-1)), Frist(null)], NichtsGesendet));
    }

    [Fact]
    public void Frist_NichtDerJuengste_ErinnertNicht_SchalterAus_ErinnertNicht()
    {
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Vormittag,
            [Frist(Heute.AddDays(3), aktuell: false), Frist(Heute.AddDays(3), erlaubt: false)], NichtsGesendet));
    }

    [Fact]
    public void Frist_JeBesitzerinEineErinnerung_ZweiEintraegeZweiErinnerungen()
    {
        var eintrag = Guid.NewGuid();
        var a = Frist(Heute.AddDays(3), eintrag: eintrag);
        var b = Frist(Heute.AddDays(3), eintrag: eintrag);
        var andererEintrag = Frist(Heute.AddDays(3), DogHealthKind.Deworming, "Entwurmung");

        Assert.Equal(3, Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [a, b, andererEintrag], NichtsGesendet).Count);
    }

    [Fact]
    public void Frist_EntwurmungUndSonstiges_HeissenFaellig()
    {
        var bald = Frist(Heute.AddDays(3), DogHealthKind.Deworming, "Entwurmung");
        var heute = Frist(Heute, DogHealthKind.ParasiteProtection, "Zeckenschutz");

        var erg = Erinnerungsregeln.GesundheitsErinnerungen(Vormittag, [bald, heute], NichtsGesendet);

        Assert.Equal("Bello: Entwurmung ist am 07.11.2026 fällig.", De(erg[0]));
        Assert.Equal("Bello: Entwurmung is due on 7 Nov 2026.", En(erg[0]));
        Assert.Equal("Bello: Zeckenschutz ist heute fällig.", De(erg[1]));
        Assert.Equal("Bello: Zeckenschutz is due today.", En(erg[1]));
    }

    [Theory]
    [InlineData(6, 59, false)]  // 07:59 Uhr
    [InlineData(7, 0, true)]    // 08:00 Uhr
    [InlineData(20, 59, true)]  // 21:59 Uhr
    [InlineData(21, 0, false)]  // 22:00 Uhr
    public void Frist_NurImVersandfenster(int stunde, int minute, bool erwartet)
    {
        var erg = Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, stunde, minute), [Frist(Heute.AddDays(3))], NichtsGesendet);

        Assert.Equal(erwartet, erg.Count == 1);
    }

    [Fact]
    public void Frist_NachMitternachtInVereinszeit_ZaehltDerNeueTag()
    {
        var a = Frist(new DateOnly(2026, 11, 5));
        // 23:30 UTC am 3.11. sind 00:30 Uhr am 4.11. - Ruhezeit, nichts kommt.
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Utc(3, 23, 30), [a], NichtsGesendet));
        // Um 08:30 Uhr am 5.11. ist die Frist heute: die zweite Stufe.
        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Utc(5, 7, 30), [a], NichtsGesendet));
        Assert.Equal(ReminderType.HealthDueToday, e.Schluessel.Typ);
    }

    [Fact]
    public void Frist_UeberDieZeitumstellung_RechnetMitKalendertagen()
    {
        // Sonntag, 25.10.2026, 20:30 Uhr (Winterzeit seit 03:00): Die Frist am 26.10. ist morgen - noch Stufe eins.
        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Utc(25, 19, 30, 10), [Frist(new DateOnly(2026, 10, 26))], NichtsGesendet));

        Assert.Equal(ReminderType.HealthDueAhead, e.Schluessel.Typ);
    }

    // ---- Tierarzttermin

    [Fact]
    public void Termin_AmVortagAb18Uhr_MitUhrzeit()
    {
        var a = Termin(Heute.AddDays(1), new TimeOnly(10, 30));

        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 17), [a], NichtsGesendet)); // 18:00 Uhr

        Assert.Equal("Morgen 10:30: Tierarzt mit Bello (Impfung).", De(e));
        Assert.Equal("Tomorrow 10:30: vet appointment with Bello (Impfung).", En(e));
        Assert.Equal(new ErinnerungsSchluessel(ReminderType.HealthVetDayBefore, a.UserId, a.EintragId, Heute.AddDays(1)), e.Schluessel);
        Assert.Equal($"/dogs/{a.HundId}?tab=gesundheit", e.Link);
    }

    [Fact]
    public void Termin_OhneUhrzeit_HatKeineZeitImSatz()
    {
        var e = Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 17), [Termin(Heute.AddDays(1))], NichtsGesendet));

        Assert.Equal("Morgen: Tierarzt mit Bello (Impfung).", De(e));
        Assert.Equal("Tomorrow: vet appointment with Bello (Impfung).", En(e));
    }

    [Fact]
    public void Termin_VorAchtzehnUhr_Heute_Uebermorgen_Abgelaufen_KommtNichts()
    {
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 16, 59), [Termin(Heute.AddDays(1))], NichtsGesendet)); // 17:59 Uhr
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 17), [Termin(Heute)], NichtsGesendet));
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 17), [Termin(Heute.AddDays(2))], NichtsGesendet));
    }

    [Fact]
    public void Termin_ZweimalNichtUndNachVerschiebenErneut()
    {
        var a = Termin(Heute.AddDays(1));
        var gesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.HealthVetDayBefore, a.UserId, a.EintragId, Heute.AddDays(1)) };
        var verschoben = new HashSet<ErinnerungsSchluessel> { new(ReminderType.HealthVetDayBefore, a.UserId, a.EintragId, Heute.AddDays(3)) };

        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 17), [a], gesendet));
        Assert.Single(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 17), [a], verschoben));
    }

    [Fact]
    public void Termin_SchalterAus_KommtNichts() =>
        Assert.Empty(Erinnerungsregeln.GesundheitsErinnerungen(Utc(4, 17), [Termin(Heute.AddDays(1), erlaubt: false)], NichtsGesendet));

    // ---------------------------------------------------------------- Versand gegen die Datenbank

    private sealed class Uhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Szene(
        ApplicationDbContext Db, FakeNotificationService Meldungen, Uhr Uhr, ErinnerungService Dienst,
        Dog Hund, Guid Besitzerin, Guid Mitbesitzer);

    // Mittwoch, 4.11.2026, 10:30 Uhr deutscher Zeit.
    private static Szene Aufbauen(DateTimeOffset? jetzt = null)
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var uhr = new Uhr(jetzt ?? Utc(4, 9, 30));
        var hund = new Dog { Name = "Bello", Gender = DogGender.Male };
        var besitzerin = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzerin, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = mitbesitzer, Role = DogOwnerRole.Owner });
        db.SaveChanges();
        return new Szene(db, meldungen, uhr, new ErinnerungService(db, meldungen, uhr, NullLogger<ErinnerungService>.Instance), hund, besitzerin, mitbesitzer);
    }

    private static async Task<DogHealthRecord> EintragAsync(
        Szene s, DateOnly? faellig, DogHealthKind art = DogHealthKind.Vaccination, string titel = "Tollwut",
        DateOnly? datum = null, TimeOnly? uhrzeit = null, Dog? hund = null)
    {
        var eintrag = new DogHealthRecord
        {
            DogId = (hund ?? s.Hund).Id, Kind = art, Title = titel, Date = datum ?? Heute.AddYears(-1), Time = uhrzeit, DueDate = faellig,
        };
        s.Db.DogHealthRecords.Add(eintrag);
        await s.Db.SaveChangesAsync();
        return eintrag;
    }

    [Fact]
    public async Task Versand_VierzehnTageVorher_AnBesitzerinUndMitbesitzer_GlockeUndPushKeineMail()
    {
        var s = Aufbauen();
        var eintrag = await EintragAsync(s, Heute.AddDays(14));

        var anzahl = await s.Dienst.VersendeFaelligeAsync();

        Assert.Equal(2, anzahl);
        Assert.Equal(["Bello: Die Impfung Tollwut ist bis 18.11.2026 gültig."], s.Meldungen.Created.Where(c => c.UserId == s.Besitzerin).Select(c => c.Message));
        Assert.Contains(s.Meldungen.Created, c => c.UserId == s.Mitbesitzer);
        Assert.All(s.Meldungen.Created, c => Assert.Equal($"/dogs/{s.Hund.Id}?tab=gesundheit", c.LinkPath));
        Assert.Empty(s.Meldungen.Wichtige);
        var zeile = await s.Db.SentReminders.FirstAsync();
        Assert.Equal((ReminderType.HealthDueAhead, eintrag.Id, Heute.AddDays(14)), (zeile.Type, zeile.ReferenceId, zeile.DayKey));
    }

    [Fact]
    public async Task Versand_ZweiterLaufUndNeustart_SchickenNichtsNochmal()
    {
        var s = Aufbauen();
        await EintragAsync(s, Heute.AddDays(14));

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        s.Uhr.Jetzt = Utc(4, 9, 40);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        var neu = new ErinnerungService(s.Db, s.Meldungen, s.Uhr, NullLogger<ErinnerungService>.Instance);
        Assert.Equal(0, await neu.VersendeFaelligeAsync());

        Assert.Equal(2, s.Meldungen.Created.Count);
        Assert.Equal(2, await s.Db.SentReminders.CountAsync());
    }

    [Fact]
    public async Task Versand_VorabUndAmTagSelbst_JeEinmal()
    {
        var s = Aufbauen();
        await EintragAsync(s, Heute.AddDays(14));

        await s.Dienst.VersendeFaelligeAsync();
        s.Uhr.Jetzt = Utc(18, 9, 30); // am Tag der Frist, 10:30 Uhr
        await s.Dienst.VersendeFaelligeAsync();
        s.Uhr.Jetzt = Utc(18, 12);
        await s.Dienst.VersendeFaelligeAsync();

        var besitzerin = s.Meldungen.Created.Where(c => c.UserId == s.Besitzerin).Select(c => c.Message).ToList();
        Assert.Equal(["Bello: Die Impfung Tollwut ist bis 18.11.2026 gültig.", "Bello: Die Impfung Tollwut läuft heute ab."], besitzerin);
    }

    [Fact]
    public async Task Versand_ErneuerteImpfung_BeendetDieErinnerungDerAlten()
    {
        var s = Aufbauen();
        await EintragAsync(s, Heute.AddDays(5), datum: Heute.AddYears(-1));
        // Eine neue Tollwut-Impfung mit weit entfernter Frist: Die alte erinnert nicht mehr.
        await EintragAsync(s, Heute.AddYears(3), datum: Heute.AddDays(-2));

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        Assert.Empty(s.Meldungen.Created);
    }

    [Fact]
    public async Task Versand_ErneuerteImpfung_AnderesPraeparat_ErinnertWeiter()
    {
        var s = Aufbauen();
        await EintragAsync(s, Heute.AddDays(5), titel: "SHPPi", datum: Heute.AddYears(-1));
        await EintragAsync(s, Heute.AddYears(3), titel: "Tollwut", datum: Heute.AddDays(-2));

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        Assert.All(s.Meldungen.Created, c => Assert.Contains("SHPPi", c.Message));
    }

    [Fact]
    public async Task Versand_SchalterAus_KeineErinnerung_ObneZeileGiltAn()
    {
        var s = Aufbauen();
        await EintragAsync(s, Heute.AddDays(14));
        s.Db.UserPreferences.Add(new UserPreference { UserId = s.Besitzerin, ReminderHealth = false });
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        Assert.DoesNotContain(s.Meldungen.Created, c => c.UserId == s.Besitzerin);
        Assert.Contains(s.Meldungen.Created, c => c.UserId == s.Mitbesitzer);
    }

    [Fact]
    public async Task Versand_AndereErinnerungenAus_ZaehlenHierNicht()
    {
        var s = Aufbauen();
        await EintragAsync(s, Heute.AddDays(14));
        s.Db.UserPreferences.Add(new UserPreference { UserId = s.Besitzerin, ReminderSessions = false, ReminderTracks = false, ReminderTrainerSummary = false });
        await s.Db.SaveChangesAsync();

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_ModulGesundheitAbgeschaltet_KeineErinnerung()
    {
        var s = Aufbauen();
        await EintragAsync(s, Heute.AddDays(14));
        var einstellung = new UserPreference { UserId = s.Besitzerin };
        s.Db.UserPreferences.Add(einstellung);
        s.Db.UserDisabledModules.Add(new UserDisabledModule { UserPreferenceId = einstellung.Id, ModuleKey = Modules.Gesundheit });
        // Ein anderes abgeschaltetes Modul ändert nichts.
        var andere = new UserPreference { UserId = s.Mitbesitzer };
        s.Db.UserPreferences.Add(andere);
        s.Db.UserDisabledModules.Add(new UserDisabledModule { UserPreferenceId = andere.Id, ModuleKey = Modules.Wetter });
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        Assert.DoesNotContain(s.Meldungen.Created, c => c.UserId == s.Besitzerin);
    }

    [Fact]
    public async Task Versand_GeloeschterEintrag_GeloeschterHund_ArchivierterHund_ErinnernNicht()
    {
        var s = Aufbauen();
        var geloescht = await EintragAsync(s, Heute.AddDays(14));
        geloescht.DeletedAt = DateTimeOffset.UtcNow;
        var weg = new Dog { Name = "Weg" };
        var archiviert = new Dog { Name = "Archiv", ArchivedAt = DateTimeOffset.UtcNow };
        s.Db.Dogs.AddRange(weg, archiviert);
        s.Db.DogOwners.AddRange(
            new DogOwner { DogId = weg.Id, UserId = s.Besitzerin, Role = DogOwnerRole.Owner },
            new DogOwner { DogId = archiviert.Id, UserId = s.Besitzerin, Role = DogOwnerRole.Owner });
        await s.Db.SaveChangesAsync();
        await EintragAsync(s, Heute.AddDays(3), hund: weg);
        await EintragAsync(s, Heute.AddDays(3), hund: archiviert);
        weg.DeletedAt = DateTimeOffset.UtcNow;
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        Assert.Empty(s.Meldungen.Created);
    }

    [Fact]
    public async Task Versand_NurBesitzer_NichtTrainerinNichtFremdeNichtEingeladene()
    {
        var s = Aufbauen();
        var trainerin = Guid.NewGuid();
        var eingeladen = Guid.NewGuid();
        s.Db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerin, MemberId = s.Besitzerin, DogId = s.Hund.Id, StartDate = Heute });
        s.Db.DogOwners.Add(new DogOwner { DogId = s.Hund.Id, UserId = eingeladen, Role = DogOwnerRole.Owner, Status = DogOwnerStatus.Invited });
        await s.Db.SaveChangesAsync();
        await EintragAsync(s, Heute.AddDays(14));

        await s.Dienst.VersendeFaelligeAsync();

        Assert.Equal(new[] { s.Besitzerin, s.Mitbesitzer }.Order(), s.Meldungen.Created.Select(c => c.UserId).Order());
    }

    [Fact]
    public async Task Versand_NachtraeglichVerschobenesDatum_ErinnertErneut()
    {
        var s = Aufbauen();
        var eintrag = await EintragAsync(s, Heute.AddDays(14));
        await s.Dienst.VersendeFaelligeAsync();

        eintrag.DueDate = Heute.AddDays(6);
        await s.Db.SaveChangesAsync();
        s.Uhr.Jetzt = Utc(4, 10);

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        Assert.Equal(4, s.Meldungen.Created.Count);
    }

    [Fact]
    public async Task Versand_AusserhalbDesFensters_NichtsGeladenNichtsGesendet()
    {
        var s = Aufbauen(Utc(4, 21, 30)); // 22:30 Uhr
        await EintragAsync(s, Heute.AddDays(14));

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        Assert.Empty(s.Meldungen.Created);
    }

    [Fact]
    public async Task Versand_Tierarzttermin_AmVortagAbAchtzehnUhr_AnAlleBesitzer()
    {
        var s = Aufbauen(Utc(4, 16, 59)); // 17:59 Uhr
        await EintragAsync(s, null, DogHealthKind.VetAppointment, "Impfung", datum: Heute.AddDays(1), uhrzeit: new TimeOnly(10, 30));

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());

        s.Uhr.Jetzt = Utc(4, 17, 0); // 18:00 Uhr
        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        Assert.All(s.Meldungen.Created, c => Assert.Equal("Morgen 10:30: Tierarzt mit Bello (Impfung).", c.Message));
        Assert.Equal(2, await s.Db.SentReminders.CountAsync(r => r.Type == ReminderType.HealthVetDayBefore));

        s.Uhr.Jetzt = Utc(4, 18, 0);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_Tierarzttermin_GeloeschtOderAndererTag_KommtNichts()
    {
        var s = Aufbauen(Utc(4, 17));
        var geloescht = await EintragAsync(s, null, DogHealthKind.VetAppointment, "Weg", datum: Heute.AddDays(1));
        geloescht.DeletedAt = DateTimeOffset.UtcNow;
        await EintragAsync(s, null, DogHealthKind.VetAppointment, "Übermorgen", datum: Heute.AddDays(2));
        await EintragAsync(s, null, DogHealthKind.VetAppointment, "Heute", datum: Heute);
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_TierarzttermineSindKeineFrist_UndFristenKeineTermine()
    {
        var s = Aufbauen(Utc(4, 17));
        // Ein Termin in zehn Tagen hat keine Frist; ein Eintrag mit Frist morgen ist keine Terminmeldung.
        await EintragAsync(s, null, DogHealthKind.VetAppointment, "Später", datum: Heute.AddDays(10));
        await EintragAsync(s, Heute.AddDays(1), DogHealthKind.Deworming, "Entwurmung", datum: Heute.AddDays(-80));

        await s.Dienst.VersendeFaelligeAsync();

        Assert.All(s.Meldungen.Created, c => Assert.Equal("Bello: Entwurmung ist am 05.11.2026 fällig.", c.Message));
        Assert.Equal(2, s.Meldungen.Created.Count);
    }
}
