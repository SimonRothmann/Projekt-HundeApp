using System.Reflection;
using System.Text.Json;
using Dogity.Api.Contracts;
using Dogity.Api.Controllers;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Der Abmelde-Endpunkt ist anonym, begrenzt und ändert nichts per GET.
/// </summary>
public class EmailAbmeldungControllerTests
{
    private sealed class MerkenderDienst(bool gueltig) : IEmailAbmeldungService
    {
        public List<(string? Token, bool Aktiv)> Aufrufe { get; } = [];

        public Task<Result> SetzeAsync(string? token, bool aktiv, CancellationToken ct = default)
        {
            Aufrufe.Add((token, aktiv));
            return Task.FromResult(gueltig ? Result.Success() : Result.Failure(EmailAbmeldungService.Ungueltig));
        }
    }

    [Fact]
    public void Controller_IstAnonym_UndHatEigenesRateLimit()
    {
        Assert.NotNull(typeof(EmailAbmeldungController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("abmeldung", typeof(EmailAbmeldungController).GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public void EsGibtKeinGet_DasEtwasAendert()
    {
        var gets = typeof(EmailAbmeldungController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a => a.HttpMethods.Contains("GET")));

        // Link-Scanner (SafeLinks) rufen GET-Links aus Mails vorab auf.
        Assert.Empty(gets);
    }

    [Fact]
    public async Task Setze_GueltigesToken_204()
    {
        var dienst = new MerkenderDienst(gueltig: true);

        var antwort = await new EmailAbmeldungController(dienst).Setze(new EmailAbmeldungRequest("abc", false), CancellationToken.None);

        Assert.IsType<NoContentResult>(antwort);
        Assert.Equal(("abc", false), Assert.Single(dienst.Aufrufe));
    }

    [Fact]
    public async Task Setze_UngueltigesToken_400_MitErrors()
    {
        var antwort = await new EmailAbmeldungController(new MerkenderDienst(gueltig: false))
            .Setze(new EmailAbmeldungRequest("kaputt", true), CancellationToken.None);

        var schlecht = Assert.IsType<BadRequestObjectResult>(antwort);
        Assert.Contains("errors", JsonSerializer.Serialize(schlecht.Value));
    }

    [Fact]
    public void Registrierung_OhneFeld_GiltAlsNichtGesetzt_AlsoVorgabeJa()
    {
        var optionen = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var alt = JsonSerializer.Deserialize<RegisterRequest>(
            """{"email":"a@b.de","password":"x","firstName":"A","lastName":"B"}""", optionen)!;
        var neu = JsonSerializer.Deserialize<RegisterRequest>(
            """{"email":"a@b.de","password":"x","firstName":"A","lastName":"B","emailBenachrichtigungen":false}""", optionen)!;

        Assert.Null(alt.EmailBenachrichtigungen);
        Assert.False(neu.EmailBenachrichtigungen);
    }
}
