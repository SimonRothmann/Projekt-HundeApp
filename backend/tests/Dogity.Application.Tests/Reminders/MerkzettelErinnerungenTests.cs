using Dogity.Application.Common;
using Dogity.Application.Reminders;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Reminders;

/// <summary>
/// Die Erinnerung an den Merkzettel: je Person und Hund eine Erinnerung am Trainingstag, zwei Stunden vor dem
/// Training (ohne Uhrzeit um 09:00), gebündelt, nie zweimal am selben Tag. Die Regeln ohne Datenbank, der Versand
/// (wer, Schalter, erledigte) gegen eine Datenbank. Die Uhrzeiten im Test sind UTC; im November gilt in Deutschland UTC+1.
/// </summary>
public class MerkzettelErinnerungenTests
{
    private static readonly IReadOnlySet<ErinnerungsSchluessel> NichtsGesendet = new HashSet<ErinnerungsSchluessel>();

    private static DateTimeOffset Utc(int tag, int stunde, int minute = 0, int monat = 11) =>
        new(2026, monat, tag, stunde, minute, 0, TimeSpan.Zero);

    private static string De(FaelligeErinnerung e) => Uebersetzung.Rendere(e.Meldung, null);
    private static string En(FaelligeErinnerung e) => Uebersetzung.Rendere(e.Meldung, "en");

    // Mittwoch, 4.11.2026. Das Tagebuch sagt: mittwochs um 18:00 (21.10. und 28.10.).
    private static readonly DateOnly Heute = new(2026, 11, 4);
    private static readonly TagebuchTraining[] MittwochAchtzehn =
        [new(new DateOnly(2026, 10, 21), new TimeOnly(18, 0)), new(new DateOnly(2026, 10, 28), new TimeOnly(18, 0))];

    // ---------------------------------------------------------------- Regeln

    private static MerkzettelEintrag Eintrag(
        string text = "Fuß rückwärts", TrainingNoteGoal ziel = TrainingNoteGoal.NextTraining, DateOnly? datum = null, DateTimeOffset? angelegt = null) =>
        new(Guid.NewGuid(), text, ziel, datum, angelegt ?? Utc(1, 10));

    private static MerkzettelKandidat Kandidat(
        IEnumerable<MerkzettelEintrag> eintraege, IReadOnlyList<TagebuchTraining>? tagebuch = null,
        IReadOnlyList<DateTimeOffset>? gruppen = null, GespeicherterRhythmus? rhythmus = null,
        bool erlaubt = true, Guid? user = null, Guid? hund = null) =>
        new(user ?? Guid.NewGuid(), hund ?? Guid.NewGuid(), "Bello", erlaubt, eintraege.ToList(),
            tagebuch ?? MittwochAchtzehn, gruppen ?? [], rhythmus);

    private static IReadOnlyList<FaelligeErinnerung> Fuer(DateTimeOffset jetzt, params MerkzettelKandidat[] k) =>
        Erinnerungsregeln.MerkzettelErinnerungen(jetzt, k, NichtsGesendet);

    [Fact]
    public void ZweiStundenVorDemTraining_KommtDieErinnerung_MitUhrzeitImText()
    {
        var k = Kandidat([Eintrag()]);

        var e = Assert.Single(Fuer(Utc(4, 15), k)); // 16:00 Uhr deutscher Zeit

        Assert.Equal("Heute 18:00 - gemerkt für Bello: Fuß rückwärts", De(e));
        Assert.Equal("Today 18:00 - noted for Bello: Fuß rückwärts", En(e));
        Assert.Equal(new ErinnerungsSchluessel(ReminderType.TrainingNote, k.UserId, k.HundId, Heute), e.Schluessel);
        Assert.Equal($"/dogs/{k.HundId}?tab=plan", e.Link);
    }

    [Fact]
    public void VorDemErinnerungszeitpunkt_KommtNichts()
    {
        Assert.Empty(Fuer(Utc(4, 14, 59), Kandidat([Eintrag()]))); // 15:59 Uhr
    }

    [Fact]
    public void ZumSpaetenTick_BisZumTrainingsbeginn_KommtSieNoch()
    {
        // Der Tick kommt alle zehn Minuten: 17:50 Uhr, das Training beginnt um 18:00.
        Assert.Single(Fuer(Utc(4, 16, 50), Kandidat([Eintrag()])));
    }

    [Fact]
    public void NachDemTrainingsbeginn_KommtNichts()
    {
        Assert.Empty(Fuer(Utc(4, 17, 5), Kandidat([Eintrag()]))); // 18:05 Uhr
    }

