namespace Dogity.Application.Notifications;

/// <summary>
/// Welche Wege nach draußen gerade eingerichtet sind. Beides hängt allein an der
/// Konfiguration (SMTP-Host bzw. VAPID-Schlüssel): Ohne sie bleibt die Glocke
/// in der App der einzige Weg, und der Code verhält sich wie vor der Einführung.
/// </summary>
public interface INotificationChannels
{
    /// <summary>Ob E-Mails tatsächlich versendet werden (SMTP konfiguriert).</summary>
    bool EmailAktiv { get; }

    /// <summary>
    /// Der öffentliche VAPID-Schlüssel (base64url), mit dem sich ein Gerät beim
    /// Push-Dienst anmeldet - null, wenn Push nicht eingerichtet ist. Nur der
    /// öffentliche Teil: Er steht ohnehin in jeder Anmeldung des Browsers.
    /// </summary>
    string? PushPublicKey { get; }
}
