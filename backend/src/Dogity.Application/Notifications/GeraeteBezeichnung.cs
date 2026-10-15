namespace Dogity.Application.Notifications;

/// <summary>
/// Macht aus dem User-Agent eine kurze Bezeichnung wie "iPhone (Safari)".
///
/// Gespeichert wird nur diese Bezeichnung, nicht der User-Agent: Er enthält
/// genaue Versionsnummern und wäre ein Merkmal, an dem sich ein Gerät
/// wiedererkennen lässt. Für die Auskunft ("auf welchen Geräten kommen
/// Push-Nachrichten an?") genügt Gerät und Browser.
/// </summary>
public static class GeraeteBezeichnung
{
    public static string? Aus(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;

        var geraet =
            Enthaelt(userAgent, "iPhone") ? "iPhone" :
            Enthaelt(userAgent, "iPad") ? "iPad" :
            Enthaelt(userAgent, "Android") ? "Android" :
            Enthaelt(userAgent, "Windows") ? "Windows" :
            Enthaelt(userAgent, "Macintosh") || Enthaelt(userAgent, "Mac OS X") ? "Mac" :
            Enthaelt(userAgent, "Linux") ? "Linux" :
            null;

        // Reihenfolge zählt: Edge und Opera nennen sich auch "Chrome", Chrome
        // nennt sich auch "Safari".
        var browser =
            Enthaelt(userAgent, "Edg") ? "Edge" :
            Enthaelt(userAgent, "OPR/") ? "Opera" :
            Enthaelt(userAgent, "Firefox") || Enthaelt(userAgent, "FxiOS") ? "Firefox" :
            Enthaelt(userAgent, "Chrome") || Enthaelt(userAgent, "CriOS") ? "Chrome" :
            Enthaelt(userAgent, "Safari") ? "Safari" :
            null;

        var text = (geraet, browser) switch
        {
            (null, null) => null,
            (null, _) => browser,
            (_, null) => geraet,
            _ => $"{geraet} ({browser})",
        };
        return text is { Length: > Common.Textlaengen.GeraeteBezeichnung }
            ? text[..Common.Textlaengen.GeraeteBezeichnung]
            : text;
    }

    private static bool Enthaelt(string text, string teil) => text.Contains(teil, StringComparison.OrdinalIgnoreCase);
}