    [Fact]
    public void MehrereEintraege_EineGebuendelteErinnerung_ErsterTextPlusAnzahl()
    {
        var k = Kandidat([
            Eintrag("Fuß rückwärts", angelegt: Utc(2, 10)),
            Eintrag("Abliegen aus der Bewegung", angelegt: Utc(3, 10)),
            Eintrag("Voran", ziel: TrainingNoteGoal.Date, datum: Heute, angelegt: Utc(3, 11))]);

        var e = Assert.Single(Fuer(Utc(4, 15), k));

        Assert.Equal("Heute 18:00 - gemerkt für Bello: Fuß rückwärts · +2 weitere", De(e));
        Assert.Equal("Today 18:00 - noted for Bello: Fuß rückwärts · +2 more", En(e));
    }

    [Fact]
    public void ZweiEintraege_ErsterIstDerAelteste()
    {
        var k = Kandidat([Eintrag("Neuer", angelegt: Utc(3, 10)), Eintrag("Älterer", angelegt: Utc(2, 10))]);

        Assert.Equal("Heute 18:00 - gemerkt für Bello: Älterer · +1 weitere", De(Assert.Single(Fuer(Utc(4, 15), k))));
    }

    [Fact]
    public void LangerText_WirdGekuerzt_ZeilenumbruecheWerdenZuLeerzeichen()
    {
        var lang = "Zeile eins\nZeile zwei " + new string('x', 200);

        var e = Assert.Single(Fuer(Utc(4, 15), Kandidat([Eintrag(lang)])));

        var text = De(e);
        Assert.StartsWith("Heute 18:00 - gemerkt für Bello: Zeile eins Zeile zwei xxx", text);
        Assert.EndsWith("…", text);
        Assert.DoesNotContain('\n', text);
        Assert.True(text.Length < 160);
    }

    [Fact]
    public void OhneUhrzeit_UmNeunUhr_TextOhneZeit()
    {
        var k = Kandidat([Eintrag()], tagebuch: [], rhythmus: new GespeicherterRhythmus(4, null)); // mittwochs, keine Uhrzeit

        Assert.Empty(Fuer(Utc(4, 7, 59), k)); // 08:59 Uhr
        var e = Assert.Single(Fuer(Utc(4, 8), k)); // 09:00 Uhr
        Assert.Equal("Heute Training - gemerkt für Bello: Fuß rückwärts", De(e));
        Assert.Equal("Training today - noted for Bello: Fuß rückwärts", En(e));
    }

    [Fact]
    public void OhneUhrzeit_MitWeiteren_TextMitAnzahl()
    {
        var k = Kandidat([Eintrag("Eins", angelegt: Utc(2, 10)), Eintrag("Zwei", angelegt: Utc(3, 10))], tagebuch: [], rhythmus: new GespeicherterRhythmus(4, null));

        Assert.Equal("Heute Training - gemerkt für Bello: Eins · +1 weitere", De(Assert.Single(Fuer(Utc(4, 8), k))));
    }

    [Fact]
    public void TrainingAmMorgen_ErinnerungFruehestensUmAchtUhr()
    {
        // Training 09:30 -> zwei Stunden vorher wäre 07:30, in der Ruhezeit: Es kommt um 08:00.
        var k = Kandidat([Eintrag()], tagebuch: [], rhythmus: new GespeicherterRhythmus(4, 9));
        Assert.Empty(Fuer(Utc(4, 6, 59), k)); // 07:59 Uhr, außerhalb des Fensters

        var e = Assert.Single(Fuer(Utc(4, 7), k)); // 08:00 Uhr
        Assert.Equal("Heute 09:00 - gemerkt für Bello: Fuß rückwärts", De(e));
    }

    [Fact]
    public void AusserhalbDesVersandfensters_KommtNichts()
    {
        var k = Kandidat([Eintrag()], tagebuch: [], rhythmus: new GespeicherterRhythmus(4, 23));

        Assert.Empty(Fuer(Utc(4, 21, 30), k)); // 22:30 Uhr: Training 23:00, aber das Fenster ist zu
    }

    [Fact]
    public void Gruppentermin_GehtVorDemRhythmus_UhrzeitDesTermins()
    {
        // Heute 19:00 Uhr (18:00 UTC) in der Gruppe; das Tagebuch sagt 18:00.
        var k = Kandidat([Eintrag()], gruppen: [Utc(4, 18)]);

        Assert.Empty(Fuer(Utc(4, 15), k)); // 16:00: Erinnerung erst um 17:00
        Assert.Equal("Heute 19:00 - gemerkt für Bello: Fuß rückwärts", De(Assert.Single(Fuer(Utc(4, 16), k))));
    }

