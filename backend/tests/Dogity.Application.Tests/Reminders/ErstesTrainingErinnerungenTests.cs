using Dogity.Application.Common;
using Dogity.Application.Reminders;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Reminders;

/// <summary>
/// Der einmalige Anstoß zum ersten Training: ab dem dritten Kalendertag nach dem ersten Hund, ab 18 Uhr Ortszeit,
/// höchstens bis zum 30. Tag, nur einmal je Konto. Die Regeln ohne Datenbank, der Versand (Training, Pause, Gast,
/// Hund weg) gegen eine Datenbank. Im November gilt in Deutschland UTC+1, in New York UTC-5.
/// </summary>
public class ErstesTrainingErinnerungenTests
{
    private static readonly IReadOnlySet<ErinnerungsSchluessel> NichtsGesendet = new HashSet<ErinnerungsSchluessel>();

    private static DateTimeOffset Utc(int tag, int stunde, int minute = 0, int monat = 11) =>
        new(2026, monat, tag, stunde, minute, 0, TimeSpan.Zero);

    private static string De(FaelligeErinnerung e) => Uebersetzung.Rendere(e.Meldung, null);

    // Erster Hund: Dienstag, 10.11.2026, 13:00 Uhr deutscher Zeit. Tag 3 ist der 13.11.
    private static readonly DateTimeOffset ErsterHund = Utc(10, 12);

    private static ErstesTrainingKandidat Kandidat(bool pausiert = false, Guid? user = null) =>
        new(user ?? Guid.NewGuid(), Guid.NewGuid(), "Bello", ErsterHund, pausiert);

    private static IReadOnlyList<FaelligeErinnerung> Fuer(DateTimeOffset jetzt, params ErstesTrainingKandidat[] k) =>
        Erinnerungsregeln.ErstesTrainingErinnerungen(jetzt, k, NichtsGesendet);

    // ---------------------------------------------------------------- Regeln

    [Fact]
    public void AbTagDreiUndAchtzehnUhr_KommtDerAnstoss_MitHundImText()
    {
        var k = Kandidat();

        var e = Assert.Single(Fuer(Utc(13, 17), k)); // 18:00 Uhr am 13.11.

        Assert.Equal("Zeit fürs erste Training mit Bello? Eintragen dauert nur ein paar Sekunden – die Übungen sind schon vorgeschlagen.", De(e));
        Assert.StartsWith("Time for your first training with Bello?", Uebersetzung.Rendere(e.Meldung, "en"));
        Assert.Equal(new ErinnerungsSchluessel(ReminderType.FirstTraining, k.UserId, Guid.Empty, SentReminder.KeinTag), e.Schluessel);
        Assert.Equal("/dashboard", e.Link);
    }

    [Fact]
    public void VorAchtzehnUhr_OderVorTagDrei_KommtNichts()
    {
        var k = Kandidat();

        Assert.Empty(Fuer(Utc(13, 16, 59), k)); // 17:59 Uhr
        Assert.Empty(Fuer(Utc(12, 17), k));     // erst Tag 2
    }

    [Fact]
    public void InDerRuhezeit_KommtNichts_AmNaechstenAbendSchon()
    {
        var k = Kandidat();

        Assert.Empty(Fuer(Utc(13, 21), k));       // 22:00 Uhr: Ruhezeit
        Assert.Single(Fuer(Utc(13, 20, 59), k));  // 21:59 Uhr: noch im Fenster
        Assert.Single(Fuer(Utc(14, 17), k));      // am Folgetag, falls der erste Abend verpasst wurde
    }

