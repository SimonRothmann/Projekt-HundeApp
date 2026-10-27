using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Dogs;

/// <summary>
/// Gesundheitseinträge eines Hundes: wer sie sehen und ändern darf (nur Besitzer:innen und
/// Mitbesitzer:innen - betreuende Trainer:innen und Fremde bekommen "nicht gefunden"), die Eingabe-Regeln,
/// "aktuell" gegenüber Verlauf und der Gesundheitsstand am Hund (nie für Trainer:innen).
/// </summary>
public class DogHealthServiceTests
{
    // Mittwoch, 4. November 2026, 10:00 Uhr deutscher Zeit (09:00 UTC).
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = new(2026, 11, 4);

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    private sealed record Aufbau(
        ApplicationDbContext Db,
        DogHealthService Gesundheit,
        DogService Hunde,
        Guid OwnerId,
        Guid CoOwnerId,
        Guid TrainerId,
        Guid FremdeId,
        Guid DogId,
        Guid BelloId);

    /// <summary>Hündin "Flitzer" mit Besitzerin, Mitbesitzer und betreuender Trainerin; ein Rüde "Bello" der Besitzerin; eine fremde Person.</summary>
    private static async Task<Aufbau> AufbauAsync()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var uhr = new FesteUhr(Jetzt);
        var ownerId = Guid.NewGuid();
        var coOwnerId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        lookup.Register(trainerId, "tina@dogity.test", "Tina", "Trainer");

