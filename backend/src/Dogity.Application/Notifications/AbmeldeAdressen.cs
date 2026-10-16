namespace Dogity.Application.Notifications;

/// <summary>
/// Die Seite zum Abbestellen für eine Mail an ein Konto - als Link im Mailtext.
///
/// Bewusst OHNE die Kopfzeilen List-Unsubscribe/List-Unsubscribe-Post
/// (RFC 8058, "Abbestellen" im Mailprogramm): Mit ihnen sortierte Gmail die
/// Willkommensmail unter "Werbung" (Test, 2026-10-16). Die Kopfzeilen
/// kennzeichnen Massen- und Werbemail; für Mails zum eigenen Konto verlangt
/// sie niemand, und der Link im Text erfüllt den Zweck.
/// </summary>
public static class AbmeldeAdressen
{
    /// <returns>Null ohne Frontend:BaseUrl - dann steht kein Link in der Mail.</returns>
    public static string? Seite(Guid userId, IEmailAbmeldeToken tokens, IFrontendLinks links)
    {
        if (!links.HatBasis) return null;

        // base64url braucht kein Kodieren; trotzdem, damit ein anderes Tokenformat
        // die Adresse nicht still zerlegt.
        var token = Uri.EscapeDataString(tokens.Erzeuge(userId));
        return links.Absolut($"/email-abmelden?t={token}");
    }
}
