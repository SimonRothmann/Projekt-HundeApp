using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Betreuungen und Vereinsrollen: Das Betreuen fremder Gruppen hängt an
/// <see cref="ClubRollen.AlleGruppen"/>. Mit der Rolle endet auch, was sie
/// möglich gemacht hat - und Betreuungen, die über keine Gruppe mehr bestehen
/// (auch Altzeilen aus der Zeit vor den Vereinsrollen), kann die Gruppenleitung
/// beenden.
/// </summary>
public class BetreuungUndVereinsrollenTests
{
    private sealed record Welt(
        ApplicationDbContext Db,
        ClubService Vereine,
        GroupService Gruppen,
        Club Verein,
        Group Gruppe,
        Guid Leitung,
        Guid Mitglied,
        Guid HundId);

    /// <summary>Ein Verein mit einer Gruppe unter fremder Leitung; darin ein aktives Mitglied mit Hund.</summary>
    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var rollen = new TrainerRoleService(db, nutzer);
        var verein = new Club { Name = "Hundesportverein Nord" };
        db.Clubs.Add(verein);
        var leitung = Guid.NewGuid();
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = leitung, Role = ClubRole.Training });
        var gruppe = new Group { ClubId = verein.Id, TrainerId = leitung, Name = "Dienstagsgruppe" };
        db.Groups.Add(gruppe);
        var mitglied = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = mitglied });
        db.ClubMemberships.Add(new ClubMembership { ClubId = verein.Id, UserId = mitglied, Status = ClubMembershipStatus.Approved });
        db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Id, UserId = mitglied, Status = GroupMemberStatus.Active });
        await db.SaveChangesAsync();
        return new Welt(
            db,
            new ClubService(db, nutzer, meldungen, rollen),
            new GroupService(db, nutzer, rollen, meldungen, TimeProvider.System),
            verein, gruppe, leitung, mitglied, hund.Id);
    }

    private static async Task<Guid> PersonMitRolleAsync(Welt w, ClubRole rollen)
    {
        var id = Guid.NewGuid();
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = id, Role = rollen });
        await w.Db.SaveChangesAsync();
        return id;
    }

    /// <summary>Die Betreuung, so wie die Gruppenverwaltung sie anlegt (die Person darf die Gruppe verwalten).</summary>
    private static async Task BetreuenAsync(Welt w, Guid trainer)
    {
        var ergebnis = await w.Gruppen.AssignTrainerToDogAsync(trainer, w.Gruppe.Id, new AssignTrainerRequest(w.Mitglied, w.HundId));
        Assert.True(ergebnis.Succeeded, string.Join(" ", ergebnis.Errors));
        Assert.True(await w.Db.HasDogAccessAsync(trainer, w.HundId));
    }

    /// <summary>Eine Betreuung, wie sie vor den Vereinsrollen über die damals für alle Gruppen zuständige Trainer-Rolle entstand.</summary>
    private static async Task AltzeileAsync(Welt w, Guid trainer)
    {
        w.Db.TrainerAssignments.Add(new TrainerAssignment
        {
            TrainerId = trainer,
            MemberId = w.Mitglied,
            DogId = w.HundId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await w.Db.SaveChangesAsync();
    }

    // ---------------------------------------------------- N1 (a): Rolle fällt weg

    [Fact]
    public async Task Herabstufung_AusAusbildungswart_BeendetDieBetreuungFremderGruppen()
    {
        var w = await AufbauenAsync();
        var ausbildungswart = await PersonMitRolleAsync(w, ClubRole.Ausbildungswart);
        await BetreuenAsync(w, ausbildungswart);

        var ergebnis = await w.Vereine.UpdateTrainerRoleAsync(Guid.NewGuid(), true, w.Verein.Id, ausbildungswart, ClubRole.Vorstand);

        Assert.True(ergebnis.Succeeded);
        Assert.False(await w.Db.HasDogAccessAsync(ausbildungswart, w.HundId));
    }

    [Fact]
    public async Task Abberufen_BeendetDieBetreuungFremderGruppen()
    {
        var w = await AufbauenAsync();
        var verwaltung = await PersonMitRolleAsync(w, ClubRole.Verwaltung);
        await BetreuenAsync(w, verwaltung);

        // Verwaltung ist über Training hinaus Verwaltung: ein zweiter Verwalter darf sie abberufen.
        await PersonMitRolleAsync(w, ClubRole.Verwaltung);
        var ergebnis = await w.Vereine.RemoveTrainerAsync(Guid.NewGuid(), true, w.Verein.Id, verwaltung);

        Assert.True(ergebnis.Succeeded);
        Assert.False(await w.Db.HasDogAccessAsync(verwaltung, w.HundId));
    }

    [Fact]
    public async Task Herabstufung_LaesstEineBetreuungUeberEigeneGruppeBestehen()
    {
        var w = await AufbauenAsync();
        var ausbildungswart = await PersonMitRolleAsync(w, ClubRole.Ausbildungswart);
        // Dieselbe Person leitet eine zweite Gruppe, in der das Mitglied ebenfalls ist.
        var eigene = new Group { ClubId = w.Verein.Id, TrainerId = ausbildungswart, Name = "Donnerstagsgruppe" };
        w.Db.Groups.Add(eigene);
        w.Db.GroupMembers.Add(new GroupMember { GroupId = eigene.Id, UserId = w.Mitglied, Status = GroupMemberStatus.Active });
        await w.Db.SaveChangesAsync();
        await BetreuenAsync(w, ausbildungswart);

        await w.Vereine.UpdateTrainerRoleAsync(Guid.NewGuid(), true, w.Verein.Id, ausbildungswart, ClubRole.Training);

        Assert.True(await w.Db.HasDogAccessAsync(ausbildungswart, w.HundId));
    }

    [Fact]
    public async Task Herabstufung_LaesstDieBetreuungBestehen_WennAlleGruppenBleibt()
    {
        var w = await AufbauenAsync();
        var ausbildungswart = await PersonMitRolleAsync(w, ClubRole.Ausbildungswart | ClubRole.Vorstand);
        await BetreuenAsync(w, ausbildungswart);

        await w.Vereine.UpdateTrainerRoleAsync(Guid.NewGuid(), true, w.Verein.Id, ausbildungswart, ClubRole.Ausbildungswart);

        Assert.True(await w.Db.HasDogAccessAsync(ausbildungswart, w.HundId));
    }

    [Fact]
    public async Task Herabstufung_RuehrtBetreuungenAusAnderenVereinenNichtAn()
    {
        var w = await AufbauenAsync();
        var ausbildungswart = await PersonMitRolleAsync(w, ClubRole.Ausbildungswart);
        // Ein Mitglied eines ANDEREN Vereins, das diese Person über ihre Gruppe dort betreut.
        var anderer = new Club { Name = "Hundesportverein Süd" };
        var andereGruppe = new Group { ClubId = anderer.Id, TrainerId = ausbildungswart, Name = "Südgruppe" };
        var fremdesMitglied = Guid.NewGuid();
        var fremderHund = new Dog { Name = "Rex" };
        w.Db.Clubs.Add(anderer);
        w.Db.Groups.Add(andereGruppe);
        w.Db.Dogs.Add(fremderHund);
        w.Db.DogOwners.Add(new DogOwner { DogId = fremderHund.Id, UserId = fremdesMitglied });
        w.Db.GroupMembers.Add(new GroupMember { GroupId = andereGruppe.Id, UserId = fremdesMitglied, Status = GroupMemberStatus.Active });
        await w.Db.SaveChangesAsync();
        Assert.True((await w.Gruppen.AssignTrainerToDogAsync(ausbildungswart, andereGruppe.Id, new AssignTrainerRequest(fremdesMitglied, fremderHund.Id))).Succeeded);

        await w.Vereine.UpdateTrainerRoleAsync(Guid.NewGuid(), true, w.Verein.Id, ausbildungswart, ClubRole.Vorstand);

        Assert.True(await w.Db.HasDogAccessAsync(ausbildungswart, fremderHund.Id));
    }

    // ------------------------------------------- N1 (b): Gruppenleitung beendet Altzeilen

    [Fact]
    public async Task Gruppenleitung_BeendetEineAltzeile_AuchOhneAlleGruppenRolle()
    {
        var w = await AufbauenAsync();
        var training = await PersonMitRolleAsync(w, ClubRole.Training);
        await AltzeileAsync(w, training);
        Assert.True(await w.Db.HasDogAccessAsync(training, w.HundId));

        var ergebnis = await w.Gruppen.RemoveTrainerFromDogAsync(w.Leitung, w.Gruppe.Id, training, w.HundId);

        Assert.True(ergebnis.Succeeded);
        Assert.False(await w.Db.HasDogAccessAsync(training, w.HundId));
    }

    [Fact]
    public async Task Gruppenleitung_BeendetKeineBetreuungDurchEineFremdePerson()
    {
        var w = await AufbauenAsync();
        // Keine Rolle, keine Mitgliedschaft in diesem Verein.
        var fremde = Guid.NewGuid();
        await AltzeileAsync(w, fremde);

        var ergebnis = await w.Gruppen.RemoveTrainerFromDogAsync(w.Leitung, w.Gruppe.Id, fremde, w.HundId);

        Assert.False(ergebnis.Succeeded);
        Assert.True(await w.Db.HasDogAccessAsync(fremde, w.HundId));
    }

    [Fact]
    public async Task GruppenaustrittDesMitglieds_BeendetAuchAltzeilenDesVereins()
    {
        var w = await AufbauenAsync();
        var training = await PersonMitRolleAsync(w, ClubRole.Training);
        await AltzeileAsync(w, training);

        var ergebnis = await w.Gruppen.LeaveGroupAsync(w.Mitglied, w.Gruppe.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.False(await w.Db.HasDogAccessAsync(training, w.HundId));
    }

    [Fact]
    public async Task GruppenaustrittDesMitglieds_LaesstDieBetreuungUeberEineAndereGemeinsameGruppeBestehen()
    {
        var w = await AufbauenAsync();
        var training = await PersonMitRolleAsync(w, ClubRole.Training);
        var zweite = new Group { ClubId = w.Verein.Id, TrainerId = training, Name = "Donnerstagsgruppe" };
        w.Db.Groups.Add(zweite);
        w.Db.GroupMembers.Add(new GroupMember { GroupId = zweite.Id, UserId = w.Mitglied, Status = GroupMemberStatus.Active });
        await w.Db.SaveChangesAsync();
        await AltzeileAsync(w, training);

        await w.Gruppen.LeaveGroupAsync(w.Mitglied, w.Gruppe.Id);

        Assert.True(await w.Db.HasDogAccessAsync(training, w.HundId));
    }

    [Fact]
    public async Task GruppenaustrittDesMitglieds_RuehrtBetreuungDurchAndereVereineNichtAn()
    {
        var w = await AufbauenAsync();
        var aussenstehende = Guid.NewGuid();
        await AltzeileAsync(w, aussenstehende);

        await w.Gruppen.LeaveGroupAsync(w.Mitglied, w.Gruppe.Id);

        Assert.True(await w.Db.HasDogAccessAsync(aussenstehende, w.HundId));
    }
}