        var flitzer = new Dog { Name = "Flitzer", Gender = DogGender.Female };
        var bello = new Dog { Name = "Bello", Gender = DogGender.Male };
        db.Dogs.AddRange(flitzer, bello);
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = coOwnerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = bello.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerId, MemberId = ownerId, DogId = flitzer.Id, StartDate = Heute });
        await db.SaveChangesAsync();

        return new Aufbau(
            db,
            new DogHealthService(db, uhr),
            new DogService(db, lookup, new FakeNotificationService(), uhr),
            ownerId, coOwnerId, trainerId, Guid.NewGuid(), flitzer.Id, bello.Id);
    }

    private static SaveDogHealthRecordRequest Impfung(string titel = "Tollwut", DateOnly? datum = null, DateOnly? bis = null, string? notiz = null) =>
        new(DogHealthKind.Vaccination, titel, datum ?? Heute, null, bis ?? Heute.AddYears(3), notiz);

    private static SaveDogHealthRecordRequest Termin(DateOnly datum, TimeOnly? uhrzeit = null, string titel = "Kontrolle") =>
        new(DogHealthKind.VetAppointment, titel, datum, uhrzeit, null, null);

    // --- Rechte ---------------------------------------------------------------

    [Fact]
    public async Task Create_BesitzerinUndMitbesitzer_DuerfenEintragen()
    {
        var a = await AufbauAsync();

        var erste = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung());
        var zweite = await a.Gesundheit.CreateAsync(a.CoOwnerId, a.DogId, Impfung("SHPPi"));

        Assert.True(erste.Succeeded);
        Assert.True(zweite.Succeeded);
        Assert.Equal(a.CoOwnerId, (await a.Db.DogHealthRecords.SingleAsync(r => r.Id == zweite.Value!.Id)).CreatedByUserId);
    }

    [Fact]
    public async Task Create_NurEingeladeneMitbesitzerin_BekommtNichtGefunden()
    {
        var a = await AufbauAsync();
        var eingeladen = Guid.NewGuid();
        a.Db.DogOwners.Add(new DogOwner { DogId = a.DogId, UserId = eingeladen, Role = DogOwnerRole.Owner, Status = DogOwnerStatus.Invited });
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Gesundheit.CreateAsync(eingeladen, a.DogId, Impfung())).IsNotFound);
        Assert.True((await a.Gesundheit.GetAsync(eingeladen, a.DogId)).IsNotFound);
    }

    [Fact]
    public async Task BetreuendeTrainerinUndFremde_BekommenBeiAllemNichtGefunden_AuchBeimLesen()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(notiz: "privat"))).Value!;

        foreach (var fremd in new[] { a.TrainerId, a.FremdeId })
        {
            Assert.True((await a.Gesundheit.GetAsync(fremd, a.DogId)).IsNotFound);
            Assert.True((await a.Gesundheit.CreateAsync(fremd, a.DogId, Impfung("SHPPi"))).IsNotFound);
            Assert.True((await a.Gesundheit.UpdateAsync(fremd, a.DogId, eintrag.Id, Impfung("Anders"))).IsNotFound);
            Assert.True((await a.Gesundheit.DeleteAsync(fremd, a.DogId, eintrag.Id)).IsNotFound);
        }

        var unveraendert = await a.Db.DogHealthRecords.SingleAsync();
        Assert.Equal("Tollwut", unveraendert.Title);
        Assert.Null(unveraendert.DeletedAt);
    }

    [Fact]
    public async Task EintragEinesAnderenHundes_LaesstSichUeberDieseUrlNichtAendern()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung())).Value!;

        // Die Besitzerin besitzt auch Bello - aber der Eintrag gehört Flitzer.
        Assert.True((await a.Gesundheit.UpdateAsync(a.OwnerId, a.BelloId, eintrag.Id, Impfung("Anders"))).IsNotFound);
        Assert.True((await a.Gesundheit.DeleteAsync(a.OwnerId, a.BelloId, eintrag.Id)).IsNotFound);
        Assert.Null((await a.Db.DogHealthRecords.SingleAsync()).DeletedAt);
    }

    [Fact]
    public async Task GeloeschterHund_KeinZugriff()
    {
        var a = await AufbauAsync();
        await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung());
        (await a.Db.Dogs.SingleAsync(d => d.Id == a.DogId)).DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Gesundheit.GetAsync(a.OwnerId, a.DogId)).IsNotFound);
        Assert.Empty(await a.Db.DogHealthRecords.ToListAsync());
    }

    // --- Eintragen, Ändern, Löschen ---------------------------------------------

    [Fact]
    public async Task Create_SpeichertAlleFelder_UndBereinigtTextUndLeereNotiz()
    {
        var a = await AufbauAsync();

        var ergebnis = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId,
            new SaveDogHealthRecordRequest(DogHealthKind.Vaccination, "  Tollwut  ", Heute, null, Heute.AddYears(3), "   "));

        var dto = ergebnis.Value!;
        Assert.Equal("Tollwut", dto.Title);
        Assert.Null(dto.Note);
        Assert.Equal(Heute.AddYears(3), dto.DueDate);
        Assert.True(dto.IsCurrent);
    }

    [Fact]
    public async Task Tierarzttermin_MitUhrzeit_OhneFrist()
    {
        var a = await AufbauAsync();

        var dto = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Termin(Heute.AddDays(2), new TimeOnly(10, 30)))).Value!;

        Assert.Equal(new TimeOnly(10, 30), dto.Time);
        Assert.Null(dto.DueDate);
        Assert.True(dto.IsCurrent);
    }

    [Fact]
    public async Task Update_AendertDenEintrag_UndSetztUpdatedAt()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung())).Value!;

        var ergebnis = await a.Gesundheit.UpdateAsync(a.CoOwnerId, a.DogId, eintrag.Id, Impfung("Leptospirose", Heute.AddDays(-1), Heute.AddYears(1), "Jahresimpfung"));

        Assert.True(ergebnis.Succeeded);
        var zeile = await a.Db.DogHealthRecords.SingleAsync();
        Assert.Equal("Leptospirose", zeile.Title);
        Assert.Equal("Jahresimpfung", zeile.Note);
        Assert.NotNull(zeile.UpdatedAt);
        // Wer angelegt hat, bleibt der, der angelegt hat.
        Assert.Equal(a.OwnerId, zeile.CreatedByUserId);
    }

    [Fact]
    public async Task Update_UnbekannterEintrag_NichtGefunden()
    {
        var a = await AufbauAsync();

        Assert.True((await a.Gesundheit.UpdateAsync(a.OwnerId, a.DogId, Guid.NewGuid(), Impfung())).IsNotFound);
    }

    [Fact]
    public async Task Delete_BlendetWeichAus_UndDieAnderenBleiben()
    {
        var a = await AufbauAsync();
        var weg = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung())).Value!;
        await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung("SHPPi"));

        Assert.True((await a.Gesundheit.DeleteAsync(a.OwnerId, a.DogId, weg.Id)).Succeeded);

        var liste = (await a.Gesundheit.GetAsync(a.OwnerId, a.DogId)).Value!;
        Assert.Equal("SHPPi", Assert.Single(liste).Title);
        Assert.NotNull((await a.Db.DogHealthRecords.IgnoreQueryFilters().SingleAsync(r => r.Id == weg.Id)).DeletedAt);
    }

    // --- Aktuell und Verlauf ----------------------------------------------------

    [Fact]
    public async Task Get_ErneuerteImpfung_IstAktuell_DieAlteBleibtImVerlauf()
    {
        var a = await AufbauAsync();
        var alt = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(datum: Heute.AddYears(-3), bis: Heute.AddDays(-5)))).Value!;
        var neu = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(datum: Heute.AddDays(-1), bis: Heute.AddYears(3)))).Value!;
        var shppi = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung("SHPPi"))).Value!;

        var liste = (await a.Gesundheit.GetAsync(a.OwnerId, a.DogId)).Value!;

        Assert.Equal(3, liste.Count);
        Assert.False(liste.Single(e => e.Id == alt.Id).IsCurrent);
        Assert.True(liste.Single(e => e.Id == neu.Id).IsCurrent);
        Assert.True(liste.Single(e => e.Id == shppi.Id).IsCurrent);
        // Jüngste zuerst.
        Assert.Equal(alt.Id, liste[^1].Id);
    }

    [Fact]
    public async Task Get_VergangenerTermin_IstVerlauf_KuenftigerAktuell()
    {
        var a = await AufbauAsync();
        var vorbei = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Termin(Heute.AddDays(-3)))).Value!;
        var heute = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Termin(Heute, titel: "Heute"))).Value!;
        var spaeter = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Termin(Heute.AddDays(5), titel: "Später"))).Value!;

        var liste = (await a.Gesundheit.GetAsync(a.OwnerId, a.DogId)).Value!;

        Assert.False(liste.Single(e => e.Id == vorbei.Id).IsCurrent);
        Assert.True(liste.Single(e => e.Id == heute.Id).IsCurrent);
        Assert.True(liste.Single(e => e.Id == spaeter.Id).IsCurrent);
    }

    [Fact]
    public async Task Create_DieRueckgabeKennzeichnetEineAelterePostVorhandeneImpfungAlsVerlauf()
    {
        var a = await AufbauAsync();
        await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(datum: Heute));

        // Nachgetragen: eine ältere Tollwut-Impfung - sie ist nicht die aktuelle.
        var nachgetragen = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(datum: Heute.AddYears(-4), bis: Heute.AddYears(-1)))).Value!;

        Assert.False(nachgetragen.IsCurrent);
    }

    // --- Eingabe ------------------------------------------------------------------

    [Fact]
    public async Task Create_OhneTitel_Abgelehnt()
    {
        var a = await AufbauAsync();

        var ergebnis = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung("   "));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Empty(await a.Db.DogHealthRecords.ToListAsync());
    }

    [Fact]
    public async Task Create_UnbekannteArt_Abgelehnt()
    {
        var a = await AufbauAsync();

        var ergebnis = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId,
            new SaveDogHealthRecordRequest((DogHealthKind)99, "X", Heute, null, null, null));

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Create_ZuLangeTexte_Abgelehnt_AmGrenzwertErlaubt()
    {
        var a = await AufbauAsync();

        Assert.False((await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(new string('a', Textlaengen.GesundheitTitel + 1)))).Succeeded);
        Assert.False((await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(notiz: new string('n', Textlaengen.GesundheitNotiz + 1)))).Succeeded);
        Assert.True((await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId,
            Impfung(new string('a', Textlaengen.GesundheitTitel), notiz: new string('n', Textlaengen.GesundheitNotiz)))).Succeeded);
    }

    [Fact]
    public async Task Create_FristVorDemDatum_Abgelehnt_GleicherTagErlaubt()
    {
        var a = await AufbauAsync();

        var davor = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(datum: Heute, bis: Heute.AddDays(-1)));
        var gleich = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung("SHPPi", Heute, Heute));

        Assert.False(davor.Succeeded);
        Assert.True(gleich.Succeeded);
    }

    [Fact]
    public async Task Create_TierarzttermineHabenKeineFrist_ImpfungenKeineUhrzeit()
    {
        var a = await AufbauAsync();

        var terminMitFrist = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId,
            new SaveDogHealthRecordRequest(DogHealthKind.VetAppointment, "Kontrolle", Heute.AddDays(1), null, Heute.AddDays(30), null));
        var impfungMitUhrzeit = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId,
            new SaveDogHealthRecordRequest(DogHealthKind.Vaccination, "Tollwut", Heute, new TimeOnly(9, 0), Heute.AddYears(1), null));

        Assert.False(terminMitFrist.Succeeded);
        Assert.False(impfungMitUhrzeit.Succeeded);
    }

    [Theory]
    [InlineData(-26 * 365, false)]
    [InlineData(-24 * 365, true)]
    [InlineData(2 * 365 + 10, false)]
    [InlineData(300, true)]
    public async Task Create_DatumZuWeitWeg_Abgelehnt(int tage, bool erlaubt)
    {
        var a = await AufbauAsync();

        var ergebnis = await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Termin(Heute.AddDays(tage)));

        Assert.Equal(erlaubt, ergebnis.Succeeded);
    }

    [Fact]
    public async Task Create_FristZuWeitWeg_Abgelehnt()
    {
        var a = await AufbauAsync();

        Assert.False((await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(bis: Heute.AddYears(11)))).Succeeded);
        Assert.True((await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung(bis: Heute.AddYears(5)))).Succeeded);
    }

    [Fact]
    public async Task Update_PruefungGiltAuchBeimAendern()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung())).Value!;

        var ergebnis = await a.Gesundheit.UpdateAsync(a.OwnerId, a.DogId, eintrag.Id, Impfung(titel: ""));

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Tollwut", (await a.Db.DogHealthRecords.SingleAsync()).Title);
    }

    // --- Gesundheitsstand am Hund (Hundeliste / Hundeseite) ---------------------------

    [Fact]
    public async Task Hund_BesitzerinUndMitbesitzer_BekommenDenStand_ImmerNurFuerDieEigenenHunde()
    {
        var a = await AufbauAsync();
        await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung("Tollwut", Heute.AddYears(-1), Heute.AddDays(-1)));
        await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Termin(Heute.AddDays(2), new TimeOnly(10, 30)));

        var liste = (await a.Hunde.GetMyDogsAsync(a.CoOwnerId)).Value!;
        var einzeln = (await a.Hunde.GetByIdAsync(a.OwnerId, a.DogId)).Value!;

        var flitzer = Assert.Single(liste);
        Assert.Equal(1, flitzer.Gesundheit!.Overdue);
        Assert.Equal("Tollwut", flitzer.Gesundheit.NextDue!.Title);
        Assert.Equal(new TimeOnly(10, 30), flitzer.Gesundheit.NextAppointment!.Time);
        Assert.Equal(1, einzeln.Gesundheit!.Overdue);
    }

    [Fact]
    public async Task Hund_BetreuendeTrainerin_BekommtKeinenStand_AuchNichtInDerUebersicht()
    {
        var a = await AufbauAsync();
        await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung("Tollwut", Heute.AddYears(-1), Heute.AddDays(-1)));

        var einzeln = (await a.Hunde.GetByIdAsync(a.TrainerId, a.DogId)).Value!;
        var betreut = (await a.Hunde.GetSupervisedDogsAsync(a.TrainerId)).Value!;

        Assert.Null(einzeln.Gesundheit);
        Assert.Single(betreut); // Die Trainer-DTOs haben das Feld gar nicht (SupervisedDogDto).
        Assert.DoesNotContain(typeof(SupervisedDogDto).GetProperties(), p => p.PropertyType == typeof(DogHealthSummaryDto));
    }

    [Fact]
    public async Task Hund_OhneEintraege_HatKeinenStand()
    {
        var a = await AufbauAsync();

        var liste = (await a.Hunde.GetMyDogsAsync(a.OwnerId)).Value!;

        Assert.All(liste, d => Assert.Null(d.Gesundheit));
    }

    [Fact]
    public async Task Hund_StandIstJeHundGetrennt_UndGeloeschteEintraegeZaehlenNicht()
    {
        var a = await AufbauAsync();
        var flitzer = (await a.Gesundheit.CreateAsync(a.OwnerId, a.DogId, Impfung("Tollwut", Heute.AddYears(-1), Heute.AddDays(-1)))).Value!;
        await a.Gesundheit.CreateAsync(a.OwnerId, a.BelloId, Impfung("Tollwut", Heute, Heute.AddYears(3)));
        await a.Gesundheit.DeleteAsync(a.OwnerId, a.DogId, flitzer.Id);

        var liste = (await a.Hunde.GetMyDogsAsync(a.OwnerId)).Value!;

        Assert.Null(liste.Single(d => d.Id == a.DogId).Gesundheit);
        Assert.Equal(0, liste.Single(d => d.Id == a.BelloId).Gesundheit!.Overdue);
    }
}
