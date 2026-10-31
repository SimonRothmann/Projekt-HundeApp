using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Wer eine Sportart anlegen darf: globale nur der Admin, vereinseigene nur Trainer:innen
/// des Vereins (jede Trainer-Rolle, nicht Mitgliederverwaltung oder Vorstand) - und kein
/// Verein legt etwas in einem fremden Verein an.
/// </summary>
public class CreateSportRechteTests
{
    private sealed record Welt(ApplicationDbContext Db, SportCatalogService Dienst, Club Verein, Club Fremd);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var verein = new Club { Name = "Mein Verein" };
        var fremd = new Club { Name = "Fremder Verein" };
        db.Clubs.AddRange(verein, fremd);
        await db.SaveChangesAsync();
        return new Welt(db, new SportCatalogService(db, TimeProvider.System), verein, fremd);
    }

    private static async Task<Guid> MitRolleAsync(Welt w, Club verein, ClubRole rolle)
    {
        var id = Guid.NewGuid();
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = id, Role = rolle });
        await w.Db.SaveChangesAsync();
        return id;
    }

    private static CreateSportRequest Anfrage(Guid? verein, string code = "NEU") => new(code, "Neue Sportart", null, verein);

    [Fact]
    public async Task GlobaleSportart_NurDerAdmin()
    {
        var w = await AufbauenAsync();
        var trainer = await MitRolleAsync(w, w.Verein, ClubRole.Verwaltung);

        var abgelehnt = await w.Dienst.CreateSportAsync(trainer, isAdmin: false, Anfrage(null));
        var erlaubt = await w.Dienst.CreateSportAsync(Guid.NewGuid(), isAdmin: true, Anfrage(null));

        Assert.False(abgelehnt.Succeeded);
        Assert.True(erlaubt.Succeeded);
        Assert.Null(erlaubt.Value!.ClubId);
        Assert.Single(w.Db.Sports);
    }

    [Theory]
    [InlineData(ClubRole.Training)]
    [InlineData(ClubRole.Ausbildungswart)]
    [InlineData(ClubRole.Verwaltung)]
    public async Task VereinsSportart_DieTrainerRollenDesVereinsDuerfen(ClubRole rolle)
    {
        var w = await AufbauenAsync();
        var trainer = await MitRolleAsync(w, w.Verein, rolle);

        var ergebnis = await w.Dienst.CreateSportAsync(trainer, isAdmin: false, Anfrage(w.Verein.Id));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(w.Verein.Id, ergebnis.Value!.ClubId);
    }

    [Theory]
    [InlineData(ClubRole.Mitgliederverwaltung)]
    [InlineData(ClubRole.Vorstand)]
    public async Task VereinsSportart_MitgliederverwaltungUndVorstandDuerfenNicht(ClubRole rolle)
    {
        var w = await AufbauenAsync();
        var person = await MitRolleAsync(w, w.Verein, rolle);

        Assert.False((await w.Dienst.CreateSportAsync(person, isAdmin: false, Anfrage(w.Verein.Id))).Succeeded);
        Assert.Empty(w.Db.Sports);
    }

    [Fact]
    public async Task VereinsSportart_EineFremdeTrainerinDarfNichtFuerDenVerein_UndEinMitgliedAuchNicht()
    {
        var w = await AufbauenAsync();
        var fremdeTrainerin = await MitRolleAsync(w, w.Fremd, ClubRole.Verwaltung);
        var mitglied = Guid.NewGuid();
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();

        Assert.False((await w.Dienst.CreateSportAsync(fremdeTrainerin, isAdmin: false, Anfrage(w.Verein.Id))).Succeeded);
        Assert.False((await w.Dienst.CreateSportAsync(mitglied, isAdmin: false, Anfrage(w.Verein.Id))).Succeeded);
        Assert.Empty(w.Db.Sports);
    }

    [Fact]
    public async Task VereinsSportart_DerAdminDarfNichtOhneRolleFuerEinenVereinAnlegen()
    {
        // Der Admin-Weg gilt für globale Sportarten; Vereinssportarten gehören den Trainer:innen des Vereins.
        var w = await AufbauenAsync();

        Assert.False((await w.Dienst.CreateSportAsync(Guid.NewGuid(), isAdmin: true, Anfrage(w.Verein.Id))).Succeeded);
    }

    [Fact]
    public async Task Code_MussImSichtbarkeitsbereichEindeutigSein()
    {
        var w = await AufbauenAsync();
        var trainer = await MitRolleAsync(w, w.Verein, ClubRole.Training);
        var andererTrainer = await MitRolleAsync(w, w.Fremd, ClubRole.Training);
        Assert.True((await w.Dienst.CreateSportAsync(trainer, false, Anfrage(w.Verein.Id, "RALLY"))).Succeeded);

        // Im selben Verein doppelt: nein. Im anderen Verein derselbe Code: ja.
        Assert.False((await w.Dienst.CreateSportAsync(trainer, false, Anfrage(w.Verein.Id, "RALLY"))).Succeeded);
        Assert.True((await w.Dienst.CreateSportAsync(andererTrainer, false, Anfrage(w.Fremd.Id, "RALLY"))).Succeeded);
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("CODE", "  ")]
    public async Task CodeUndName_SindPflicht(string code, string name)
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.CreateSportAsync(Guid.NewGuid(), isAdmin: true, new CreateSportRequest(code, name, null, null));

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(w.Db.Sports);
    }
}
