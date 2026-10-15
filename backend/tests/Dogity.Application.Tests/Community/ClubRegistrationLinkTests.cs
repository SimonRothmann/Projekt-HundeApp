using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Wohin die Meldung über einen neuen Vereinsantrag führt: seit der
/// Aufteilung des Adminbereichs auf die Vereine-Seite, nicht mehr auf die
/// Übersicht. (Alte Meldungen mit "/admin" bleiben gültig - die Übersicht
/// zeigt offene Anträge ebenfalls.)
/// </summary>
public class ClubRegistrationLinkTests
{
    [Fact]
    public async Task Antrag_BenachrichtigtAdmins_MitLinkZurVereineSeite()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var adminId = Guid.NewGuid();
        var antragstellerId = Guid.NewGuid();
        lookup.Register(adminId, "admin@test.de");
        lookup.Register(antragstellerId, "antrag@test.de");
        lookup.Rollen["ADMIN"] = [adminId];
        var service = new ClubRegistrationService(db, lookup, meldungen, new TrainerRoleService(db, lookup));

        var result = await service.RequestAsync(antragstellerId, new CreateClubRegistrationRequest("Hundesportverein Test", null));

        Assert.True(result.Succeeded);
        var meldung = Assert.Single(meldungen.Created);
        Assert.Equal(adminId, meldung.UserId);
        Assert.Equal("/admin/vereine", meldung.LinkPath);
    }
}
