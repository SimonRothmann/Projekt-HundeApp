using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Welche Mails einen Abmelde-Link tragen - Benachrichtigungen und Willkommen
/// ja, die Passwort-Mail nicht - und dass er das Token des richtigen Kontos trägt.
/// </summary>
public class AbmeldeLinksTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Notif(NotificationService Service, FakeMailQueue Mails, IEmailAbmeldeToken Tokens, Guid UserId);

    private static Notif Benachrichtigung(string? basis = TestLinks.Basis)
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var userId = Guid.NewGuid();
        lookup.Register(userId, "max@example.com", "Max", "Muster");
        var mails = new FakeMailQueue();
        var tokens = TestAbmeldeToken.Neu();
        var service = new NotificationService(
            db, lookup, new FakeNotificationChannels { EmailAktiv = true }, TestLinks.Mit(basis), tokens,
            mails, new FakePushQueue(), NullLogger<NotificationService>.Instance);
        return new Notif(service, mails, tokens, userId);
    }

    private static (AccountMailService Service, FakeMailQueue Mails, IEmailAbmeldeToken Tokens) Konto(string? basis = TestLinks.Basis)
    {
        var mails = new FakeMailQueue();
        var tokens = TestAbmeldeToken.Neu();
        var service = new AccountMailService(
            mails, new FakeNotificationChannels { EmailAktiv = true }, TestLinks.Mit(basis), tokens,
            new FakeUserLookupService(), new FakeNotificationService(), new MailDrossel(new FesteUhr()));
        return (service, mails, tokens);
    }

    private static string Token(string adresse) =>
        Uri.UnescapeDataString(new Uri(adresse).Query.Split("t=")[1]);

    [Fact]
    public async Task Benachrichtigung_TraegtAbmeldeSeite_MitDemTokenDesKontos()
    {
        var n = Benachrichtigung();

        await n.Service.CreateAsync(n.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        var mail = Assert.Single(n.Mails.Mails);
        Assert.StartsWith("https://dogity.example/email-abmelden?t=", mail.AbmeldeSeite);
        Assert.Equal(n.UserId, n.Tokens.Pruefe(Token(mail.AbmeldeSeite!)));
        Assert.Contains(mail.AbmeldeSeite!, mail.Text);
        Assert.Contains("Hier abmelden", mail.Html);
    }

    [Fact]
    public async Task Benachrichtigung_OhneFrontendBasis_HatKeinenAbmeldeLink()
    {
        var n = Benachrichtigung(basis: "");

        await n.Service.CreateAsync(n.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        var mail = Assert.Single(n.Mails.Mails);
        Assert.Null(mail.AbmeldeSeite);
        Assert.DoesNotContain("abmelden", mail.Text);
        Assert.DoesNotContain("Hier abmelden", mail.Html);
    }

    [Fact]
    public void Willkommen_TraegtAbmeldeSeite_MitDerKontoId()
    {
        var (service, mails, tokens) = Konto();
        var id = Guid.NewGuid();

        service.SendeWillkommen(id, "max@example.com");

        var mail = Assert.Single(mails.Mails);
        Assert.Equal(id, tokens.Pruefe(Token(mail.AbmeldeSeite!)));
        Assert.Contains(mail.AbmeldeSeite!, mail.Text);
        Assert.Contains("Hier abmelden", mail.Html);
    }

    [Fact]
    public void Willkommen_OhneBasisAdresse_HatKeineAbmeldung()
    {
        var (service, mails, _) = Konto(basis: "");

        service.SendeWillkommen(Guid.NewGuid(), "max@example.com");

        var mail = Assert.Single(mails.Mails);
        Assert.Null(mail.AbmeldeSeite);
        Assert.DoesNotContain("abbestellen", mail.Text);
    }

    [Fact]
    public async Task PasswortMail_TraegtKeineAbmeldung()
    {
        var (service, mails, _) = Konto();

        await service.SendePasswortZuruecksetzenAsync(
            new PasswortResetAnfrage(Guid.NewGuid(), "max@example.com", "Max", "Muster", "https://dogity.example/reset-password?token=x"),
            "ADMIN");

        var mail = Assert.Single(mails.Mails);
        Assert.Null(mail.AbmeldeSeite);
        Assert.DoesNotContain("abmelden", mail.Text);
    }
}
