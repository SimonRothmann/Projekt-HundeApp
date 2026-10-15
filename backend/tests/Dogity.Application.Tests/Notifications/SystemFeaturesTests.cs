using System.Text.Json;
using Dogity.Api.Controllers;
using Dogity.Application.Notifications;
using Dogity.Infrastructure.Email;
using Dogity.Infrastructure.Notifications;
using Dogity.Infrastructure.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// <c>GET /api/system/features</c> ist anonym erreichbar und darf deshalb nur
/// Wahrheitswerte und den ÖFFENTLICHEN Schlüssel nennen.
/// </summary>
public class SystemFeaturesTests
{
    private const string PublicKey = "BPUBLIC-KEY-oeffentlich";
    private const string PrivateKey = "PRIVATER-SCHLUESSEL-geheim";
    private const string SmtpPasswort = "SMTP-PASSWORT-geheim";

    private static SystemController Controller(SmtpSettings smtp, PushSettings push) =>
        new(new NotificationChannels(Options.Create(smtp), Options.Create(push)));

    private static SystemFeaturesDto Antwort(SystemController controller) =>
        Assert.IsType<SystemFeaturesDto>(Assert.IsType<OkObjectResult>(controller.Features().Result).Value);

    [Fact]
    public void OhneKonfiguration_KeineMail_KeinPush()
    {
        var antwort = Antwort(Controller(new SmtpSettings(), new PushSettings()));

        Assert.False(antwort.Email);
        Assert.Null(antwort.PushPublicKey);
    }

    [Fact]
    public void MitHost_IstMailAktiv()
    {
        var antwort = Antwort(Controller(new SmtpSettings { Host = "smtp-relay.brevo.com" }, new PushSettings()));

        Assert.True(antwort.Email);
    }

    [Fact]
    public void PushNurMitVollstaendigemSchluesselpaar()
    {
        var voll = new PushSettings { VapidPublicKey = PublicKey, VapidPrivateKey = PrivateKey, Subject = "mailto:hallo@dogity.example" };
        Assert.Equal(PublicKey, Antwort(Controller(new SmtpSettings(), voll)).PushPublicKey);

        Assert.Null(Antwort(Controller(new SmtpSettings(), new PushSettings { VapidPublicKey = PublicKey })).PushPublicKey);
        Assert.Null(Antwort(Controller(new SmtpSettings(), new PushSettings { VapidPublicKey = PublicKey, VapidPrivateKey = PrivateKey })).PushPublicKey);
    }

    [Fact]
    public void Antwort_EnthaeltKeineGeheimnisse()
    {
        var smtp = new SmtpSettings
        {
            Host = "smtp-relay.brevo.com", Username = "benutzer@example.org", Password = SmtpPasswort,
            FromAddress = "hallo@dogity.example", ReplyToAddress = "antwort@example.org",
        };
        var push = new PushSettings { VapidPublicKey = PublicKey, VapidPrivateKey = PrivateKey, Subject = "mailto:hallo@dogity.example" };

        var json = JsonSerializer.Serialize(Antwort(Controller(smtp, push)));

        Assert.Contains(PublicKey, json);
        Assert.DoesNotContain(PrivateKey, json);
        Assert.DoesNotContain(SmtpPasswort, json);
        Assert.DoesNotContain("brevo", json);
        Assert.DoesNotContain("benutzer@example.org", json);
        Assert.DoesNotContain("mailto:", json);
        // Genau diese zwei Felder - ein neues Feld ist eine bewusste Entscheidung.
        using var dokument = JsonDocument.Parse(json);
        Assert.Equal(["Email", "PushPublicKey"], dokument.RootElement.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public void Endpunkt_IstAnonymErreichbar()
    {
        var methode = typeof(SystemController).GetMethod(nameof(SystemController.Features))!;

        Assert.NotEmpty(methode.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    [Fact]
    public void PushVerwaltung_VerlangtAnmeldung()
    {
        // PushController erbt [Authorize] von ApiControllerBase.
        Assert.NotEmpty(typeof(PushController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }

    [Theory]
    [InlineData("https://dogity.example/", "/clubs", "https://dogity.example/clubs")]
    [InlineData("https://dogity.example", "/dogs/1?eintrag=2", "https://dogity.example/dogs/1?eintrag=2")]
    [InlineData("https://dogity.example", null, "https://dogity.example/dashboard")]
    [InlineData("https://dogity.example", "//boese.example/x", "https://dogity.example/dashboard")]
    [InlineData("https://dogity.example", "https://boese.example/x", "https://dogity.example/dashboard")]
    [InlineData("", "/clubs", "/clubs")]
    public void Links_BleibenAufDerEigenenSeite(string basis, string? pfad, string erwartet) =>
        Assert.Equal(erwartet, Tests.TestSupport.TestLinks.Mit(basis).Absolut(pfad));
}