    [Fact]
    public void Tagesgrenze_EinGruppentermin_NachMitternachtVereinszeit_GehoertZumNeuenTag()
    {
        // 23:30 UTC am 4.11. ist 00:30 Uhr am 5.11. - heute (4.11.) ist es nicht.
        var k = Kandidat([Eintrag()], tagebuch: [], gruppen: [Utc(4, 23, 30)]);

        Assert.Empty(Fuer(Utc(4, 12), k));
    }

    [Fact]
    public void Datum_HeuteOhneErkanntesTraining_UmNeunUhr()
    {
        var k = Kandidat([Eintrag(ziel: TrainingNoteGoal.Date, datum: Heute)], tagebuch: []);

        Assert.Empty(Fuer(Utc(4, 7, 30), k));
        Assert.Equal("Heute Training - gemerkt für Bello: Fuß rückwärts", De(Assert.Single(Fuer(Utc(4, 8), k))));
    }

    [Fact]
    public void Datum_EinAndererTag_KommtNichtHeute()
    {
        var k = Kandidat([Eintrag(ziel: TrainingNoteGoal.Date, datum: Heute.AddDays(1)), Eintrag(ziel: TrainingNoteGoal.Date, datum: Heute.AddDays(-1))], tagebuch: []);

        Assert.Empty(Fuer(Utc(4, 12), k));
    }

    [Fact]
    public void NaechsteWoche_KommtAmErstenErkanntenTrainingAbMontag()
    {
        // Montag der Folgewoche ist der 9.11.; das erste erkannte Training danach: Mittwoch, 11.11., 18:00.
        var k = Kandidat([Eintrag(ziel: TrainingNoteGoal.NextWeek, datum: new DateOnly(2026, 11, 9))]);

        Assert.Empty(Fuer(Utc(4, 15), k));
        Assert.Empty(Fuer(Utc(9, 12), k));
        Assert.Equal("Heute 18:00 - gemerkt für Bello: Fuß rückwärts", De(Assert.Single(Fuer(Utc(11, 15), k))));
    }

    [Fact]
    public void NaechsteWoche_OhneErkanntesTraining_MontagUmNeunUhr()
    {
        var k = Kandidat([Eintrag(ziel: TrainingNoteGoal.NextWeek, datum: new DateOnly(2026, 11, 9))], tagebuch: []);

        Assert.Empty(Fuer(Utc(9, 7, 30), k));
        Assert.Equal("Heute Training - gemerkt für Bello: Fuß rückwärts", De(Assert.Single(Fuer(Utc(9, 8), k))));
    }

    [Fact]
    public void NaechstesTraining_OhneErkanntesTraining_WieNaechsteWoche()
    {
        // Mittwoch gemerkt (4.11.), nichts erkannt: Montag, 9.11., 09:00.
        var k = Kandidat([Eintrag(angelegt: Utc(4, 9))], tagebuch: []);

        Assert.Empty(Fuer(Utc(4, 12), k));
        Assert.Single(Fuer(Utc(9, 8), k));
    }

    [Fact]
    public void NaechstesTraining_ErstNachDemErinnerungszeitpunktGemerkt_MeintDasNaechste()
    {
        // Mittwoch 17:30 Uhr gemerkt (die Maske zeigte schon "nächste Woche"): heute um 18:00 kommt nichts.
        var k = Kandidat([Eintrag(angelegt: Utc(4, 16, 30))]);

        Assert.Empty(Fuer(Utc(4, 16, 40), k));
        Assert.Single(Fuer(Utc(11, 15), k)); // eine Woche später
    }

    [Fact]
    public void NaechstesTraining_VorDemErinnerungszeitpunktGemerkt_IstDabei()
    {
        var k = Kandidat([Eintrag(angelegt: Utc(4, 14, 59))]); // 15:59 Uhr, die Erinnerung ist erst um 16:00

        Assert.Single(Fuer(Utc(4, 15), k));
    }

