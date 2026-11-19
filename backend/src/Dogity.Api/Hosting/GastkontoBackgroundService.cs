using Dogity.Infrastructure.Identity;

namespace Dogity.Api.Hosting;

/// <summary>
/// Löscht abgelaufene Gastzugänge samt ihren Gast-Vereinen (siehe <see cref="GastkontoService"/>,
/// docs/GASTZUGANG.md). Alle zehn Minuten; der erste Lauf kommt gleich nach dem Start, damit ein Neustart
/// den Rest abgelaufener Gäste einsammelt. Gleiche Bauart wie RegistrationRetentionBackgroundService.
/// </summary>
public class GastkontoBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<GastkontoBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<GastkontoService>().EntferneAbgelaufeneAsync(stoppingToken);
                if (count > 0)
                    logger.LogInformation("Gastzugänge: {Count} abgelaufene Konten gelöscht.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // App fährt herunter - sauber beenden.
            }
            catch (Exception ex)
            {
                // Ein Fehler darf den Dienst nicht dauerhaft stoppen; der nächste Tick versucht es erneut.
                logger.LogError(ex, "Löschen abgelaufener Gastzugänge fehlgeschlagen.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
