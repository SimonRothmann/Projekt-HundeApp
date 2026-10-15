using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Email;

namespace Dogity.Api.Hosting;

/// <summary>
/// Versendet die Mails aus der Warteschlange (<see cref="MailQueue"/>), eine nach
/// der anderen. Der Request, der eine Mail auslöst, wartet nie darauf und sieht
/// nie einen Fehler - der Ablauf mit Wiederholung steht in <see cref="MailVersender"/>.
///
/// Gleiche Bauart wie die übrigen Hintergrunddienste (PlanRegenerationBackgroundService).
/// </summary>
public class MailVersandBackgroundService(
    MailQueue queue,
    MailVersender versender,
    IServiceScopeFactory scopeFactory,
    ILogger<MailVersandBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var mail in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    await versender.VersendeAsync(sender, mail, MailVersender.WiederholungsPause, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // MailVersender wirft nicht; das hier fängt nur noch einen Fehler
                    // beim Aufbau des Senders. Der Dienst läuft mit der nächsten Mail weiter.
                    logger.LogError(ex, "Mail-Versand: unerwarteter Fehler.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // App fährt herunter - sauber beenden.
        }
    }
}
