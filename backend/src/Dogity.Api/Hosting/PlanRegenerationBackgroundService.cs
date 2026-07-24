using Dogity.Application.Planning;

namespace Dogity.Api.Hosting;

/// <summary>
/// Hintergrund-Scheduler für die adaptive Trainingsplan-Regenerierung (P4b,
/// siehe docs/SMART_TRAINING_PLAN.md). Prüft täglich und lässt
/// <see cref="IGoalService.RegenerateDuePlansAsync"/> die kommende Woche
/// fälliger Ziele adaptiv neu erzeugen (die eigentliche Wochen-Kadenz steuert
/// LastPlanGeneratedAt, nicht der Tick-Takt). In-Process per PeriodicTimer -
/// für einen einzelnen wiederkehrenden Job bewusst ohne Quartz/Hangfire.
/// </summary>
public class PlanRegenerationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PlanRegenerationBackgroundService> logger) : BackgroundService
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
                var goals = scope.ServiceProvider.GetRequiredService<IGoalService>();
                var count = await goals.RegenerateDuePlansAsync(stoppingToken);
                if (count > 0)
                    logger.LogInformation("Adaptive Trainingsplan-Regenerierung: {Count} Ziele aktualisiert.", count);
            }
            catch (OperationCanceledException)
            {
                break; // App fährt herunter - sauber beenden.
            }
            catch (Exception ex)
            {
                // Ein Fehler darf den Scheduler nicht dauerhaft stoppen; nächster
                // Tick versucht es erneut.
                logger.LogError(ex, "Adaptive Trainingsplan-Regenerierung fehlgeschlagen.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
