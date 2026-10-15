using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Willkommens- und Passwort-Mail, und wann die Admins statt dessen
/// benachrichtigt werden.
/// </summary>
public class AccountMailServiceTests
{
    private const string AdminRolle = "ADMIN";
    private const string ResetLink = "https://dogity.example/reset-password?email=max%40example.com&token=geheim";

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public DateTimeOffset Jetzt { get; set; } = jetzt;
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Umgebung(
        AccountMailService Service,
        FakeMailQueue Mails,
        FakeNotificationChannels Kanaele,
        FakeNotificationService Meldungen,
        FakeUserLookupService Lookup,
        FesteUhr Uhr);

    private static Umgebung Aufbauen(bool email)
    {
        var mails = new FakeMailQueue();
        var kanaele = new FakeNotificationChannels { EmailAktiv = email };
        var meldungen = new FakeNotificationService();
        var lookup = new FakeUserLookupService();
        lookup.Rollen[AdminRolle] = [Guid.NewGuid(), Guid.NewGuid()];
        var uhr = new FesteUhr(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        var service = new AccountMailService(mails, kanaele, TestLinks.Mit(), lookup, meldungen, new MailDrossel(uhr));
        return new Umgebung(service, mails, kanaele, meldungen, lookup, uhr);
    }

    private static PasswortResetAnfrage Anfrage() =>
        new(Guid.NewGuid(), "max@example.com", "Max", "Muster", ResetLink);

    [Fact]
    public void Willkommen_MitMailversand_ReihtDieMailEin()
    {
        var u = Aufbauen(email: true);

        u.Service.SendeWillkommen("max@example.com");

        var mail = Assert.Single(u.Mails.Mails);
        Assert.Equal("max@example.com", mail.An);
        Assert.Contains("Willkommen", mail.Betreff);
        Assert.Contains("https://dogity.example/dashboard", mail.Text);
    }

    [Fact]
    public void Willkommen_OhneMailversand_TutNichts()
    {
        var u = Aufbauen(email: false);

        u.Service.SendeWillkommen("max@example.com");

        Assert.Empty(u.Mails.Mails);
    }

    [Fact]
    public async Task Passwort_MitMailversand_SchicktMail_UndBenachrichtigtKeineAdmins()
    {
        var u = Aufbauen(email: true);

        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);

        var mail = Assert.Single(u.Mails.Mails);
        Assert.Contains(ResetLink, mail.Text);
        Assert.Empty(u.Meldungen.Created);
    }

    [Fact]
    public async Task Passwort_OhneMailversand_BenachrichtigtAlleAdmins()
    {
        var u = Aufbauen(email: false);
        var anfrage = Anfrage();

        await u.Service.SendePasswortZuruecksetzenAsync(anfrage, AdminRolle);

        Assert.Equal(2, u.Meldungen.Created.Count);
        Assert.All(u.Meldungen.Created, m =>
        {
            Assert.Contains("möchte das Passwort zurücksetzen", m.Message);
            Assert.Equal($"/admin/nutzer?resetUser={anfrage.UserId}", m.LinkPath);
            // Die Admin-Meldung trägt den Link nicht - sie führt zur Nutzerverwaltung.
            Assert.DoesNotContain("geheim", m.Message);
        });
        // Die Meldung an die Admins ist keine Mail-Meldung.
        Assert.Empty(u.Meldungen.Wichtige);
    }

    [Fact]
    public async Task Passwort_OhneMailversand_ReihtTrotzdemEin_DamitDerLoggingSenderProtokolliert()
    {
        var u = Aufbauen(email: false);

        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);

        Assert.Single(u.Mails.Mails);
    }

    [Fact]
    public void Willkommen_NenntKeinenNamen()
    {
        var u = Aufbauen(email: true);

        u.Service.SendeWillkommen("max@example.com");

        var mail = Assert.Single(u.Mails.Mails);
        Assert.StartsWith("Hallo,", mail.Text);
    }

    [Fact]
    public async Task Passwort_ZweiteAnfrageFuerDieselbeAdresse_ImAbstand_ReihtKeineZweiteMailEin()
    {
        var u = Aufbauen(email: true);

        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);
        u.Uhr.Jetzt += TimeSpan.FromMinutes(2);
        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);

        Assert.Single(u.Mails.Mails);
        // Auch die zweite Anfrage macht keine Admin-Meldung auf.
        Assert.Empty(u.Meldungen.Created);
    }

    [Fact]
    public async Task Passwort_NachDemAbstand_GehtWiederEineMailRaus()
    {
        var u = Aufbauen(email: true);

        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);
        u.Uhr.Jetzt += TimeSpan.FromMinutes(11);
        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);

        Assert.Equal(2, u.Mails.Mails.Count);
    }

    [Fact]
    public async Task Passwort_VerschiedeneAdressen_DrosseltNurJeAdresse()
    {
        var u = Aufbauen(email: true);

        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);
        await u.Service.SendePasswortZuruecksetzenAsync(
            new PasswortResetAnfrage(Guid.NewGuid(), "ANNA@example.com", "Anna", "Beispiel", ResetLink), AdminRolle);
        // Groß-/Kleinschreibung der Adresse macht keinen neuen Topf auf.
        await u.Service.SendePasswortZuruecksetzenAsync(
            new PasswortResetAnfrage(Guid.NewGuid(), "anna@example.com", "Anna", "Beispiel", ResetLink), AdminRolle);

        Assert.Equal(2, u.Mails.Mails.Count);
    }

    [Fact]
    public void Willkommen_TageskontingentIstBegrenzt_UndStelltSichAmNaechstenTagZurueck()
    {
        var u = Aufbauen(email: true);

        for (var i = 0; i < 150; i++) u.Service.SendeWillkommen($"person{i}@example.com");
        Assert.Equal(100, u.Mails.Mails.Count);

        u.Uhr.Jetzt += TimeSpan.FromDays(1);
        u.Service.SendeWillkommen("neu@example.com");
        Assert.Equal(101, u.Mails.Mails.Count);
    }

    [Fact]
    public async Task Passwort_BleibtMoeglich_AuchWennDasWillkommenKontingentErschoepftIst()
    {
        var u = Aufbauen(email: true);
        for (var i = 0; i < 150; i++) u.Service.SendeWillkommen($"person{i}@example.com");
        var vorher = u.Mails.Mails.Count;

        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);

        Assert.Equal(vorher + 1, u.Mails.Mails.Count);
    }

    [Fact]
    public async Task Passwort_OhneMailversand_WirdNichtGedrosselt()
    {
        var u = Aufbauen(email: false);

        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);
        await u.Service.SendePasswortZuruecksetzenAsync(Anfrage(), AdminRolle);

        // Beide Anfragen erreichen die Admins (2 Admins x 2 Anfragen).
        Assert.Equal(4, u.Meldungen.Created.Count);
    }
}
