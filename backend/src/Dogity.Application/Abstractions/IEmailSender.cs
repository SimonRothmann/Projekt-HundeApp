namespace Dogity.Application.Abstractions;

/// <summary>
/// Abstraktion für ausgehende Transaktions-E-Mails (z.B. Passwort-Reset).
/// Aktive Implementierung ist vorerst <c>LoggingEmailSender</c> (loggt statt
/// zu versenden, siehe Dogity.Infrastructure/Email) - die fertige
/// <c>SmtpEmailSender</c>-Implementierung wird erst registriert, sobald
/// echte SMTP-Zugangsdaten vorliegen.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct = default);
}
