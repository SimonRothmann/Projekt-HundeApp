using Dogity.Application.Notifications;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Die drei Mails an Nutzer:innen in der Sprache der Empfänger:in. Deutsch bleibt die
/// Vorgabe (siehe MailVorlagenTests), die Testmail ist Adminwerkzeug und bleibt deutsch.
/// </summary>
public class MailVorlagenEnglischTests
{
    private const string AppUrl = "https://dogity.example/dashboard";
    private const string Abmeldung = "https://dogity.example/abmelden?t=abc";

    [Fact]
    public void Willkommen_Englisch()
    {
        var mail = MailVorlagen.Willkommen(AppUrl, Abmeldung, "en");

        Assert.Equal("Welcome to Dogity", mail.Betreff);
        Assert.StartsWith("Hi,", mail.Text);
        Assert.Contains("training diary for dog sports", mail.Text);
        Assert.Contains("Just reply to this email", mail.Text);
        Assert.Contains("Open Dogity: " + AppUrl, mail.Text);
        Assert.Contains("The Dogity team", mail.Text);
        Assert.Contains("lang=\"en\"", mail.Html);
        Assert.Contains("The Dogity team", mail.Html);
        Assert.DoesNotContain("Dein Dogity-Team", mail.Text + mail.Html);
        Assert.DoesNotContain("Hallo", mail.Text + mail.Html);
    }

    [Fact]
    public void Willkommen_Englisch_TraegtDenAbmeldeLink()
    {
        var mail = MailVorlagen.Willkommen(AppUrl, Abmeldung, "en");

        Assert.Contains($"unsubscribe from email notifications (invitations, decisions) at any time: {Abmeldung}", mail.Text);
        Assert.Contains($"href=\"{Abmeldung}\"", mail.Html);
        Assert.Contains("Unsubscribe here", mail.Html);
        Assert.DoesNotContain("Hier abmelden", mail.Html);
    }

    [Fact]
    public void Willkommen_OhneSprache_BleibtDeutsch()
    {
        var mail = MailVorlagen.Willkommen(AppUrl, Abmeldung);

        Assert.Equal("Willkommen bei Dogity", mail.Betreff);
        Assert.Contains("lang=\"de\"", mail.Html);
        Assert.Contains("Hier abmelden", mail.Html);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    public void UnbekannteSprache_IstDeutsch(string sprache)
    {
        Assert.Equal("Dogity: Passwort zurücksetzen", MailVorlagen.PasswortZuruecksetzen("Max", "https://x.example/r", sprache).Betreff);
    }

    [Fact]
    public void Passwort_Englisch()
    {
        const string link = "https://dogity.example/reset-password?email=a%40b.de&token=abc%2Bdef";

        var mail = MailVorlagen.PasswortZuruecksetzen("Max", link, "en");

        Assert.Equal("Dogity: Reset your password", mail.Betreff);
        Assert.StartsWith("Hi Max,", mail.Text);
        Assert.Contains("set a new password for your Dogity account", mail.Text);
        Assert.Contains("you can ignore this email", mail.Text);
        Assert.Contains("Reset password: " + link, mail.Text);
        Assert.Contains("&amp;token=", mail.Html);
        Assert.Contains("lang=\"en\"", mail.Html);
        Assert.Contains("The Dogity team", mail.Text);
        // Keine Abmeldung: Die Konto-Mail bleibt unabhängig vom Schalter.
        Assert.DoesNotContain("nsubscribe", mail.Text);
    }

    [Fact]
    public void Benachrichtigung_Englisch_HatLinkFusszeileUndAbmeldung()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung(
            "Max",
            "Anna invites you to the club \"Nord\".",
            "https://dogity.example/clubs",
            "https://dogity.example/profile/funktionen",
            Abmeldung,
            "en");

        Assert.Equal("Dogity: Anna invites you to the club \"Nord\".", mail.Betreff);
        Assert.StartsWith("Hi Max,", mail.Text);
        Assert.Contains("Anna invites you to the club \"Nord\".", mail.Text);
        Assert.Contains("View in Dogity: https://dogity.example/clubs", mail.Text);
        Assert.Contains("You are receiving this email because", mail.Text);
        Assert.Contains("Profile → Features: https://dogity.example/profile/funktionen", mail.Text);
        Assert.Contains($"No more emails like this? Unsubscribe with one click: {Abmeldung}", mail.Text);
        Assert.Contains($"href=\"{Abmeldung}\"", mail.Html);
        Assert.Contains("Unsubscribe here", mail.Html);
        Assert.Contains("lang=\"en\"", mail.Html);
        Assert.DoesNotContain("Hallo", mail.Text);
    }

    [Fact]
    public void Benachrichtigung_Englisch_OhneBasisAdresse_HatKeinenKnopf()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung("Max", "Hello.", link: null, einstellungenUrl: null, sprache: "en");

        Assert.DoesNotContain("<a ", mail.Html);
        Assert.DoesNotContain("View in Dogity", mail.Text);
        Assert.Contains("under Profile → Features.", mail.Text);
    }

    [Fact]
    public void Benachrichtigung_Englisch_KodiertDenTextImHtml()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung("Max", "A <b>club</b> & more", null, null, sprache: "en");

        Assert.Contains("A &lt;b&gt;club&lt;/b&gt; &amp; more", mail.Html);
    }

    [Fact]
    public void Testmail_BleibtDeutsch()
    {
        var mail = MailVorlagen.Testmail(AppUrl, new DateTimeOffset(2026, 10, 16, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal("Dogity: Testmail", mail.Betreff);
        Assert.Contains("lang=\"de\"", mail.Html);
        Assert.StartsWith("Hallo,", mail.Text);
    }
}
