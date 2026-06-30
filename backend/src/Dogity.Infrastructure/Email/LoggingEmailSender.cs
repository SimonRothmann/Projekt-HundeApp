using Dogity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Email;

/// <summary>
/// Platzhalter-Implementierung, solange kein echter SMTP-Versand
/// angebunden ist: loggt die Mail (inkl. Reset-Link/Token) statt sie zu
/// verschicken - im Dev-Log bzw. auf der Test-VPS in den Container-Logs
/// einsehbar (siehe deploy/README.md "Logs / Status"). Umschalten auf
/// echten Versand: SmtpEmailSender statt dieser Klasse in
/// DependencyInjection.cs registrieren, sobald SMTP-Zugangsdaten vorliegen.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string bodyText, CancellationToken ct = default)
    {
        logger.LogInformation(
            "E-Mail-Versand (Platzhalter, kein SMTP konfiguriert) an {ToEmail}, Betreff \"{Subject}\":\n{Body}",
            toEmail, subject, bodyText);

        return Task.CompletedTask;
    }
}
