using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Der Versand an die Geräte einer Person - mit einem Sender, der nie ins Netz
/// geht und die Antworten des Push-Dienstes (404/410) nachstellt.
/// </summary>
public class PushDispatcherTests
{
    private const string Handy = "https://web.push.apple.com/handy";
    private const string Rechner = "https://fcm.googleapis.com/fcm/send/rechner";

    private static (PushDispatcher Dispatcher, Dogity.Infrastructure.Persistence.ApplicationDbContext Db, FakePushSender Sender, Guid UserId) Aufbauen()
    {
        var db = InMemoryDbContext.Create();
        var sender = new FakePushSender();
        var userId = Guid.NewGuid();
        db.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = Handy, P256dh = "p1", Auth = "a1" });
        db.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = Rechner, P256dh = "p2", Auth = "a2" });
        db.PushSubscriptions.Add(new PushSubscription { UserId = Guid.NewGuid(), Endpoint = "https://fcm.googleapis.com/fcm/send/fremd", P256dh = "p3", Auth = "a3" });
        db.SaveChanges();
        return (new PushDispatcher(db, sender, NullLogger<PushDispatcher>.Instance), db, sender, userId);
    }

    [Fact]
    public async Task Sendet_AnAlleGeraeteDerPerson_UndNurAnDie()
    {
        var (dispatcher, db, sender, userId) = Aufbauen();

        await dispatcher.SendeAsync(new PushAuftrag(userId, "{\"web_push\":8030}"));

        Assert.Equal(2, sender.Gesendet.Count);
        Assert.All(sender.Gesendet, g => Assert.Equal("{\"web_push\":8030}", g.Payload));
        Assert.DoesNotContain(sender.Gesendet, g => g.Ziel.Endpoint.EndsWith("fremd"));
        Assert.All(await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync(), s => Assert.NotNull(s.LastUsedAt));
    }

    [Fact]
    public async Task EntferntGeraete_DieDerPushDienstNichtMehrKennt()
    {
        var (dispatcher, db, sender, userId) = Aufbauen();
        // 404/410 übersetzt WebPushSender in "ungültig" (siehe WebPushSenderTests).
        sender.Ergebnisse[Handy] = PushErgebnis.AboUngueltig;

        await dispatcher.SendeAsync(new PushAuftrag(userId, "{}"));

        var uebrig = await db.PushSubscriptions.IgnoreQueryFilters().Where(s => s.UserId == userId).ToListAsync();
        Assert.Equal(Rechner, Assert.Single(uebrig).Endpoint);
    }

    [Fact]
    public async Task EinSendefehler_BehaeltDasGeraet_UndStoertDieAnderenNicht()
    {
        var (dispatcher, db, sender, userId) = Aufbauen();
        sender.Ergebnisse[Handy] = PushErgebnis.Fehler;

        await dispatcher.SendeAsync(new PushAuftrag(userId, "{}"));

        var abos = await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();
        Assert.Equal(2, abos.Count);
        Assert.Null(abos.Single(s => s.Endpoint == Handy).LastUsedAt);
        Assert.NotNull(abos.Single(s => s.Endpoint == Rechner).LastUsedAt);
    }

    [Fact]
    public async Task EineAusnahmeBeimSenden_StoptDieUebrigenGeraeteNicht()
    {
        var (dispatcher, db, sender, userId) = Aufbauen();
        sender.Wirft.Add(Handy);

        await dispatcher.SendeAsync(new PushAuftrag(userId, "{}"));

        Assert.Equal(2, sender.Gesendet.Count);
        Assert.Equal(2, await db.PushSubscriptions.CountAsync(s => s.UserId == userId));
    }

    [Fact]
    public async Task OhneGeraete_TutNichts()
    {
        var (dispatcher, _, sender, _) = Aufbauen();

        await dispatcher.SendeAsync(new PushAuftrag(Guid.NewGuid(), "{}"));

        Assert.Empty(sender.Gesendet);
    }
}
