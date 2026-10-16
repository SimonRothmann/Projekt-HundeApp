using Dogity.Application.Notifications;
using Microsoft.AspNetCore.DataProtection;

namespace Dogity.Infrastructure.Notifications;

/// <summary>
/// Token über ASP.NET DataProtection - dieselben, im Volume persistierten
/// Schlüssel wie bei Identity, aber ein eigener Zweck. Ein Token für den
/// Passwort-Reset (anderer Zweck) lässt sich hier nicht einlösen und umgekehrt.
/// "v1" im Zweck, damit sich das Format später ablösen ließe, ohne dass alte
/// Links versehentlich als neue gelten.
/// </summary>
public class EmailAbmeldeToken(IDataProtectionProvider provider) : IEmailAbmeldeToken
{
    public const string Zweck = "Dogity.EmailAbmeldung.v1";

    // Ein echtes Token ist unter 150 Zeichen lang; alles darüber ist Unsinn und
    // soll gar nicht erst entschlüsselt werden (anonymer Endpunkt).
    private const int LaengeHoechstens = 512;

    private readonly IDataProtector _schutz = provider.CreateProtector(Zweck);

    // Protect(string) liefert base64url - ohne "+", "/" oder "=" und damit in einer Adresse verwendbar.
    public string Erzeuge(Guid userId) => _schutz.Protect(userId.ToString("N"));

    public Guid? Pruefe(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > LaengeHoechstens) return null;

        try
        {
            var klartext = _schutz.Unprotect(token.Trim());
            return Guid.TryParseExact(klartext, "N", out var id) && id != Guid.Empty ? id : null;
        }
        catch (Exception)
        {
            // Manipuliert, fremder Zweck, andere Schlüssel, kein base64url: für den
            // Aufrufer alles dasselbe - ungültig. Keine Ausnahme nach außen.
            return null;
        }
    }
}
