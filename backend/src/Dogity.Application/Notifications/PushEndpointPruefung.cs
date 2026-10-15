namespace Dogity.Application.Notifications;

/// <summary>
/// Prüft, ob eine Push-Adresse zu einem bekannten Push-Dienst gehört.
///
/// Der Server schickt an diese Adresse später eine Anfrage. Nähme er jede
/// beliebige an, könnte jedes Konto den Server dazu bringen, Anfragen an
/// interne Adressen zu senden (Server-Side Request Forgery). Deshalb nur https
/// und nur die Dienste, über die die Browser ihre Push-Nachrichten tatsächlich
/// leiten: Google (Chrome, Edge, Brave, Samsung u. a.), Apple (Safari, Home-
/// Bildschirm-Apps), Mozilla (Firefox) und Microsoft (Windows-Push).
///
/// Kommt ein weiterer Dienst dazu, steht er hier - eine Zeile, bewusst von Hand.
/// </summary>
public static class PushEndpointPruefung
{
    // Genau dieser Host bzw. jede Subdomain davon.
    private static readonly string[] ErlaubteHosts =
    [
        "fcm.googleapis.com",
        "push.apple.com",
        "push.services.mozilla.com",
        "notify.windows.com",
    ];

    public static bool IstErlaubt(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > Common.Textlaengen.PushEndpoint) return false;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        // Keine Zugangsdaten in der Adresse, keine fremden Ports.
        if (!string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;

        var host = uri.IdnHost.ToLowerInvariant();
        return ErlaubteHosts.Any(erlaubt => host == erlaubt || host.EndsWith("." + erlaubt, StringComparison.Ordinal));
    }
}
