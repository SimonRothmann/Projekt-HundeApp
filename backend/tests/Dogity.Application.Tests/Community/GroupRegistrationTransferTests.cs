using Dogity.Application.Community;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// "Wächst heraus": Höchstalter der Gruppe, Markierung in der Liste und die
/// Übernahme einer Anmeldung in eine andere Gruppe desselben Vereins.
/// </summary>
public class GroupRegistrationTransferTests
{
    private static readonly DateOnly Heute = new(2026, 11, 20);

    // ---- Altersrechnung ----

    [Theory]
    [InlineData(null, false)] // keine Grenze
    [InlineData(20, false)]   // Grenze erst in 35 Tagen
    [InlineData(16, true)]    // Grenze in 7 Tagen = im Vorlauf
    [InlineData(14, true)]    // schon überschritten
    public void WaechstHeraus_RechnetGrenzeUndVorlauf(int? hoechstalterWochen, bool erwartet)
    {
        var wurftag = Heute.AddDays(-15 * 7); // Hund ist 15 Wochen alt

        Assert.Equal(erwartet, GroupRegistrationRules.WaechstHeraus(wurftag, hoechstalterWochen, Heute));
    }

    [Fact]
    public void WaechstHeraus_AmTagVorDemVorlauf_NochNicht()
    {
        // Grenze (16 Wochen) liegt in 15 Tagen: Wurftag = heute - 16 Wochen + 15 Tage.
        var wurftag = Heute.AddDays(-16 * 7 + 15);
        Assert.False(GroupRegistrationRules.WaechstHeraus(wurftag, 16, Heute));
        Assert.True(GroupRegistrationRules.WaechstHeraus(wurftag.AddDays(-1), 16, Heute));
    }

    [Fact]
    public async Task Liste_MarkiertHerauswachsende()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        a.Gruppe.MaxAgeWeeks = 16;
        var jung = await a.AnmeldungAsync("Jung"); // 3 Monate alt = ca. 13 Wochen
        var alt = await a.AnmeldungAsync("Alt", telefon: "0160 999999");
        alt.DogBirthDate = Vereinszeit.Heute().AddDays(-20 * 7);
        await a.Db.SaveChangesAsync();

        var liste = (await a.Dienst.ListAsync(a.Leitung, a.Gruppe.Id)).Value!;