    [Fact]
    public void OrtszeitDerPerson_NewYorkUndBerlin_WerdenGetrenntBeurteilt()
    {
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var k = Kandidat();

        // 23:00 UTC am 13.11. = 18:00 Uhr in New York, aber 00:00 Uhr in Berlin.
        Assert.Empty(Fuer(Utc(13, 23), k));
        Assert.Single(Erinnerungsregeln.ErstesTrainingErinnerungen(Utc(13, 23), [k], NichtsGesendet, _ => ny));
        // 17:00 UTC = 18:00 Uhr in Berlin, aber 12:00 Uhr in New York.
        Assert.Empty(Erinnerungsregeln.ErstesTrainingErinnerungen(Utc(13, 17), [k], NichtsGesendet, _ => ny));
    }

    [Fact]
    public void ErsterHundAelterAlsDreissigTage_KommtNichts_DreissigTageNochJa()
    {
        // 10.11. + 30 Tage = 10.12.
        Assert.Single(Fuer(Utc(10, 17, monat: 12), Kandidat()));
        Assert.Empty(Fuer(Utc(11, 17, monat: 12), Kandidat()));
    }

    [Fact]
    public void Trainingspause_Oder_SchonGesendet_KommtNichts()
    {
        var k = Kandidat();
        var gesendet = new HashSet<ErinnerungsSchluessel> { new(ReminderType.FirstTraining, k.UserId, Guid.Empty, SentReminder.KeinTag) };

        Assert.Empty(Fuer(Utc(13, 17), Kandidat(pausiert: true)));
        Assert.Empty(Erinnerungsregeln.ErstesTrainingErinnerungen(Utc(13, 17), [k], gesendet));
    }

    // ---------------------------------------------------------------- Versand gegen die Datenbank

    private sealed class Uhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Szene(
        ApplicationDbContext Db, FakeNotificationService Meldungen, FakeUserLookupService Nutzer, Uhr Uhr, ErinnerungService Dienst, Dog Hund, Guid Besitzerin);

