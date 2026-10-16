using Dogity.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Email;

/// <summary>
/// Aktiv, solange "Email:Smtp:Host" nicht gesetzt ist: protokolliert statt zu
/// versenden (lokale Entwicklung, Testumgebung ohne Mailkonto).
///
/// Der Mail-BODY wird nur in Development geloggt: er enthält beim
/// Passwort-Reset den vollständigen Reset-Link inkl. gültigem Token - in
/// Production-Container-Logs (Retention bis zum nächsten Recreate) wäre das
/// für jeden mit Log-Zugriff ein Konto-Übernahme-Vektor. In Development
/// (lokal + Test-VPS) bleibt der Body einsehbar, dort ist der geloggte Link
/// der dokumentierte Weg, den Reset ohne echten E-Mail-Versand
/// durchzuspielen (siehe deploy/README.md "Logs / Status"). Es wird nur die
/// Textfassung geloggt, nie das HTML und nie die Kopfzeilen (die Abmeldung trägt ein Token).
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment environment) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string bodyText, string? bodyHtml = null, IReadOnlyDictionary<string, string>? kopfzeilen = null, CancellationToken ct = default)
    {
        if (environment.IsDevelopment())
        {
            logger.LogInformation(
                "E-Mail-Versand (Platzhalter, kein SMTP konfiguriert) an {ToEmail}, Betreff \"{Subject}\":\n{Body}",
                toEmail, subject, bodyText);
        }
        else
        {
            logger.LogInformation(
                "E-Mail-Versand (Platzhalter, kein SMTP konfiguriert) an {ToEmail}, Betreff \"{Subject}\" - Body unterdrückt (kann Reset-Token enthalten).",
                toEmail, subject);
        }

        return Task.CompletedTask;
    }
}
