using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Was NotificationService nach dem Anlegen der Glockenmeldung einreiht: eine
/// Mail nur bei "wichtig" (und nur, wenn eingerichtet und nicht abgeschaltet),
/// Push nur mit Gerät. Die Glocke bleibt in jedem Fall.
/// </summary>
public class NotificationVersandTests
{
    private sealed record Umgebung(
        ApplicationDbContext Db,
        FakeUserLookupService Lookup,
        FakeNotificationChannels Kanaele,
        FakeMailQueue Mails,
        FakePushQueue Pushs,
        NotificationService Service,
        Guid UserId);

    private static Umgebung Aufbauen(bool email = true, string? pushKey = "pub-key", string? basis = TestLinks.Basis)
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var userId = Guid.NewGuid();
        lookup.Register(userId, "max@example.com", "Max", "Muster");
        var kanaele = new FakeNotificationChannels { EmailAktiv = email, PushPublicKey = pushKey };
        var mails = new FakeMailQueue();
        var pushs = new FakePushQueue();
        var service = new NotificationService(db, lookup, kanaele, TestLinks.Mit(basis), mails, pushs, NullLogger<NotificationService>.Instance);
        return new Umgebung(db, lookup, kanaele, mails, pushs, service, userId);
    }

    [Fact]
    public async Task Wichtig_UndEinstellungAn_SchicktMail()
    {
        var u = Aufbauen();

        await u.Service.CreateAsync(u.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        var mail = Assert.Single(u.Mails.Mails);
        Assert.Equal("max@example.com", mail.An);
        Assert.Contains("Anna lädt dich ein.", mail.Text);
        Assert.Contains("https://dogity.example/clubs", mail.Text);
        Assert.NotNull(mail.Html);
        Assert.Single(u.Db.Notifications);
    }

    [Fact]
    public async Task Wichtig_AberEinstellungAus_SchicktKeineMail_LegtGlockeTrotzdemAn()
    {
        var u = Aufbauen();
        u.Db.UserPreferences.Add(new UserPreference { UserId = u.UserId, EmailNotifications = false });
        await u.Db.SaveChangesAsync();

        await u.Service.CreateAsync(u.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        Assert.Empty(u.Mails.Mails);
        Assert.Single(u.Db.Notifications);
    }

    [Fact]
    public async Task Wichtig_OhneEinstellungsZeile_GiltAlsAn()
    {
        var u = Aufbauen();

        await u.Service.CreateAsync(u.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        Assert.Single(u.Mails.Mails);
    }

    [Fact]
    public async Task Unwichtig_SchicktKeineMail()
    {
        var u = Aufbauen();

        await u.Service.CreateAsync(u.UserId, "Dein Trainer hat Feedback hinterlassen.", "/dogs");

        Assert.Empty(u.Mails.Mails);
        Assert.Single(u.Db.Notifications);
    }

    [Fact]
    public async Task Wichtig_OhneEingerichtetenMailversand_SchicktKeineMail()
    {
        var u = Aufbauen(email: false);

        await u.Service.CreateAsync(u.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        Assert.Empty(u.Mails.Mails);
        Assert.Single(u.Db.Notifications);
    }

    [Fact]
    public async Task Wichtig_OhneBasisAdresse_SchicktMailOhneKnopf()
    {
        var u = Aufbauen(basis: "");

        await u.Service.CreateAsync(u.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        var mail = Assert.Single(u.Mails.Mails);
        Assert.DoesNotContain("<a ", mail.Html);
    }

    [Fact]
    public async Task Push_MitGeraet_ReihtAuftragMitAbsolutemLinkUndUngelesenZahlEin()
    {
        var u = Aufbauen();
        u.Db.PushSubscriptions.Add(new PushSubscription { UserId = u.UserId, Endpoint = "https://fcm.googleapis.com/x", P256dh = "a", Auth = "b" });
        await u.Db.SaveChangesAsync();
        await u.Service.CreateAsync(u.UserId, "Erste.", "/dashboard");

        await u.Service.CreateAsync(u.UserId, "Zweite.", "/dogs/1");

        Assert.Equal(2, u.Pushs.Auftraege.Count);
        var auftrag = u.Pushs.Auftraege[1];
        Assert.Equal(u.UserId, auftrag.UserId);
        using var json = System.Text.Json.JsonDocument.Parse(auftrag.Payload);
        var notification = json.RootElement.GetProperty("notification");
        Assert.Equal("Zweite.", notification.GetProperty("body").GetString());
        Assert.Equal("https://dogity.example/dogs/1", notification.GetProperty("navigate").GetString());
        Assert.Equal(2, notification.GetProperty("app_badge").GetInt32());
    }

    [Fact]
    public async Task Push_OhneGeraet_ReihtNichtsEin()
    {
        var u = Aufbauen();

        await u.Service.CreateAsync(u.UserId, "Hallo.", "/dashboard");

        Assert.Empty(u.Pushs.Auftraege);
    }

    [Fact]
    public async Task Push_OhneBasisAdresse_ReihtNichtsEin_WeilNavigateAbsolutSeinMuss()
    {
        var u = Aufbauen(basis: "");
        u.Db.PushSubscriptions.Add(new PushSubscription { UserId = u.UserId, Endpoint = "https://fcm.googleapis.com/x", P256dh = "a", Auth = "b" });
        await u.Db.SaveChangesAsync();

        await u.Service.CreateAsync(u.UserId, "Hallo.", "/dashboard");

        Assert.Empty(u.Pushs.Auftraege);
        Assert.Single(u.Db.Notifications);
    }

    [Fact]
    public async Task Push_NichtEingerichtet_ReihtNichtsEin()
    {
        var u = Aufbauen(pushKey: null);
        u.Db.PushSubscriptions.Add(new PushSubscription { UserId = u.UserId, Endpoint = "https://fcm.googleapis.com/x", P256dh = "a", Auth = "b" });
        await u.Db.SaveChangesAsync();

        await u.Service.CreateAsync(u.UserId, "Hallo.", "/dashboard");

        Assert.Empty(u.Pushs.Auftraege);
    }

    [Fact]
    public async Task Push_GehtNurAnGeraeteDerPerson()
    {
        var u = Aufbauen();
        var andere = Guid.NewGuid();
        u.Db.PushSubscriptions.Add(new PushSubscription { UserId = andere, Endpoint = "https://fcm.googleapis.com/y", P256dh = "a", Auth = "b" });
        await u.Db.SaveChangesAsync();

        await u.Service.CreateAsync(u.UserId, "Hallo.", "/dashboard");

        Assert.Empty(u.Pushs.Auftraege);
    }

    [Fact]
    public async Task EinFehlerBeimEinreihen_LaesstDieMeldungNichtScheitern()
    {
        var u = Aufbauen();
        var service = new NotificationService(
            u.Db, new WerfendeNutzersuche(), u.Kanaele, TestLinks.Mit(), u.Mails, u.Pushs, NullLogger<NotificationService>.Instance);

        await service.CreateAsync(u.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        Assert.Single(u.Db.Notifications);
        Assert.Empty(u.Mails.Mails);
    }

    private sealed class WerfendeNutzersuche : FakeUserLookupService
    {
        public override Task<IReadOnlyDictionary<Guid, Dogity.Application.Abstractions.UserLookupResult>> FindByIdsAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
            throw new InvalidOperationException("Identity nicht erreichbar.");
    }
}
