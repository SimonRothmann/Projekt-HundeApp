using Dogity.Application.Notifications;
using Dogity.Infrastructure.Push;

namespace Dogity.Api.Hosting;

/// <summary>
/// Sendet die Push-Aufträge aus der Warteschlange (<see cref="PushQueue"/>).
/// Je Auftrag ein eigener Scope: Der <see cref="IPushDispatcher"/> braucht die
/// Datenbank (Geräte laden, tote Geräte entfernen). Wie beim Mailversand wartet
/// der auslösende Request nie darauf.
/// </summary>
public class PushVersandBackgroundService(
    PushQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PushVersandBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var auftrag in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IPushDispatcher>().SendeAsync(auftrag, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Ohne Inhalt des Auftrags - er trägt den Text der Meldung.
                    logger.LogError(ex, "Push-Versand: unerwarteter Fehler.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // App fährt herunter - sauber beenden.
        }
    }
}
