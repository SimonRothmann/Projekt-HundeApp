namespace Dogity.Application.Notifications;

/// <summary>
/// Die Adressen zum Abbestellen für eine Mail an ein Konto: die Seite der App
/// (Link im Text) und der Ein-Klick-Endpunkt der API (Kopfzeile
/// List-Unsubscribe, RFC 8058). Beides steckt dasselbe Token.
/// </summary>
/// <param name="Seite">Null ohne Frontend:BaseUrl - dann steht kein Link in der Mail.</param>
/// <param name="EinKlickUrl">Null ohne Api:BaseUrl - dann entfallen die Kopfzeilen.</param>
public record AbmeldeAdressen(string? Seite, string? EinKlickUrl)
{
    public static readonly AbmeldeAdressen Keine = new(null, null);

    public static AbmeldeAdressen Fuer(Guid userId, IEmailAbmeldeToken tokens, IFrontendLinks links)
    {
        if (!links.HatBasis && !links.HatApiBasis) return Keine;

        // base64url braucht kein Kodieren; trotzdem, damit ein anderes Tokenformat
        // die Adresse nicht still zerlegt.
        var token = Uri.EscapeDataString(tokens.Erzeuge(userId));
        return new AbmeldeAdressen(
            links.HatBasis ? links.Absolut($"/email-abmelden?t={token}") : null,
            links.ApiAbsolut($"/api/email-abmeldung/ein-klick?t={token}"));
    }
}
