using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Erreichbarkeit je Vereinsrolle (Paket V3): Was die Oberfläche einer Rolle als
/// Offenes zeigt, folgt dem Recht und nicht der Trainer-Rolle - die
/// Mitgliederverwaltung sieht die Beitrittsanfragen ihres Vereins, ohne
/// Trainer:in zu sein, und die reine Training-Rolle sieht sie nicht.
/// </summary>
public class VereinsrollenErreichbarkeitTests
{
    private static async Task<(Dogity.Infrastructure.Persistence.ApplicationDbContext Db, Club Verein, Guid Person)> PersonMitRolleAsync(ClubRole rollen)
    {
        var db = InMemoryDbContext.Create();
        var verein = new Club { Name = "Testverein" };
        db.Clubs.Add(verein);
        var person = Guid.NewGuid();
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = person, Role = rollen });
        db.ClubMemberships.AddRange(
            new ClubMembership { ClubId = verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Pending },
            new ClubMembership { ClubId = verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Pending },
            new ClubMembership { ClubId = verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Approved });
        await db.SaveChangesAsync();
        return (db, verein, person);
    }

    [Theory]
    [InlineData(ClubRole.Training, 0)]
    [InlineData(ClubRole.Ausbildungswart, 0)]
    [InlineData(ClubRole.Vorstand, 0)]
    [InlineData(ClubRole.Training | ClubRole.Vorstand, 0)]
    [InlineData(ClubRole.Mitgliederverwaltung, 2)]
    [InlineData(ClubRole.Verwaltung, 2)]
    [InlineData(ClubRole.Training | ClubRole.Mitgliederverwaltung, 2)]
    public async Task Vereinsanfragen_Zaehlen_Nach_Dem_Recht_Und_Nicht_Nach_Der_Trainer_Rolle(ClubRole rollen, int erwartet)
    {
        var (db, _, person) = await PersonMitRolleAsync(rollen);

        var result = await new TrainerOpenCountsService(db).GetAsync(person);

        Assert.Equal(erwartet, result.Value!.ClubJoinRequests);
    }

    [Fact]
    public async Task Mitgliederverwaltung_Ohne_Trainer_Rolle_Und_Ohne_Gruppen_Sieht_Trotzdem_Die_Vereinsanfragen()
    {
        var (db, _, person) = await PersonMitRolleAsync(ClubRole.Mitgliederverwaltung);

        var zahlen = (await new TrainerOpenCountsService(db).GetAsync(person)).Value!;

        // Keine Trainer:in: weder Gruppenanfragen noch etwas zu bewerten - nur die Vereinsanfragen.
        Assert.False(await db.IsAnyTrainerAsync(person));
        Assert.Equal(0, zahlen.GroupJoinRequests);
        Assert.Equal(0, zahlen.SessionsToRate);
        Assert.Equal(2, zahlen.ClubJoinRequests);
    }

    [Theory]
    [InlineData(ClubRole.Mitgliederverwaltung, true)]
    [InlineData(ClubRole.Verwaltung, true)]
    [InlineData(ClubRole.Training | ClubRole.Mitgliederverwaltung, true)]
    [InlineData(ClubRole.Training, false)]
    [InlineData(ClubRole.Ausbildungswart, false)]
    [InlineData(ClubRole.Vorstand, false)]
    public async Task ManagesMembers_Folgt_Dem_Recht_Mitglieder_Verwalten(ClubRole rollen, bool erwartet)
    {
        var (db, _, person) = await PersonMitRolleAsync(rollen);
        var vereine = new ClubService(db, new FakeUserLookupService(), new FakeNotificationService(), new TrainerRoleService(db, new FakeUserLookupService()));

        Assert.Equal(erwartet, await vereine.ManagesMembersAsync(person));
    }

    [Fact]
    public async Task ManagesMembers_Ohne_Rolle_Oder_Nach_Entzug_Ist_Falsch()
    {
        var (db, verein, person) = await PersonMitRolleAsync(ClubRole.Mitgliederverwaltung);
        var vereine = new ClubService(db, new FakeUserLookupService(), new FakeNotificationService(), new TrainerRoleService(db, new FakeUserLookupService()));

        Assert.False(await vereine.ManagesMembersAsync(Guid.NewGuid()));

        // Entzogene Rollen (Soft Delete) zählen nicht mehr.
        db.ClubTrainers.Single(t => t.ClubId == verein.Id && t.UserId == person).DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.False(await vereine.ManagesMembersAsync(person));
    }

    [Fact]
    public async Task Beitrittsanfrage_Geht_An_Die_Mitgliederverwaltung_Mit_Dem_Ziel_Anfragen_Und_Nicht_An_Training()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var verein = new Club { Name = "Testverein" };
        db.Clubs.Add(verein);
        var verwaltung = Guid.NewGuid();
        var mitglieder = Guid.NewGuid();
        var training = Guid.NewGuid();
        var vorstand = Guid.NewGuid();
        db.ClubTrainers.AddRange(
            new ClubTrainer { ClubId = verein.Id, UserId = verwaltung, Role = ClubRole.Verwaltung },
            new ClubTrainer { ClubId = verein.Id, UserId = mitglieder, Role = ClubRole.Mitgliederverwaltung },
            new ClubTrainer { ClubId = verein.Id, UserId = training, Role = ClubRole.Training },
            new ClubTrainer { ClubId = verein.Id, UserId = vorstand, Role = ClubRole.Vorstand });
        await db.SaveChangesAsync();
        var vereine = new ClubService(db, nutzer, meldungen, new TrainerRoleService(db, nutzer));

        var antwort = await vereine.RequestJoinAsync(Guid.NewGuid(), verein.Id);

        Assert.True(antwort.Succeeded);
        // Genau die, die die Anfrage entscheiden dürfen - das Ziel erreichen sie jetzt auch ohne Trainer-Rolle.
        Assert.Equal(2, meldungen.Created.Count);
        Assert.Contains(meldungen.Created, m => m.UserId == verwaltung);
        Assert.Contains(meldungen.Created, m => m.UserId == mitglieder);
        Assert.All(meldungen.Created, m => Assert.Equal("/trainer/anfragen", m.LinkPath));
    }
}
