using Dogity.Application.Weather;
using Dogity.Infrastructure.Weather;

namespace Dogity.Api.Hosting;

/// <summary>
/// Trägt das Wetter von Fährten und Trainings nach, einen Auftrag nach dem
/// anderen (<see cref="WetterQueue"/>). Wer speichert, wartet nie darauf und
/// sieht nie einen Fehler - der Ablauf mit den Gegenproben steht in
/// <see cref="WetterNachtrag"/>.
///
/// Gleiche Bauart wie die übrigen Hintergrunddienste (MailVersandBackgroundService).
/// </summary>
public class WetterNachtragBackgroundService(
    WetterQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<WetterNachtragBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var auftrag in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Eigener Bereich je Auftrag: frischer DbContext, kein Stand aus dem vorigen.
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<WetterNachtrag>().NachtragenAsync(auftrag, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // WetterNachtrag wirft nicht; das hier fängt nur noch einen Fehler
                    // beim Aufbau des Bereichs. Der Dienst läuft mit dem nächsten Auftrag weiter.
                    logger.LogError(ex, "Wetter nachtragen: unerwarteter Fehler.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // App fährt herunter - sauber beenden.
        }
    }
}
