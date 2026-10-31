using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Der Vereinsantrag: stellen, freigeben, ablehnen. Dass nur der Admin freigeben
/// darf, erzwingt der Controller per Attribut (AdminControllerRechteTests);
/// hier stehen die Fachregeln des Dienstes.
/// </summary>
public class ClubRegistrationServiceTests
{
    private sealed record Welt(
        ApplicationDbContext Db,
        FakeNotificationService Meldungen,
        ClubRegistrationService Dienst,
        ClubService Vereine,
        Guid Antragsteller,
        Guid Admin);

    private static Welt Aufbauen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var antragsteller = Guid.NewGuid();
        var admin = Guid.NewGuid();
        lookup.Register(antragsteller, "antrag@test.de", "Anna", "Antrag");
        lookup.Register(admin, "admin@test.de");
        lookup.Rollen["ADMIN"] = [admin];
        var rollen = new TrainerRoleService(db, lookup);
        return new Welt(db, meldungen, new ClubRegistrationService(db, lookup, meldungen, rollen),
            new ClubService(db, lookup, meldungen, rollen), antragsteller, admin);
    }

    private static async Task<Guid> AntragAsync(Welt w, string name)
    {
        var antrag = await w.Dienst.RequestAsync(w.Antragsteller, new CreateClubRegistrationRequest(name, null));
        Assert.True(antrag.Succeeded, string.Join(" ", antrag.Errors));
        w.Meldungen.Created.Clear();
        return antrag.Value!.Id;
    }

    // ---------------------------------------------------------------- Name

    [Fact]
    public async Task Antrag_NameBisZurVereinsgrenze_GehtDurch_DarueberNicht()
    {
        var w = Aufbauen();

        var genau = await w.Dienst.RequestAsync(w.Antragsteller, new CreateClubRegistrationRequest(new string('a', Textlaengen.VereinsName), null));
        Assert.True(genau.Succeeded);

        // Die Antragsspalte ließe 200 zu - aber über der Grenze des Vereins ließe er sich nicht freigeben.
        var w2 = Aufbauen();
        var zuLang = await w2.Dienst.RequestAsync(w2.Antragsteller, new CreateClubRegistrationRequest(new string('a', Textlaengen.VereinsName + 1), null));
        Assert.False(zuLang.Succeeded);
        Assert.Contains("zu lang", zuLang.Errors[0]);
        Assert.Empty(w2.Db.ClubRegistrations);
    }

    [Fact]
    public async Task Antrag_OhneName_Scheitert()
    {
        var w = Aufbauen();

        Assert.False((await w.Dienst.RequestAsync(w.Antragsteller, new CreateClubRegistrationRequest("   ", null))).Succeeded);
    }

    [Fact]
    public async Task Antrag_FuerEinenVorhandenenVereinsnamen_VerweistAufDenBeitritt()
    {
        var w = Aufbauen();
        w.Db.Clubs.Add(new Club { Name = "Hundesportverein Nord" });
        await w.Db.SaveChangesAsync();

        var antrag = await w.Dienst.RequestAsync(w.Antragsteller, new CreateClubRegistrationRequest("hundesportverein nord", null));

        Assert.False(antrag.Succeeded);
        Assert.Empty(w.Db.ClubRegistrations);
    }

    [Fact]
    public async Task Antrag_NurEinOffenerJePerson()
    {
        var w = Aufbauen();
        await AntragAsync(w, "Verein Eins");

        var zweiter = await w.Dienst.RequestAsync(w.Antragsteller, new CreateClubRegistrationRequest("Verein Zwei", null));

        Assert.False(zweiter.Succeeded);
    }

    [Fact]
    public async Task Vereinsanlage_DurchDenAdmin_PruftDieLaengeDesNamens()
    {
        var w = Aufbauen();

        var zuLang = await w.Vereine.CreateAsync(new CreateClubRequest(new string('b', Textlaengen.VereinsName + 1), null));
        Assert.False(zuLang.Succeeded);
        Assert.Empty(w.Db.Clubs);

        var passt = await w.Vereine.CreateAsync(new CreateClubRequest(new string('b', Textlaengen.VereinsName), null));
        Assert.True(passt.Succeeded);
    }

    // ------------------------------------------------------------ Freigabe

    [Fact]
    public async Task Freigabe_LegtVereinUndVerwaltungAn_Und_MeldetDemAntragsteller()
    {
        var w = Aufbauen();
        var antragId = await AntragAsync(w, "Hundesportverein Süd");

        var ergebnis = await w.Dienst.ApproveAsync(w.Admin, antragId);

        Assert.True(ergebnis.Succeeded);
        var verein = await w.Db.Clubs.SingleAsync();
        Assert.Equal("Hundesportverein Süd", verein.Name);
        // Der Antragsteller verwaltet den Verein, sonst gäbe es einen Verein, den niemand betreut.
        var rolle = await w.Db.ClubTrainers.SingleAsync(t => t.ClubId == verein.Id);
        Assert.Equal(w.Antragsteller, rolle.UserId);
        Assert.Equal(ClubRole.Verwaltung, rolle.Role);
        var antrag = await w.Db.ClubRegistrations.SingleAsync();
        Assert.Equal(ClubRegistrationStatus.Approved, antrag.Status);
        Assert.Equal(verein.Id, antrag.ClubId);
        Assert.Equal(w.Admin, antrag.DecidedByUserId);
        var meldung = Assert.Single(w.Meldungen.Created);
        Assert.Equal(w.Antragsteller, meldung.UserId);
    }

    [Fact]
    public async Task Freigabe_ZweiterAufruf_ScheitertOhneZweitenVerein()
    {
        var w = Aufbauen();
        var antragId = await AntragAsync(w, "Hundesportverein Süd");
        Assert.True((await w.Dienst.ApproveAsync(w.Admin, antragId)).Succeeded);

        var nochmal = await w.Dienst.ApproveAsync(w.Admin, antragId);

        Assert.False(nochmal.Succeeded);
        Assert.Equal("Über diesen Antrag wurde bereits entschieden.", nochmal.Errors[0]);
        Assert.Single(w.Db.Clubs);
    }

    [Fact]
    public async Task Freigabe_NamensdubletteSeitDemAntrag_Scheitert()
    {
        var w = Aufbauen();
        var antragId = await AntragAsync(w, "Hundesportverein Süd");
        // Jemand legt den Verein zwischen Antrag und Freigabe an (Groß-/Kleinschreibung zählt nicht).
        w.Db.Clubs.Add(new Club { Name = "HUNDESPORTVEREIN SÜD" });
        await w.Db.SaveChangesAsync();

        var ergebnis = await w.Dienst.ApproveAsync(w.Admin, antragId);

        Assert.False(ergebnis.Succeeded);
        Assert.Single(w.Db.Clubs);
        Assert.Equal(ClubRegistrationStatus.Pending, (await w.Db.ClubRegistrations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Freigabe_NameLaengerAlsEinVereinsname_ScheitertStattMitEinemServerfehler()
    {
        var w = Aufbauen();
        // Ein Antrag aus der Zeit, als bis 200 Zeichen erlaubt waren.
        var antrag = new ClubRegistration { Name = new string('c', Textlaengen.VereinsName + 20), RequestedByUserId = w.Antragsteller };
        w.Db.ClubRegistrations.Add(antrag);
        await w.Db.SaveChangesAsync();

        var ergebnis = await w.Dienst.ApproveAsync(w.Admin, antrag.Id);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(w.Db.Clubs);
        Assert.Equal(ClubRegistrationStatus.Pending, (await w.Db.ClubRegistrations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Freigabe_UnbekannterAntrag_NotFound()
    {
        var w = Aufbauen();

        var ergebnis = await w.Dienst.ApproveAsync(w.Admin, Guid.NewGuid());

        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
    }

    // ------------------------------------------------------------ Ablehnung

    [Fact]
    public async Task Ablehnung_MerktSichDieBegruendung_Und_MeldetDemAntragsteller()
    {
        var w = Aufbauen();
        var antragId = await AntragAsync(w, "Hundesportverein Süd");

        var ergebnis = await w.Dienst.RejectAsync(w.Admin, antragId, new DecideClubRegistrationRequest("  Gibt es schon unter anderem Namen.  "));

        Assert.True(ergebnis.Succeeded);
        var antrag = await w.Db.ClubRegistrations.SingleAsync();
        Assert.Equal(ClubRegistrationStatus.Rejected, antrag.Status);
        Assert.Equal("Gibt es schon unter anderem Namen.", antrag.DecisionNote);
        Assert.Empty(w.Db.Clubs);
        var meldung = Assert.Single(w.Meldungen.Created);
        Assert.Equal(w.Antragsteller, meldung.UserId);
        Assert.Contains("Begründung", meldung.Message);
    }

    [Fact]
    public async Task Ablehnung_OhneBegruendung_MeldetOhneBegruendung()
    {
        var w = Aufbauen();
        var antragId = await AntragAsync(w, "Hundesportverein Süd");

        await w.Dienst.RejectAsync(w.Admin, antragId, new DecideClubRegistrationRequest("   "));

        Assert.Null((await w.Db.ClubRegistrations.SingleAsync()).DecisionNote);
        Assert.DoesNotContain("Begründung", Assert.Single(w.Meldungen.Created).Message);
    }

    [Fact]
    public async Task Ablehnung_BereitsEntschiedenerAntrag_Scheitert()
    {
        var w = Aufbauen();
        var antragId = await AntragAsync(w, "Hundesportverein Süd");
        Assert.True((await w.Dienst.ApproveAsync(w.Admin, antragId)).Succeeded);

        var ergebnis = await w.Dienst.RejectAsync(w.Admin, antragId, new DecideClubRegistrationRequest(null));

        Assert.False(ergebnis.Succeeded);
        Assert.Equal(ClubRegistrationStatus.Approved, (await w.Db.ClubRegistrations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Ablehnung_UnbekannterAntrag_NotFound()
    {
        var w = Aufbauen();

        var ergebnis = await w.Dienst.RejectAsync(w.Admin, Guid.NewGuid(), new DecideClubRegistrationRequest(null));

        Assert.True(ergebnis.IsNotFound);
    }

    // -------------------------------------------------------------- Listen

    [Fact]
    public async Task OffeneAntraege_ZeigenNurOffeneUndDenAntragsteller_AelteresZuerst()
    {
        var w = Aufbauen();
        var erster = await AntragAsync(w, "Verein Eins");
        Assert.True((await w.Dienst.RejectAsync(w.Admin, erster, new DecideClubRegistrationRequest(null))).Succeeded);
        var zweiter = await AntragAsync(w, "Verein Zwei");

        var offene = (await w.Dienst.GetPendingAsync()).Value!;

        var einziger = Assert.Single(offene);
        Assert.Equal(zweiter, einziger.Id);
        Assert.Equal("antrag@test.de", einziger.RequestedByEmail);
        Assert.Equal("Anna Antrag", einziger.RequestedByName);
    }

    [Fact]
    public async Task MeineAntraege_ZeigenNurDieEigenen()
    {
        var w = Aufbauen();
        await AntragAsync(w, "Verein Eins");
        w.Db.ClubRegistrations.Add(new ClubRegistration { Name = "Fremder Verein", RequestedByUserId = Guid.NewGuid() });
        await w.Db.SaveChangesAsync();

        var meine = (await w.Dienst.GetMineAsync(w.Antragsteller)).Value!;

        Assert.Equal("Verein Eins", Assert.Single(meine).Name);
    }
}
