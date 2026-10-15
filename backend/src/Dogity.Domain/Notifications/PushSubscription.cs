using Dogity.Domain.Common;

namespace Dogity.Domain.Notifications;

/// <summary>
/// Ein Gerät (genauer: ein Browser bzw. eine installierte Web-App), an das
/// Push-Benachrichtigungen einer Person gehen. Je Gerät eine Zeile, nicht je
/// Person: Wer Handy und Rechner nutzt, schaltet sie einzeln ein und aus.
///
/// Der <see cref="Endpoint"/> ist die Adresse beim Push-Dienst des Geräteherstellers
/// (Apple, Google, Mozilla) und zugleich die Kennung des Geräts - deshalb
/// eindeutig. Meldet sich auf demselben Gerät eine andere Person an, wandert
/// die Zeile zu ihr (siehe PushSubscriptionService), statt dass zwei Zeilen
/// dieselbe Adresse tragen.
///
/// <see cref="P256dh"/> und <see cref="Auth"/> sind die Schlüssel des Geräts, mit
/// denen die Nachricht Ende-zu-Ende verschlüsselt wird (RFC 8291). Sie
/// gehören nicht in den Datenexport und nicht ins Log.
/// </summary>
public class PushSubscription : Entity
{
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;

    /// <summary>
    /// Kurze Bezeichnung des Geräts für die Auskunft, z. B. "iPhone (Safari)".
    /// Bewusst nicht der rohe User-Agent: Der verrät mehr, als die Auskunft
    /// braucht, und wäre ein Merkmal zum Wiedererkennen des Geräts.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>Letzte erfolgreiche Zustellung - zeigt, ob das Gerät noch erreichbar ist.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }
}
