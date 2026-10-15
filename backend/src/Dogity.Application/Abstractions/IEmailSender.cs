namespace Dogity.Application.Abstractions;

/// <summary>
/// Abstraktion für ausgehende Transaktions-E-Mails (Willkommen, Passwort
/// zurücksetzen, wichtige Benachrichtigungen).
///
/// Welche Implementierung läuft, entscheidet allein die Konfiguration (siehe
/// Dogity.Infrastructure/DependencyInjection.cs): Ist "Email:Smtp:Host"
/// gesetzt, wird per SMTP versendet, sonst nur protokolliert. Aufgerufen wird
/// sie nicht aus dem Request, sondern vom Hintergrunddienst hinter
/// <see cref="Notifications.IMailQueue"/> - ein langsamer oder abwesender
/// Mailserver soll keine Anfrage aufhalten.
/// </summary>
public interface IEmailSender
{
    /// <param name="bodyText">Textfassung - immer vorhanden, auch für Mailprogramme ohne HTML.</param>
    /// <param name="bodyHtml">Optionale HTML-Fassung; mit ihr entsteht eine multipart/alternative-Mail.</param>
    Task SendAsync(string toEmail, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default);
}
