using Dogity.Application.Community;
using Dogity.Application.Planning;

namespace Dogity.Api.Hosting;

/// <summary>
/// Hintergrund-Scheduler für die adaptive Trainingsplan-Regenerierung (P4b,
/// siehe docs/SMART_TRAINING_PLAN.md). Läuft beim Start und danach jede Nacht um
/// 03:00 Uhr deutscher Zeit (nicht "24 Stunden nach dem Start": Nach einem
/// Abend-Deploy fiele der Lauf sonst jeden Abend in die Hauptnutzungszeit) und lässt
/// <see cref="IGoalService.RegenerateDuePlansAsync"/> die kommende Woche
/// fälliger Ziele adaptiv neu erzeugen (die eigentliche Wochen-Kadenz steuert
/// LastPlanGeneratedAt, nicht der Takt). In-Process - für einen einzelnen
/// wiederkehrenden Job bewusst ohne Quartz/Hangfire.
/// </summary>
public class PlanRegenerationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PlanRegenerationBackgroundService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeOnly Laufzeit = new(3, 0);

    /// <summary>
    /// Der nächste Lauf: das nächste 03:00 Uhr Vereinszeit nach <paramref name="jetzt"/>
    /// (genau um 03:00 schon der folgende Tag). 03:00 gibt es bei jeder Zeitumstellung
    /// genau einmal, die Umrechnung ist daher eindeutig.
    /// </summary>
    public static DateTimeOffset NaechsterLauf(DateTimeOffset jetzt)
    {
        var lokal = Vereinszeit.Lokal(jetzt);
        var tag = DateOnly.FromDateTime(lokal);
        if (TimeOnly.FromDateTime(lokal) >= Laufzeit)
            tag = tag.AddDays(1);
        var wanduhr = DateTime.SpecifyKind(tag.ToDateTime(Laufzeit), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(wanduhr, Vereinszeit.Zone), TimeSpan.Zero);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var goals = scope.ServiceProvider.GetRequiredService<IGoalService>();
                var count = await goals.RegenerateDuePlansAsync(stoppingToken);
                if (count > 0)
                    logger.LogInformation("Adaptive Trainingsplan-Regenerierung: {Count} Ziele aktualisiert.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // App fährt herunter - sauber beenden.
            }
            catch (Exception ex)
            {
                // Ein Fehler darf den Scheduler nicht dauerhaft stoppen; der
                // nächste Lauf versucht es erneut.
                logger.LogError(ex, "Adaptive Trainingsplan-Regenerierung fehlgeschlagen.");
            }

            try
            {
                var jetzt = timeProvider.GetUtcNow();
                await Task.Delay(NaechsterLauf(jetzt) - jetzt, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
