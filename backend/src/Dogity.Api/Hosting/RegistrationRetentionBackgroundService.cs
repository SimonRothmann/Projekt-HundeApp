using Dogity.Application.Community;

namespace Dogity.Api.Hosting;

/// <summary>
/// Löscht täglich Gruppen-Anmeldungen, an denen seit zwölf Monaten nichts mehr
/// passiert ist (siehe <see cref="IGroupRegistrationRetention"/>).
///
/// Als Hintergrunddienst und nicht einmalig beim Start (wie
/// CommunityOrphanCleanup): Der Server läuft wochenlang durch und startet nur
/// bei einem Deploy neu - eine Aufbewahrungsfrist, die nur dann geprüft wird,
/// hielte die Zusage in der Datenschutzerklärung nicht verlässlich ein. Der
/// erste Lauf kommt trotzdem gleich nach dem Start; danach alle 24 Stunden.
/// Gleiche Bauart wie PlanRegenerationBackgroundService.
/// </summary>
public class RegistrationRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RegistrationRetentionBackgroundService> logger) : BackgroundService
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
                var retention = scope.ServiceProvider.GetRequiredService<IGroupRegistrationRetention>();
                var count = await retention.CleanupAsync(ct: stoppingToken);
                if (count > 0)
                    logger.LogInformation("Aufbewahrungsfrist: {Count} Gruppen-Anmeldungen gelöscht.", count);
            }
            catch (OperationCanceledException)
            {
                break; // App fährt herunter - sauber beenden.
            }
            catch (Exception ex)
            {
                // Ein Fehler darf den Dienst nicht dauerhaft stoppen; der nächste Tick versucht es erneut.
                logger.LogError(ex, "Löschen abgelaufener Gruppen-Anmeldungen fehlgeschlagen.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
