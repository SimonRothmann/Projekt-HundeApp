using Dogity.Application.Notifications;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Die Mailvorlagen sind reine Funktionen: Inhalt, Link und Fußzeile lassen sich
/// ohne Datenbank und ohne Mailserver prüfen.
/// </summary>
public class MailVorlagenTests
{
    private const string AppUrl = "https://dogity.example/dashboard";

    [Fact]
    public void Willkommen_FuehrtZurApp_UndLaedtZumAntwortenEin()
    {
        var mail = MailVorlagen.Willkommen(AppUrl);

        Assert.Equal("Willkommen bei Dogity", mail.Betreff);
        // Ohne Namen: Die Adresse ist unbestätigt, der Name frei wählbar.
        Assert.Contains("Hallo,", mail.Text);
        Assert.Contains(AppUrl, mail.Text);
        Assert.Contains($"href=\"{AppUrl}\"", mail.Html);
        Assert.Contains("Antworte einfach auf diese Mail", mail.Text);
        Assert.Contains("Hund anlegen", mail.Text);
        Assert.Contains("Verein", mail.Text);
    }

    [Fact]
    public void Willkommen_HatKeineFusszeileZumAbschalten()
    {
        var mail = MailVorlagen.Willkommen(AppUrl);

        Assert.DoesNotContain("Funktionen", mail.Text);
        Assert.DoesNotContain("Funktionen", mail.Html);
    }

    [Fact]
    public void Passwort_EnthaeltDenLink_OhneFusszeile()
    {
        const string link = "https://dogity.example/reset-password?email=a%40b.de&token=abc%2Bdef";

        var mail = MailVorlagen.PasswortZuruecksetzen("Max", link);

        Assert.Equal("Dogity: Passwort zurücksetzen", mail.Betreff);
        Assert.Contains(link, mail.Text);
        Assert.Contains("Mail ignorieren", mail.Text);
        // Das "&" im Link steht im HTML kodiert, sonst wäre das Attribut ungültig.
        Assert.Contains("token=abc%2Bdef", mail.Html);
        Assert.Contains("&amp;token=", mail.Html);
        Assert.DoesNotContain("Funktionen", mail.Text);
        Assert.DoesNotContain("Abschalten", mail.Text);
    }

    [Fact]
    public void Benachrichtigung_HatLink_Fusszeile_UndText()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung(
            "Max",
            "Anna lädt dich in den Verein \"Nord\" ein.",
            "https://dogity.example/clubs",
            "https://dogity.example/profile/funktionen");

        Assert.Equal("Dogity: Anna lädt dich in den Verein \"Nord\" ein.", mail.Betreff);
        Assert.Contains("Anna lädt dich in den Verein \"Nord\" ein.", mail.Text);
        Assert.Contains("https://dogity.example/clubs", mail.Text);
        Assert.Contains("Du bekommst diese Mail, weil", mail.Text);
        Assert.Contains("Profil → Funktionen", mail.Text);
        Assert.Contains("https://dogity.example/profile/funktionen", mail.Text);
        Assert.Contains("Profil → Funktionen", mail.Html);
    }

    [Fact]
    public void Benachrichtigung_OhneBasisAdresse_HatKeinenKnopf()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung("Max", "Hallo.", link: null, einstellungenUrl: null);

        Assert.DoesNotContain("<a ", mail.Html);
        Assert.DoesNotContain("In Dogity ansehen", mail.Text);
    }

    [Fact]
    public void Eingesetzte_Werte_WerdenKodiert()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung(
            "<b>Max</b>", "Du wurdest zu \"<script>alert(1)</script>\" hinzugefügt.", "https://dogity.example/clubs", null);

        Assert.DoesNotContain("<script>", mail.Html);
        Assert.DoesNotContain("<b>Max</b>", mail.Html);
        Assert.Contains("&lt;script&gt;", mail.Html);
    }

    [Fact]
    public void Betreff_WirdGekuerzt_UndHatKeineZeilenumbrueche()
    {
        var lang = "Zeile eins\nZeile zwei " + new string('x', 200);

        var betreff = MailVorlagen.Betreff(lang);

        Assert.StartsWith("Dogity: Zeile eins Zeile zwei", betreff);
        Assert.DoesNotContain('\n', betreff);
        Assert.EndsWith("…", betreff);
        Assert.True(betreff.Length <= "Dogity: ".Length + 80);
    }

    [Fact]
    public void OhneVorname_WirdNeutralAngesprochen()
    {
        Assert.Contains("Hallo,", MailVorlagen.PasswortZuruecksetzen("  ", AppUrl).Text);
    }

    private const string AbmeldeSeite = "https://dogity.example/email-abmelden?t=abc_DEF-123";

    [Fact]
    public void Benachrichtigung_MitAbmeldeSeite_HatDenLink_InTextUndHtml_UndNochDenHinweisAufFunktionen()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung(
            "Max", "Anna lädt dich ein.", "https://dogity.example/clubs", "https://dogity.example/profile/funktionen", AbmeldeSeite);

        Assert.Contains($"Keine solchen Mails mehr? Mit einem Klick abmelden: {AbmeldeSeite}", mail.Text);
        Assert.Contains($"href=\"{AbmeldeSeite}\"", mail.Html);
        Assert.Contains(">Hier abmelden</a>", mail.Html);
        Assert.Contains("Profil → Funktionen", mail.Text);
    }

    [Fact]
    public void Benachrichtigung_OhneAbmeldeSeite_HatKeinenAbmeldeLink()
    {
        var mail = MailVorlagen.WichtigeBenachrichtigung("Max", "Hallo.", "https://dogity.example/clubs", null);

        Assert.DoesNotContain("abmelden", mail.Text);
        Assert.DoesNotContain("Hier abmelden", mail.Html);
    }

    [Fact]
    public void Willkommen_MitAbmeldeSeite_HatDenLink_InTextUndHtml()
    {
        var mail = MailVorlagen.Willkommen(AppUrl, AbmeldeSeite);

        Assert.Contains($"jederzeit abbestellen: {AbmeldeSeite}", mail.Text);
        Assert.Contains($"href=\"{AbmeldeSeite}\"", mail.Html);
        Assert.Contains(">Hier abmelden</a>", mail.Html);
    }

    [Fact]
    public void Abmelde_Link_WirdImHtmlKodiert()
    {
        var mail = MailVorlagen.Willkommen(AppUrl, "https://dogity.example/email-abmelden?t=a&b=\"c\"");

        Assert.Contains("href=\"https://dogity.example/email-abmelden?t=a&amp;b=&quot;c&quot;\"", mail.Html);
        Assert.DoesNotContain("b=\"c\"", mail.Html);
    }

    [Fact]
    public void Passwort_UndTestmail_HabenKeinenAbmeldeLink()
    {
        var passwort = MailVorlagen.PasswortZuruecksetzen("Max", "https://dogity.example/reset-password?token=x");
        var test = MailVorlagen.Testmail(AppUrl, DateTimeOffset.UtcNow);

        foreach (var mail in new[] { passwort, test })
        {
            Assert.DoesNotContain("abmelden", mail.Text);
            Assert.DoesNotContain("abbestellen", mail.Text);
            Assert.DoesNotContain("Hier abmelden", mail.Html);
        }
    }
}