    /// <summary>Hund Bello, Besitzerin seit dem 10.11. 13:00 Uhr, kein Training; Freitag, 13.11., 18:05 Uhr.</summary>
    private static async Task<Szene> AufbauenAsync(DateTimeOffset? erster = null)
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var nutzer = new FakeUserLookupService();
        var uhr = new Uhr(Utc(13, 17, 5));
        var hund = new Dog { Name = "Bello", Gender = DogGender.Male };
        var besitzerin = Guid.NewGuid();
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzerin, Role = DogOwnerRole.Owner, CreatedAt = erster ?? ErsterHund });
        await db.SaveChangesAsync();
        return new Szene(db, meldungen, nutzer, uhr, new ErinnerungService(db, meldungen, uhr, nutzer, NullLogger<ErinnerungService>.Instance), hund, besitzerin);
    }

    private static async Task TrainingAsync(Szene s, Guid userId)
    {
        s.Db.TrainingSessions.Add(new Domain.Training.TrainingSession { UserId = userId, DogId = s.Hund.Id, Date = new DateOnly(2026, 11, 11), DurationMinutes = 20 });
        await s.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Versand_EineMeldungInDieGlocke_MitLink_KeineMail_UndNurEinmal()
    {
        var s = await AufbauenAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());

        var c = Assert.Single(s.Meldungen.Created);
        Assert.Equal(s.Besitzerin, c.UserId);
        Assert.Equal("Zeit fürs erste Training mit Bello? Eintragen dauert nur ein paar Sekunden – die Übungen sind schon vorgeschlagen.", c.Message);
        Assert.Equal("/dashboard", c.LinkPath);
        Assert.Empty(s.Meldungen.Wichtige);

        s.Uhr.Jetzt = Utc(14, 17);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        Assert.Equal(1, await s.Db.SentReminders.CountAsync(r => r.Type == ReminderType.FirstTraining));
    }

    [Fact]
    public async Task Versand_NewYork_ErstUmAchtzehnUhrDort()
    {
        var s = await AufbauenAsync();
        s.Db.UserPreferences.Add(new UserPreference { UserId = s.Besitzerin, TimeZone = "America/New_York" });
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync()); // 12:05 Uhr in New York
        s.Uhr.Jetzt = Utc(13, 23, 5);
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_EigenesTrainingOderDasDerMitbesitzerin_VerhindertDenAnstoss()
    {
        var eigenes = await AufbauenAsync();
        await TrainingAsync(eigenes, eigenes.Besitzerin);
        Assert.Equal(0, await eigenes.Dienst.VersendeFaelligeAsync());

        var mit = await AufbauenAsync();
        var andere = Guid.NewGuid();
        mit.Db.DogOwners.Add(new DogOwner { DogId = mit.Hund.Id, UserId = andere, CreatedAt = Utc(1, 10) });
        await mit.Db.SaveChangesAsync();
        await TrainingAsync(mit, andere);
        Assert.Equal(0, await mit.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_GeloeschtesTraining_ZaehltNicht()
    {
        var s = await AufbauenAsync();
        await TrainingAsync(s, s.Besitzerin);
        var t = await s.Db.TrainingSessions.SingleAsync();
        t.DeletedAt = Utc(12, 10);
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_Trainingspause_KeinAnstoss_AmTagDanachSchon()
    {
        var s = await AufbauenAsync();
        s.Db.DogPauses.Add(new DogPause { DogId = s.Hund.Id, Reason = DogPauseReason.Sick, StartDate = new DateOnly(2026, 11, 13), EndDate = new DateOnly(2026, 11, 13) });
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        s.Uhr.Jetzt = Utc(14, 17, 5);
        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_Gastkonto_NieEinAnstoss()
    {
        var s = await AufbauenAsync();
        s.Nutzer.Gaeste.Add(s.Besitzerin);

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_NurOffeneEinladungZumMitbesitz_IstKeinHund()
    {
        var s = await AufbauenAsync();
        var eingeladene = Guid.NewGuid();
        s.Db.DogOwners.Add(new DogOwner { DogId = s.Hund.Id, UserId = eingeladene, Status = DogOwnerStatus.Invited, CreatedAt = ErsterHund });
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        Assert.DoesNotContain(s.Meldungen.Created, m => m.UserId == eingeladene);
        Assert.DoesNotContain(s.Meldungen.Vorlagen, m => m.UserId == eingeladene);
    }

    [Fact]
    public async Task Versand_ErsterHundVorVierzigTagen_KeinAnstoss()
    {
        var s = await AufbauenAsync(erster: Utc(1, 12, monat: 10));

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_FruehererBesitzMitAelterer_Zeile_IstKeinErstkontakt()
    {
        var s = await AufbauenAsync();
        var alterHund = new Dog { Name = "Rex", Gender = DogGender.Male };
        s.Db.Dogs.Add(alterHund);
        s.Db.DogOwners.Add(new DogOwner { DogId = alterHund.Id, UserId = s.Besitzerin, CreatedAt = Utc(1, 12, monat: 8) });
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_ArchivierterOderGeloeschterHund_ZaehltNicht()
    {
        var archiviert = await AufbauenAsync();
        archiviert.Hund.ArchivedAt = Utc(12, 10);
        await archiviert.Db.SaveChangesAsync();
        Assert.Equal(0, await archiviert.Dienst.VersendeFaelligeAsync());

        var geloescht = await AufbauenAsync();
        geloescht.Hund.DeletedAt = Utc(12, 10);
        await geloescht.Db.SaveChangesAsync();
        Assert.Equal(0, await geloescht.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_TrainingAmZweitenHund_ErledigtDenAnstoss()
    {
        var s = await AufbauenAsync();
        var zweiter = new Dog { Name = "Luna", Gender = DogGender.Female };
        s.Db.Dogs.Add(zweiter);
        s.Db.DogOwners.Add(new DogOwner { DogId = zweiter.Id, UserId = s.Besitzerin, CreatedAt = Utc(11, 10) });
        s.Db.TrainingSessions.Add(new Domain.Training.TrainingSession { UserId = s.Besitzerin, DogId = zweiter.Id, Date = new DateOnly(2026, 11, 12), DurationMinutes = 10 });
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }
}
