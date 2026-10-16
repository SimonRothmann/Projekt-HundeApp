using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Welche Mails einen Abmelde-Link und die Ein-Klick-Adresse tragen, und dass
/// beide Basis-Adressen unabhängig voneinander gebraucht werden.
/// </summary>
public class AbmeldeLinksTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Notif(NotificationService Service, FakeMailQueue Mails, IEmailAbmeldeToken Tokens, Guid UserId);

    private static Notif Benachrichtigung(string? basis = TestLinks.Basis, string? apiBasis = TestLinks.ApiBasis)
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var userId = Guid.NewGuid();
        lookup.Register(userId, "max@example.com", "Max", "Muster");
        var mails = new FakeMailQueue();
        var tokens = TestAbmeldeToken.Neu();
        var service = new NotificationService(
            db, lookup, new FakeNotificationChannels { EmailAktiv = true }, TestLinks.Mit(basis, apiBasis), tokens,
            mails, new FakePushQueue(), NullLogger<NotificationService>.Instance);
        return new Notif(service, mails, tokens, userId);
    }

    private static (AccountMailService Service, FakeMailQueue Mails, IEmailAbmeldeToken Tokens) Konto(
        string? basis = TestLinks.Basis, string? apiBasis = TestLinks.ApiBasis)
    {
        var mails = new FakeMailQueue();
        var tokens = TestAbmeldeToken.Neu();
        var service = new AccountMailService(
            mails, new FakeNotificationChannels { EmailAktiv = true }, TestLinks.Mit(basis, apiBasis), tokens,
            new FakeUserLookupService(), new FakeNotificationService(), new MailDrossel(new FesteUhr()));
        return (service, mails, tokens);
    }

    private static string Token(string adresse) =>
        Uri.UnescapeDataString(new Uri(adresse).Query.Split("t=")[1]);

    [Fact]
    public async Task Benachrichtigung_TraegtAbmeldeSeiteUndEinKlickUrl_MitDemTokenDesKontos()
    {
        var n = Benachrichtigung();

        await n.Service.CreateAsync(n.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        var mail = Assert.Single(n.Mails.Mails);
        Assert.StartsWith("https://dogity.example/email-abmelden?t=", mail.AbmeldeSeite);
        Assert.StartsWith("https://api.dogity.example/api/email-abmeldung/ein-klick?t=", mail.EinKlickUrl);
        Assert.Equal(n.UserId, n.Tokens.Pruefe(Token(mail.AbmeldeSeite!)));
        Assert.Equal(n.UserId, n.Tokens.Pruefe(Token(mail.EinKlickUrl!)));
        Assert.Contains(mail.AbmeldeSeite!, mail.Text);
        Assert.Contains("Hier abmelden", mail.Html);
    }

    [Fact]
    public async Task Benachrichtigung_OhneApiBasis_HatLinkImText_ButKeineEinKlickUrl()
    {
        var n = Benachrichtigung(apiBasis: null);

        await n.Service.CreateAsync(n.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        var mail = Assert.Single(n.Mails.Mails);
        Assert.NotNull(mail.AbmeldeSeite);
        Assert.Contains(mail.AbmeldeSeite, mail.Text);
        Assert.Null(mail.EinKlickUrl);
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
    public async Task Benachrichtigung_OhneBeideBasisAdressen_HatWederLinkNochKopfzeile()
    {
        var n = Benachrichtigung(basis: "", apiBasis: "");

        await n.Service.CreateAsync(n.UserId, "Anna lädt dich ein.", "/clubs", wichtig: true);

        var mail = Assert.Single(n.Mails.Mails);
        Assert.Null(mail.AbmeldeSeite);
        Assert.Null(mail.EinKlickUrl);
    }

    [Fact]
    public void Willkommen_TraegtAbmeldeSeiteUndEinKlickUrl_MitDerKontoId()
    {
        var (service, mails, tokens) = Konto();
        var id = Guid.NewGuid();

        service.SendeWillkommen(id, "max@example.com");

        var mail = Assert.Single(mails.Mails);
        Assert.Equal(id, tokens.Pruefe(Token(mail.AbmeldeSeite!)));
        Assert.Equal(id, tokens.Pruefe(Token(mail.EinKlickUrl!)));
        Assert.Contains(mail.AbmeldeSeite!, mail.Text);
        Assert.Contains("Hier abmelden", mail.Html);
    }

    [Fact]
    public void Willkommen_OhneBasisAdressen_HatKeineAbmeldung()
    {
        var (service, mails, _) = Konto(basis: "", apiBasis: "");

        service.SendeWillkommen(Guid.NewGuid(), "max@example.com");

        var mail = Assert.Single(mails.Mails);
        Assert.Null(mail.AbmeldeSeite);
        Assert.Null(mail.EinKlickUrl);
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
        Assert.Null(mail.EinKlickUrl);
        Assert.DoesNotContain("abmelden", mail.Text);
    }

    [Fact]
    public void FrontendLinks_ApiAbsolut_NurMitApiBasis_UndNurFuerPfade()
    {
        var mit = TestLinks.Mit();
        Assert.True(mit.HatApiBasis);
        Assert.Equal("https://api.dogity.example/api/x?t=1", mit.ApiAbsolut("/api/x?t=1"));
        Assert.Null(mit.ApiAbsolut("//fremd.example"));
        Assert.Null(mit.ApiAbsolut("https://fremd.example"));
        Assert.Null(mit.ApiAbsolut(""));

        var ohne = TestLinks.Mit(apiBasis: null);
        Assert.False(ohne.HatApiBasis);
        Assert.Null(ohne.ApiAbsolut("/api/x"));

        // Ein Schrägstrich am Ende der Basis ergibt keinen doppelten.
        Assert.Equal("https://api.dogity.example/api/x", TestLinks.Mit(apiBasis: "https://api.dogity.example/").ApiAbsolut("/api/x"));
    }
}
