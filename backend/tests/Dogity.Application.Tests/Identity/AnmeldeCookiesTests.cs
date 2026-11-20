using Dogity.Api.Contracts;
using Dogity.Api.Controllers;
using Dogity.Api.Hosting;
using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// Die Anmeldung über HttpOnly-Cookies: Attribute, kein Token im Rumpf, Cookie als Anmeldung,
/// der Kopf X-Dogity bei ändernden Anfragen, Erneuerung und Abmeldung über das Cookie.
/// Login und Registrierung hängen ihre Cookies über denselben Weg an wie der Gastzugang
/// (BuildAuthResponseAsync) und brauchen einen echten SignInManager; sie sind hier nicht eigens aufgebaut.
/// </summary>
public class AnmeldeCookiesTests
{
    private sealed class TestJwt : IJwtTokenGenerator
    {
        public string GenerateToken(Guid userId, string email, IEnumerable<string> roles) => "jwt-neu";
    }

    private static AnmeldeCookies Cookies(bool https = true) =>
        new(https, Options.Create(new JwtSettings { ExpiryMinutes = 60, RefreshExpiryDays = 60 }), TimeProvider.System);

    private static string[] SetCookies(HttpResponse antwort) => antwort.Headers.SetCookie.Select(c => c!).ToArray();

    private static string Cookie(HttpResponse antwort, string name) =>
        SetCookies(antwort).Single(c => c.StartsWith(name + "=", StringComparison.Ordinal));

