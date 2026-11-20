using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Die beiden Anmelde-Cookies - Namen und Attribute an dieser einen Stelle. Die API setzt sie,
/// das Frontend sieht die Schlüssel nie (HttpOnly) und schickt sie nur mit
/// (<c>credentials: "include"</c>).
///
/// Ohne Domain-Attribut (nur für den Host, der sie gesetzt hat): Test und Produktion bleiben getrennt.
/// Secure gilt immer außer in der Entwicklung - hinter dem Proxy sieht die App evtl. kein HTTPS, daher
/// nicht von <c>Request.IsHttps</c> abhängig.
/// </summary>
public sealed class AnmeldeCookies(bool nurUeberHttps, IOptions<JwtSettings> jwt, TimeProvider uhr)
{
    /// <summary>Access-Token (JWT).</summary>
    public const string Zugriff = "dogity_at";

    /// <summary>Refresh-Token. Der Pfad hält ihn von allen Anfragen fern außer denen an die Anmelde-Endpunkte.</summary>
    public const string Erneuerung = "dogity_rt";

    public const string ErneuerungPfad = "/api/auth";

    /// <summary>Kopf, den das Frontend bei jedem Aufruf mitschickt (siehe KopfBeiCookies).</summary>
    public const string Kopf = "X-Dogity";

    /// <summary>Der Access-Token aus dem Cookie - nur, wenn kein Authorization-Kopf da ist (der bleibt für Tests und Übergang erlaubt).</summary>
    public static string? ZugriffstokenAusCookie(HttpRequest anfrage) =>
        !anfrage.Headers.ContainsKey("Authorization") && anfrage.Cookies.TryGetValue(Zugriff, out var token) && !string.IsNullOrEmpty(token)
            ? token
            : null;

    private readonly JwtSettings _jwt = jwt.Value;

    /// <summary>Setzt beide Cookies und nennt, bis wann der Access-Token gilt (für das Vorab-Erneuern im Frontend).</summary>
    public DateTimeOffset Setze(HttpResponse antwort, string zugriffstoken, string erneuerungstoken)
    {
        var bis = uhr.GetUtcNow().AddMinutes(_jwt.ExpiryMinutes);
        antwort.Cookies.Append(Zugriff, zugriffstoken, Optionen(SameSiteMode.Lax, "/", TimeSpan.FromMinutes(_jwt.ExpiryMinutes)));
        SetzeErneuerung(antwort, erneuerungstoken);
        return bis;
    }

    public void SetzeErneuerung(HttpResponse antwort, string erneuerungstoken) =>
        antwort.Cookies.Append(Erneuerung, erneuerungstoken, Optionen(SameSiteMode.Strict, ErneuerungPfad, TimeSpan.FromDays(_jwt.RefreshExpiryDays)));

    /// <summary>Löscht beide Cookies: gleiche Pfade und Attribute, Ablauf in der Vergangenheit.</summary>
    public void Loesche(HttpResponse antwort)
    {
        antwort.Cookies.Append(Zugriff, "", Optionen(SameSiteMode.Lax, "/", null));
        antwort.Cookies.Append(Erneuerung, "", Optionen(SameSiteMode.Strict, ErneuerungPfad, null));
    }

    private CookieOptions Optionen(SameSiteMode sameSite, string pfad, TimeSpan? dauer) => new()
    {
        HttpOnly = true,
        Secure = nurUeberHttps,
        SameSite = sameSite,
        Path = pfad,
        MaxAge = dauer,
        Expires = dauer is null ? DateTimeOffset.UnixEpoch : null,
    };
}
