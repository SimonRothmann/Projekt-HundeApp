using Dogity.Application.Abstractions;
using Dogity.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Email;

/// <summary>
/// Versendet EINE Mail aus der Warteschlange: ein Versuch, bei Fehler nach
/// kurzer Pause ein zweiter, dann wird sie verworfen. Nie eine Ausnahme nach
/// außen - der Hintergrunddienst läuft einfach mit der nächsten Mail weiter.
///
/// Eigene Klasse und nicht im Hintergrunddienst, damit sich der Ablauf ohne
/// laufenden Host testen lässt (mit Pause null und einem Sender, der scheitert).
/// </summary>
public class MailVersender(ILogger<MailVersender> logger)
{
    public static readonly TimeSpan WiederholungsPause = TimeSpan.FromSeconds(30);

    /// <returns>true, wenn die Mail versendet wurde.</returns>
    public async Task<bool> VersendeAsync(IEmailSender sender, MailNachricht mail, TimeSpan pause, CancellationToken ct)
    {
        for (var versuch = 1; versuch <= 2; versuch++)
        {
            try
            {
                await sender.SendAsync(mail.An, mail.Betreff, mail.Text, mail.Html, ct);
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // App fährt herunter - nicht als Versandfehler zählen.
                return false;
            }
            catch (Exception ex)
            {
                // Nur Art und Versuch, weder Empfänger noch Betreff noch Inhalt:
                // Betreff und Text können einen Reset-Link oder Namen enthalten.
                logger.LogWarning(ex, "E-Mail-Versand fehlgeschlagen (Versuch {Versuch} von 2).", versuch);
                if (versuch == 2) break;

                try
                {
                    await Task.Delay(pause, ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        logger.LogError("E-Mail wurde nach zwei Versuchen verworfen.");
        return false;
    }
}
