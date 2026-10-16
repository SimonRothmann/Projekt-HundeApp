using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Notifications;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// "Test-Push an mich": eine Meldung nur für die aufrufende Person, über den
/// normalen Weg - und nur, wenn Push eingerichtet ist und sie ein Gerät hat.
/// </summary>
public class PushTestServiceTests
{
    private static (PushTestService Dienst, FakeNotificationService Meldungen, Dogity.Infrastructure.Persistence.ApplicationDbContext Db) Aufbauen(
        string? schluessel = "BKey", string? basis = TestLinks.Basis)
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var dienst = new PushTestService(db, meldungen, new FakeNotificationChannels { PushPublicKey = schluessel }, TestLinks.Mit(basis));
        return (dienst, meldungen, db);
    }

    private static PushSubscription Geraet(Guid userId) => new()
    {
        UserId = userId,
        Endpoint = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid()}",
        P256dh = "p256dh",
        Auth = "auth",
    };

    [Fact]
    public async Task MitGeraeten_LegtEineMeldungNurFuerMichAn_UndZaehltSie()
    {
        var (dienst, meldungen, db) = Aufbauen();
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        db.PushSubscriptions.AddRange(Geraet(ich), Geraet(ich), Geraet(andere));
        await db.SaveChangesAsync();

        var ergebnis = await dienst.SendeAnMichAsync(ich);

        Assert.Equal(new TestpushErgebnisDto(PushEingerichtet: true, Geraete: 2), ergebnis);
        var meldung = Assert.Single(meldungen.Created);
        Assert.Equal(ich, meldung.UserId);
        Assert.Equal("Test-Push aus dem Adminbereich: Kommt diese Mitteilung an, funktioniert Push auf deinen Geräten.", meldung.Message);
        Assert.Equal("/admin/system", meldung.LinkPath);
    }

    [Fact]
    public async Task OhneGeraet_KeineMeldung()
    {
        var (dienst, meldungen, _) = Aufbauen();

        var ergebnis = await dienst.SendeAnMichAsync(Guid.NewGuid());

        Assert.Equal(new TestpushErgebnisDto(true, 0), ergebnis);
        Assert.Empty(meldungen.Created);
    }

    [Theory]
    [InlineData(null, TestLinks.Basis)]
    [InlineData("BKey", null)]
    public async Task OhneSchluesselOderBasisAdresse_IstPushNichtEingerichtet(string? schluessel, string? basis)
    {
        var (dienst, meldungen, db) = Aufbauen(schluessel, basis);
        var ich = Guid.NewGuid();
        db.PushSubscriptions.Add(Geraet(ich));
        await db.SaveChangesAsync();

        var ergebnis = await dienst.SendeAnMichAsync(ich);

        Assert.False(ergebnis.PushEingerichtet);
        Assert.Empty(meldungen.Created);
    }
}