        Assert.False(liste.Single(r => r.Id == jung.Id).GrowsOut);
        Assert.True(liste.Single(r => r.Id == alt.Id).GrowsOut);
    }

    [Fact]
    public async Task Liste_OhneHoechstalter_MarkiertNie()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var alt = await a.AnmeldungAsync("Alt");
        alt.DogBirthDate = Vereinszeit.Heute().AddDays(-60 * 7);
        await a.Db.SaveChangesAsync();

        Assert.All((await a.Dienst.ListAsync(a.Leitung, a.Gruppe.Id)).Value!, r => Assert.False(r.GrowsOut));
    }

    // ---- Übernahme ----

    private static async Task<Group> ZielgruppeAsync(AnmeldeAufbau a, Guid zielLeitung, Guid? clubId = null)
    {
        var ziel = new Group { ClubId = clubId ?? a.Verein.Id, TrainerId = zielLeitung, Name = "Junghunde" };
        a.Db.Groups.Add(ziel);
        await a.Db.SaveChangesAsync();
        return ziel;
    }

    [Fact]
    public async Task Uebernehmen_SetztFelder_LoeschtAnwesenheit_BehaeltNotiz()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zielLeitung = Guid.NewGuid();
        var ziel = await ZielgruppeAsync(a, zielLeitung);
        var zeile = await a.AnmeldungAsync();
        zeile.Notes = "Nervös bei Lärm";
        zeile.PaidAt = DateTimeOffset.UtcNow.AddDays(-30);
        zeile.PaidByUserId = a.Leitung;
        zeile.RegisteredAt = DateTimeOffset.UtcNow.AddDays(-90);
        await a.Db.SaveChangesAsync();
        Assert.True((await a.Dienst.SetAttendanceAsync(a.Leitung, a.Gruppe.Id, zeile.Id, Vereinszeit.Heute(), true)).Succeeded);

        var vorher = DateTimeOffset.UtcNow;
        var ergebnis = await a.Dienst.TransferAsync(a.Leitung, a.Gruppe.Id, zeile.Id, ziel.Id);

        Assert.True(ergebnis.Succeeded, string.Join(", ", ergebnis.Errors));
        var neu = await a.Db.GroupRegistrations.SingleAsync(r => r.Id == zeile.Id);
        Assert.Equal(ziel.Id, neu.GroupId);
        Assert.Equal(GroupRegistrationSource.Uebernommen, neu.Source);
        Assert.True(neu.RegisteredAt >= vorher);
        Assert.Null(neu.PaidAt);
        Assert.Null(neu.PaidByUserId);
        Assert.Equal("Nervös bei Lärm", neu.Notes);
        Assert.Empty(await a.Db.GroupRegistrationAttendances.IgnoreQueryFilters().Where(h => h.RegistrationId == zeile.Id).ToListAsync());
        // Die alte Gruppe sieht die Anmeldung nicht mehr.
        Assert.Empty((await a.Dienst.ListAsync(a.Leitung, a.Gruppe.Id)).Value!);
    }

    [Fact]
    public async Task Uebernehmen_BenachrichtigtTrainerinnenDerZielgruppe_NichtDieAusfuehrende()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var zielLeitung = Guid.NewGuid();
        var ziel = await ZielgruppeAsync(a, zielLeitung);
        var zeile = await a.AnmeldungAsync("Bella");

        await a.Dienst.TransferAsync(a.Leitung, a.Gruppe.Id, zeile.Id, ziel.Id);

        // Die Ausbildungswartin verwaltet alle Gruppen des Vereins und gehört damit auch zur Zielgruppe.
        Assert.Contains(a.Meldungen.Created, m => m.UserId == zielLeitung
            && m.Message == "Übernommen aus Welpengruppe in Junghunde: Bella (Labrador)."
            && m.LinkPath == $"/trainer/{ziel.Id}?ansicht=anmeldungen");
        Assert.Contains(a.Meldungen.Created, m => m.UserId == a.VereinsTrainerin);
        Assert.DoesNotContain(a.Meldungen.Created, m => m.UserId == a.Leitung);
    }

    [Fact]
    public async Task Uebernehmen_AusbildungswartinDarfAuchOhneTrainerinDerQuelle()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var ziel = await ZielgruppeAsync(a, Guid.NewGuid());
        var zeile = await a.AnmeldungAsync();

        Assert.True((await a.Dienst.TransferAsync(a.VereinsTrainerin, a.Gruppe.Id, zeile.Id, ziel.Id)).Succeeded);
    }

    [Fact]
    public async Task Uebernehmen_Fremde_Mitglied_UndNurTraining_Bekommen404()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var ziel = await ZielgruppeAsync(a, Guid.NewGuid());
        var zeile = await a.AnmeldungAsync();

        var mitglied = Guid.NewGuid();
        a.Db.ClubMemberships.Add(new ClubMembership { ClubId = a.Verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved });
        a.Db.GroupMembers.Add(new GroupMember { GroupId = a.Gruppe.Id, UserId = mitglied, Status = GroupMemberStatus.Active });
        var nurTraining = Guid.NewGuid(); // trainiert eine andere Gruppe, nicht diese
        a.Db.ClubTrainers.Add(new ClubTrainer { ClubId = a.Verein.Id, UserId = nurTraining, Role = ClubRole.Training });
        await a.Db.SaveChangesAsync();

        foreach (var wer in new[] { a.Fremde, mitglied, nurTraining })
        {
            var ergebnis = await a.Dienst.TransferAsync(wer, a.Gruppe.Id, zeile.Id, ziel.Id);
            Assert.True(ergebnis.IsNotFound);
            Assert.True((await a.Dienst.GetTransferTargetsAsync(wer, a.Gruppe.Id)).IsNotFound);
        }

        Assert.Equal(a.Gruppe.Id, (await a.Db.GroupRegistrations.SingleAsync(r => r.Id == zeile.Id)).GroupId);
    }

    [Fact]
    public async Task Uebernehmen_ZielgruppeAusFremdemVerein_IstNichtMoeglich()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var fremderVerein = new Club { Name = "Anderer Verein" };
        a.Db.Clubs.Add(fremderVerein);
        await a.Db.SaveChangesAsync();
        var ziel = await ZielgruppeAsync(a, Guid.NewGuid(), fremderVerein.Id);
        var zeile = await a.AnmeldungAsync();

        Assert.True((await a.Dienst.TransferAsync(a.Leitung, a.Gruppe.Id, zeile.Id, ziel.Id)).IsNotFound);
        Assert.True((await a.Dienst.TransferAsync(a.Leitung, a.Gruppe.Id, zeile.Id, a.Gruppe.Id)).IsNotFound);
        Assert.Equal(a.Gruppe.Id, (await a.Db.GroupRegistrations.SingleAsync(r => r.Id == zeile.Id)).GroupId);
    }

    [Fact]
    public async Task Ziele_SindDieAnderenGruppenDesVereins()
    {
        var a = await AnmeldeAufbau.ErzeugeAsync();
        var ziel = await ZielgruppeAsync(a, Guid.NewGuid());
        var fremderVerein = new Club { Name = "Anderer Verein" };
        a.Db.Clubs.Add(fremderVerein);
        await a.Db.SaveChangesAsync();
        await ZielgruppeAsync(a, Guid.NewGuid(), fremderVerein.Id);

        var ziele = (await a.Dienst.GetTransferTargetsAsync(a.Leitung, a.Gruppe.Id)).Value!;

        var einziges = Assert.Single(ziele);
        Assert.Equal(ziel.Id, einziges.Id);
    }
}
