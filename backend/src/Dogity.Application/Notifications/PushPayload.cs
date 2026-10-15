using System.Text.Json;

namespace Dogity.Application.Notifications;

/// <summary>
/// Baut den Inhalt einer Push-Nachricht im Format des "Declarative Web Push"
/// (Safari 18.4+): Das Betriebssystem zeigt die Benachrichtigung selbst an und
/// öffnet beim Tippen die Adresse in <c>navigate</c> - ohne dass ein Service
/// Worker laufen muss.
///
/// Abwärtskompatibel: Ältere Browser (und Safari vor 18.4) reichen den JSON-Text
/// an den Service Worker weiter (frontend/public/sw.js), der dasselbe Objekt
/// liest und die Benachrichtigung selbst anzeigt. Deshalb ein einziges Format
/// für alle Geräte.
///
/// Der Inhalt ist der Text der Glockenmeldung, nicht mehr: Wer die Nachricht in
/// der App sehen darf, darf sie auch auf dem Sperrbildschirm lesen - und der
/// Push-Dienst des Herstellers kann sie dank Ende-zu-Ende-Verschlüsselung
/// (RFC 8291) nicht lesen.
/// </summary>
public static class PushPayload
{
    /// <summary>Kennung des Declarative-Web-Push-Formats (RFC 8030-Erweiterung, "web_push": 8030).</summary>
    public const int WebPushKennung = 8030;

    public const string Titel = "Dogity";

    public static string Erzeuge(string nachricht, string navigateUrl, int ungelesen)
    {
        var inhalt = new Dictionary<string, object>
        {
            ["web_push"] = WebPushKennung,
            ["notification"] = new Dictionary<string, object>
            {
                ["title"] = Titel,
                ["body"] = nachricht,
                ["navigate"] = navigateUrl,
                ["lang"] = "de",
                // Die Zahl am App-Symbol auf dem Home-Bildschirm.
                ["app_badge"] = ungelesen,
            },
        };
        return JsonSerializer.Serialize(inhalt);
    }
}