    [Fact]
    public void Setze_SchreibtBeideCookiesMitDenVereinbartenAttributen()
    {
        var antwort = new DefaultHttpContext().Response;

        var bis = Cookies().Setze(antwort, "zugriff-wert", "erneuerung-wert");

        var at = Cookie(antwort, AnmeldeCookies.Zugriff).ToLowerInvariant();
        Assert.Contains("dogity_at=zugriff-wert", at);
        Assert.Contains("httponly", at);
        Assert.Contains("secure", at);
        Assert.Contains("samesite=lax", at);
        Assert.Contains("path=/;", at + ";");
        Assert.Contains("max-age=3600", at);
        Assert.DoesNotContain("domain=", at);

        var rt = Cookie(antwort, AnmeldeCookies.Erneuerung).ToLowerInvariant();
        Assert.Contains("dogity_rt=erneuerung-wert", rt);
        Assert.Contains("httponly", rt);
        Assert.Contains("secure", rt);
        Assert.Contains("samesite=strict", rt);
        Assert.Contains("path=/api/auth", rt);
        Assert.Contains($"max-age={60 * 24 * 3600}", rt);
        Assert.DoesNotContain("domain=", rt);

        Assert.InRange(bis, DateTimeOffset.UtcNow.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));
    }

    [Fact]
    public void Setze_InDerEntwicklungOhneSecure()
    {
        var antwort = new DefaultHttpContext().Response;

        Cookies(https: false).Setze(antwort, "a", "b");

        Assert.All(SetCookies(antwort), c => Assert.DoesNotContain("secure", c.ToLowerInvariant()));
    }

    [Fact]
    public void Loesche_LaeuftMitGleichenPfadenInDerVergangenheitAb()
    {
        var antwort = new DefaultHttpContext().Response;

        Cookies().Loesche(antwort);

        var at = Cookie(antwort, AnmeldeCookies.Zugriff).ToLowerInvariant();
        var rt = Cookie(antwort, AnmeldeCookies.Erneuerung).ToLowerInvariant();
        Assert.Contains("expires=thu, 01 jan 1970", at);
        Assert.Contains("expires=thu, 01 jan 1970", rt);
        Assert.Contains("path=/api/auth", rt);
        Assert.Contains("path=/", at);
    }

    [Fact]
    public void ZugriffstokenAusCookie_NurOhneAuthorizationKopf()
    {
        var anfrage = new DefaultHttpContext().Request;
        Assert.Null(AnmeldeCookies.ZugriffstokenAusCookie(anfrage));

        anfrage.Headers.Cookie = "dogity_at=cookie-jwt";
        Assert.Equal("cookie-jwt", AnmeldeCookies.ZugriffstokenAusCookie(anfrage));

        anfrage.Headers.Authorization = "Bearer kopf-jwt";
        Assert.Null(AnmeldeCookies.ZugriffstokenAusCookie(anfrage));
    }

    [Theory]
    [InlineData("POST", "dogity_at=x", null, null, true)]   // Cookie, kein Kopf: abgelehnt
    [InlineData("PUT", "dogity_rt=x", null, null, true)]
    [InlineData("DELETE", "dogity_at=x", null, null, true)]
    [InlineData("POST", "dogity_at=x", null, "1", false)]   // mit X-Dogity: durch
    [InlineData("POST", "dogity_at=x", "Bearer t", null, false)] // mit Authorization-Kopf: durch
    [InlineData("GET", "dogity_at=x", null, null, false)]   // lesend: frei
    [InlineData("HEAD", "dogity_at=x", null, null, false)]
    [InlineData("OPTIONS", "dogity_at=x", null, null, false)]
    [InlineData("POST", null, null, null, false)]           // kein Cookie (z.B. Anmeldung, Webhooks): frei
    public void KopfBeiCookies_Verlangt(string methode, string? cookie, string? authorization, string? kopf, bool verlangt)
    {
        var anfrage = new DefaultHttpContext().Request;
        anfrage.Method = methode;
        if (cookie is not null) anfrage.Headers.Cookie = cookie;
        if (authorization is not null) anfrage.Headers.Authorization = authorization;
        if (kopf is not null) anfrage.Headers[AnmeldeCookies.Kopf] = kopf;

        Assert.Equal(verlangt, KopfBeiCookies.Verlangt(anfrage));
    }

    [Fact]
    public async Task KopfBeiCookies_Middleware_AntwortetMit403UndLaesstFreieAnfragenDurch()
    {
        var weiter = false;
        var pipeline = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider())
            .UseKopfBeiCookies()
            .Use(async (ctx, next) => { weiter = true; await next(ctx); })
            .Build();

        var abgelehnt = new DefaultHttpContext();
        abgelehnt.Request.Method = "POST";
        abgelehnt.Request.Headers.Cookie = "dogity_at=x";
        abgelehnt.Response.Body = new MemoryStream();
        await pipeline(abgelehnt);
        Assert.Equal(403, abgelehnt.Response.StatusCode);
        Assert.False(weiter);

        var frei = new DefaultHttpContext();
        frei.Request.Method = "POST";
        frei.Request.Headers.Cookie = "dogity_at=x";
        frei.Request.Headers[AnmeldeCookies.Kopf] = "1";
        await pipeline(frei);
        Assert.True(weiter);
    }

    private static async Task<(AuthController Controller, FakeRefreshTokenService Tokens, ApplicationUser Nutzer)> BaueAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>();
        var scope = services.BuildServiceProvider().CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var nutzer = new ApplicationUser { UserName = "ida@example.org", Email = "ida@example.org", FirstName = "Ida", LastName = "Test" };
        Assert.True((await users.CreateAsync(nutzer, "Passwort123!")).Succeeded);

        var tokens = new FakeRefreshTokenService { RefreshErgebnis = new RefreshRotationResult(true, nutzer.Id, "rt-neu") };
        var controller = new AuthController(
            users, null!, new TestJwt(), tokens, null!, null!, null!, null!, new AnmeldeDrossel(), Cookies(), TimeProvider.System,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return (controller, tokens, nutzer);
    }

    [Fact]
    public async Task Refresh_PerCookie_SetztNeueCookies_UndKeinTokenImRumpf()
    {
        var (controller, tokens, nutzer) = await BaueAsync();
        controller.Request.Headers.Cookie = "dogity_rt=rt-alt";

        var antwort = Assert.IsType<OkObjectResult>((await controller.Refresh(null)).Result);

        Assert.Equal("rt-alt", tokens.ZuletztVorgelegt);
        var auth = Assert.IsType<AuthResponse>(antwort.Value);
        Assert.Equal(nutzer.Id, auth.UserId);
        Assert.True(auth.AccessExpiresAt > DateTimeOffset.UtcNow);
        Assert.Contains("dogity_at=jwt-neu", Cookie(controller.Response, AnmeldeCookies.Zugriff));
        Assert.Contains("dogity_rt=rt-neu", Cookie(controller.Response, AnmeldeCookies.Erneuerung));
        // Der Rumpf trägt keine Schlüssel: weder als Feld noch im Text.
        var json = System.Text.Json.JsonSerializer.Serialize(auth);
        Assert.DoesNotContain("jwt-neu", json);
        Assert.DoesNotContain("rt-neu", json);
        Assert.DoesNotContain("oken", json);
    }

    [Fact]
    public async Task Refresh_UebergangMitTokenImRumpf_GehtVorDemCookie()
    {
        var (controller, tokens, _) = await BaueAsync();
        controller.Request.Headers.Cookie = "dogity_rt=rt-cookie";

        Assert.IsType<OkObjectResult>((await controller.Refresh(new RefreshTokenRequest("rt-rumpf"))).Result);

        Assert.Equal("rt-rumpf", tokens.ZuletztVorgelegt);
        Assert.Contains("dogity_rt=rt-neu", Cookie(controller.Response, AnmeldeCookies.Erneuerung));
    }

    [Fact]
    public async Task Refresh_OhneTokenOderAbgelehnt_401OhneCookies()
    {
        var (controller, tokens, _) = await BaueAsync();

        Assert.IsType<UnauthorizedObjectResult>((await controller.Refresh(null)).Result);
        Assert.Null(tokens.ZuletztVorgelegt);

        tokens.RefreshErgebnis = new RefreshRotationResult(false);
        controller.Request.Headers.Cookie = "dogity_rt=verbraucht";
        Assert.IsType<UnauthorizedObjectResult>((await controller.Refresh(null)).Result);
        Assert.Empty(SetCookies(controller.Response));
    }

    [Fact]
    public async Task Logout_WiderruftDenToken_AusCookieOderRumpf_UndLoeschtBeideCookies()
    {
        var (controller, tokens, _) = await BaueAsync();
        controller.Request.Headers.Cookie = "dogity_rt=rt-cookie";

        Assert.IsType<NoContentResult>(await controller.Logout(null));
        Assert.Equal(["rt-cookie"], tokens.Widerrufen);
        Assert.Contains("expires=Thu, 01 Jan 1970", Cookie(controller.Response, AnmeldeCookies.Zugriff));
        Assert.Contains("expires=Thu, 01 Jan 1970", Cookie(controller.Response, AnmeldeCookies.Erneuerung));

        Assert.IsType<NoContentResult>(await controller.Logout(new RefreshTokenRequest("rt-rumpf")));
        Assert.Equal(["rt-cookie", "rt-rumpf"], tokens.Widerrufen);
    }
}