    [Fact]
    public void Unerledigt_WirdAmNaechstenTrainingTagErneutErinnert_AmSelbenTagNicht()
    {
        var k = Kandidat([Eintrag(angelegt: Utc(1, 10))]);
        var erste = Assert.Single(Fuer(Utc(4, 15), k));

        // Gleicher Tag: der Schlüssel ist gemerkt.
        Assert.Empty(Erinnerungsregeln.MerkzettelErinnerungen(Utc(4, 16), [k], new HashSet<ErinnerungsSchluessel> { erste.Schluessel }));
        // Eine Woche später: anderer Trainingstag, anderer Schlüssel.
        var spaeter = Assert.Single(Erinnerungsregeln.MerkzettelErinnerungen(Utc(11, 15), [k], new HashSet<ErinnerungsSchluessel> { erste.Schluessel }));
        Assert.Equal(Heute.AddDays(7), spaeter.Schluessel.Tag);
    }

    [Fact]
    public void SchalterAus_OderKeineEintraege_KeineErinnerung()
    {
        Assert.Empty(Fuer(Utc(4, 15), Kandidat([Eintrag()], erlaubt: false)));
        Assert.Empty(Fuer(Utc(4, 15), Kandidat([])));
    }

    [Fact]
    public void ZweiPersonenAmSelbenHund_JeEineErinnerung()
    {
        var hund = Guid.NewGuid();

        var ergebnis = Fuer(Utc(4, 15), Kandidat([Eintrag()], hund: hund), Kandidat([Eintrag()], hund: hund));

        Assert.Equal(2, ergebnis.Count);
        Assert.Equal(2, ergebnis.Select(e => e.Schluessel.UserId).Distinct().Count());
    }

    [Fact]
    public void ZweimalDieselbeKennungInEinemLauf_NurEineErinnerung()
    {
        var user = Guid.NewGuid();
        var hund = Guid.NewGuid();

        Assert.Single(Fuer(Utc(4, 15), Kandidat([Eintrag()], user: user, hund: hund), Kandidat([Eintrag()], user: user, hund: hund)));
    }

    // ---------------------------------------------------------------- Versand gegen die Datenbank

    private sealed class Uhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Szene(
        ApplicationDbContext Db, FakeNotificationService Meldungen, Uhr Uhr, ErinnerungService Dienst,
        Dog Hund, Guid Besitzerin, Guid Mitbesitzer, Guid Trainerin);

