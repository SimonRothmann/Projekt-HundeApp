namespace Dogity.Infrastructure.Push;

/// <summary>
/// Konfiguration für Web Push (Section "Push" in appsettings/.env, siehe
/// .env.example und docs/BETRIEB.md). Das Schlüsselpaar erzeugt
/// <c>scripts/vapid-schluessel.sh</c>. Fehlt etwas davon, ist Push aus: Die
/// Oberfläche blendet den Abschnitt aus (<c>/api/system/features</c> liefert
/// keinen Schlüssel) und das Senden tut nichts.
/// </summary>
public class PushSettings
{
    public const string SectionName = "Push";

    /// <summary>Öffentlicher VAPID-Schlüssel, base64url, unkomprimiert (65 Byte).</summary>
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>Privater VAPID-Schlüssel, base64url (32 Byte). Geheimnis - nie loggen oder ausgeben.</summary>
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>Kontakt für die Push-Dienste, "mailto:..." oder eine https-Adresse (RFC 8292).</summary>
    public string Subject { get; set; } = string.Empty;

    public bool Aktiv =>
        !string.IsNullOrWhiteSpace(VapidPublicKey)
        && !string.IsNullOrWhiteSpace(VapidPrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
