namespace Dogity.Infrastructure.Email;

/// <summary>
/// Konfiguration für <see cref="SmtpEmailSender"/> (Section "Email:Smtp" in
/// appsettings/.env, siehe .env.example und docs/BETRIEB.md). Ist
/// <see cref="Host"/> gesetzt, wird per SMTP versendet - sonst nur
/// protokolliert (<see cref="LoggingEmailSender"/>). Einen Codewechsel zum
/// Aktivieren gibt es nicht; es genügt die Konfiguration.
///
/// Anbieterneutral. Im Betrieb ist es der eigene Versandserver im
/// Compose-Netz (deploy/mail, Host "mail", Port 25, ohne Anmeldung); ein
/// externer Relay mit Benutzername und Passwort geht ebenso (dann mit
/// STARTTLS-Pflicht, siehe <see cref="SmtpEmailSender.Verschluesselung"/>).
/// </summary>
public class SmtpSettings
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Dogity";

    /// <summary>
    /// Wohin Antworten auf unsere Mails gehen (optional). Die Absenderadresse
    /// (noreply@) ist kein Postfach - mit der Antwortadresse (info@dogity.net,
    /// per Cloudflare Email Routing weitergeleitet) erreicht "Antworte einfach auf diese
    /// Mail" tatsächlich jemanden.
    /// </summary>
    public string ReplyToAddress { get; set; } = string.Empty;

    /// <summary>Ob der Versand eingerichtet ist - allein der Host entscheidet.</summary>
    public bool Aktiv => !string.IsNullOrWhiteSpace(Host);
}