    /// <summary>Hund Bello mit Besitzerin, Mitbesitzer und betreuender Trainerin; im Tagebuch zwei Mittwochs-Trainings um 18:00; Mittwoch, 4.11., 16:05 Uhr.</summary>
    private static async Task<Szene> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var uhr = new Uhr(Utc(4, 15, 5));
        var hund = new Dog { Name = "Bello", Gender = DogGender.Male };
        var besitzerin = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        var trainerin = Guid.NewGuid();
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzerin, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = mitbesitzer, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = hund.Id, TrainerId = trainerin, MemberId = besitzerin, StartDate = Heute });
        foreach (var t in MittwochAchtzehn)
            db.TrainingSessions.Add(new Domain.Training.TrainingSession { UserId = besitzerin, DogId = hund.Id, Date = t.Datum, StartTime = t.StartTime, DurationMinutes = 60 });
        await db.SaveChangesAsync();
        return new Szene(db, meldungen, uhr, new ErinnerungService(db, meldungen, uhr, NullLogger<ErinnerungService>.Instance), hund, besitzerin, mitbesitzer, trainerin);
    }

    private static async Task<TrainingNote> NotizAsync(
        Szene s, string text = "Fuß rückwärts", TrainingNoteGoal ziel = TrainingNoteGoal.NextTraining, DateOnly? datum = null,
        DateTimeOffset? erledigt = null, DateTimeOffset? angelegt = null, Dog? hund = null)
    {
        var n = new TrainingNote
        {
            DogId = (hund ?? s.Hund).Id, UserId = s.Besitzerin, Text = text, Goal = ziel, TargetDate = datum, DoneAt = erledigt,
            CreatedAt = angelegt ?? Utc(1, 10),
        };
        s.Db.TrainingNotes.Add(n);
        await s.Db.SaveChangesAsync();
        return n;
    }

    [Fact]
    public async Task Versand_BeideBesitzer_JeEineMeldung_TrainerinNicht_GlockeMitLink()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s);

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());

        Assert.Equal(new[] { s.Besitzerin, s.Mitbesitzer }.Order(), s.Meldungen.Created.Select(c => c.UserId).Order());
        Assert.DoesNotContain(s.Meldungen.Created, c => c.UserId == s.Trainerin);
        Assert.All(s.Meldungen.Created, c =>
        {
            Assert.Equal("Heute 18:00 - gemerkt für Bello: Fuß rückwärts", c.Message);
            Assert.Equal($"/dogs/{s.Hund.Id}?tab=plan", c.LinkPath);
        });
        Assert.Empty(s.Meldungen.Wichtige); // Glocke und Push, nie Mail
    }

    [Fact]
    public async Task Versand_ZweiterLaufUndNeustart_SchickenNichtsNochmal()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s);

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        s.Uhr.Jetzt = Utc(4, 15, 40);
        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
        var neu = new ErinnerungService(s.Db, s.Meldungen, s.Uhr, NullLogger<ErinnerungService>.Instance);
        Assert.Equal(0, await neu.VersendeFaelligeAsync());

        Assert.Equal(2, await s.Db.SentReminders.CountAsync(r => r.Type == ReminderType.TrainingNote));
    }

    [Fact]
    public async Task Versand_ErledigteUndGeloeschteEintraege_ZaehlenNicht()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s, "Erledigt", erledigt: Utc(3, 10));
        var geloescht = await NotizAsync(s, "Gelöscht");
        geloescht.DeletedAt = Utc(3, 11);
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());

        await NotizAsync(s, "Offen");
        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        Assert.All(s.Meldungen.Created, c => Assert.Equal("Heute 18:00 - gemerkt für Bello: Offen", c.Message));
    }

    [Fact]
    public async Task Versand_SchalterAus_DieseBesitzerinNicht_OhneZeileGiltAn()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s);
        s.Db.UserPreferences.Add(new UserPreference { UserId = s.Besitzerin, ReminderTrainingNotes = false });
        await s.Db.SaveChangesAsync();

        Assert.Equal(1, await s.Dienst.VersendeFaelligeAsync());
        Assert.Equal(s.Mitbesitzer, Assert.Single(s.Meldungen.Created).UserId);
    }

    [Fact]
    public async Task Versand_AndereErinnerungenAus_ZaehlenHierNicht()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s);
        s.Db.UserPreferences.Add(new UserPreference
        {
            UserId = s.Besitzerin, ReminderSessions = false, ReminderTracks = false, ReminderTrainerSummary = false, ReminderHealth = false,
        });
        await s.Db.SaveChangesAsync();

        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_ArchivierterHund_KeineErinnerung()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s);
        s.Hund.ArchivedAt = Utc(1, 10);
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync());
    }

    [Fact]
    public async Task Versand_NutztDenGespeichertenRhythmusVorDemTagebuch()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s);
        // Der Hund trainiert laut Angabe donnerstags (8) um 10 Uhr, nicht mittwochs.
        s.Db.DogTrainingRhythms.Add(new DogTrainingRhythm { DogId = s.Hund.Id, Days = 8, Hour = 10 });
        await s.Db.SaveChangesAsync();

        Assert.Equal(0, await s.Dienst.VersendeFaelligeAsync()); // heute ist Mittwoch

        s.Uhr.Jetzt = Utc(5, 7); // Donnerstag, 08:00 Uhr: zwei Stunden vor 10:00
        Assert.Equal(2, await s.Dienst.VersendeFaelligeAsync());
        Assert.All(s.Meldungen.Created, c => Assert.StartsWith("Heute 10:00", c.Message));
    }

    [Fact]
    public async Task Versand_GruppenterminDerPerson_ZaehltNurFuerSie()
    {
        var s = await AufbauenAsync();
        await NotizAsync(s);
        var verein = new Club { Name = "Verein" };
        var gruppe = new Group { ClubId = verein.Id, Name = "Gruppe A", TrainerId = Guid.NewGuid() };
        s.Db.Clubs.Add(verein);
        s.Db.Groups.Add(gruppe);
        s.Db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Id, UserId = s.Besitzerin, Status = GroupMemberStatus.Active });
        s.Db.GroupTrainingSessions.Add(new GroupTrainingSession { ClubId = verein.Id, GroupId = gruppe.Id, StartsAt = Utc(4, 18) }); // 19:00 Uhr
        await s.Db.SaveChangesAsync();

        s.Uhr.Jetzt = Utc(4, 16, 5); // 17:05 Uhr: beide Termine stehen noch bevor und die Erinnerung ist dran
        await s.Dienst.VersendeFaelligeAsync();

        Assert.Contains(s.Meldungen.Created, c => c.UserId == s.Besitzerin && c.Message.StartsWith("Heute 19:00"));
        Assert.Contains(s.Meldungen.Created, c => c.UserId == s.Mitbesitzer && c.Message.StartsWith("Heute 18:00"));
    }
}
