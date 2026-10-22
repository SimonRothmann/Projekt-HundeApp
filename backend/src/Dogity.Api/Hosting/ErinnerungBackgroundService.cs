using Dogity.Application.Reminders;

namespace Dogity.Api.Hosting;

/// <summary>
/// Schaut alle zehn Minuten, welche Erinnerungen fällig sind (Termin am Vortag, Fährte noch
/// nicht abgelaufen, offene Rückmeldungen), und lässt sie vom <see cref="IErinnerungService"/>
/// versenden. Der Takt ist nur das Raster; wann etwas fällig ist, steht in den
/// Erinnerungsregeln, und was schon versendet wurde, in der Datenbank - ein Neustart oder
/// ein zweiter Lauf schickt deshalb nichts doppelt. In-Process per PeriodicTimer wie die
/// anderen Dienste (PlanRegenerationBackgroundService).
/// </summary>
public class ErinnerungBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ErinnerungBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                // Eigener Bereich je Lauf: frischer DbContext, kein Stand aus dem vorigen.
                using var scope = scopeFactory.CreateScope();
                var erinnerungen = scope.ServiceProvider.GetRequiredService<IErinnerungService>();
                var gesendet = await erinnerungen.VersendeFaelligeAsync(stoppingToken);
                if (gesendet > 0)
                    logger.LogInformation("Erinnerungen: {Count} versendet.", gesendet);
            }
            catch (OperationCanceledException)
            {
                break; // App fährt herunter - sauber beenden.
            }
            catch (Exception ex)
            {
                // Ein Fehler darf den Dienst nicht dauerhaft stoppen; der nächste Tick versucht es erneut.
                logger.LogError(ex, "Erinnerungen konnten nicht versendet werden.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
