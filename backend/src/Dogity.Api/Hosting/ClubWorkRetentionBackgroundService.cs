using Dogity.Application.Community;

namespace Dogity.Api.Hosting;

/// <summary>
/// Löscht täglich Arbeitseinsätze, die vor dem Beginn des drittletzten Kalenderjahres liegen (siehe
/// <see cref="IClubWorkRetention"/>). Bauart wie RegistrationRetentionBackgroundService, aber ein eigener
/// Dienst: Beide Läufe haben je eine Frist, ein Protokoll und ein Fehlerbild - in einem gemeinsamen Dienst
/// ließe ein Fehler des einen den anderen an diesem Tag aus.
/// </summary>
public class ClubWorkRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ClubWorkRetentionBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<IClubWorkRetention>();
                var count = await retention.CleanupAsync(ct: stoppingToken);
                if (count > 0)
                    logger.LogInformation("Aufbewahrungsfrist: {Count} Arbeitseinsätze gelöscht.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // App fährt herunter - sauber beenden.
            }
            catch (Exception ex)
            {
                // Ein Fehler darf den Dienst nicht dauerhaft stoppen; der nächste Tick versucht es erneut.
                logger.LogError(ex, "Löschen abgelaufener Arbeitseinsätze fehlgeschlagen.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
