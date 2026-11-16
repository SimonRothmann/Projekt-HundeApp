using Dogity.Application.Betrieb;
using Dogity.Application.Community;

namespace Dogity.Api.Hosting;

/// <summary>
/// Entfernt jede Nacht um 03:30 Uhr Vereinszeit, was die Aufbewahrungsfristen hinter sich hat
/// (<see cref="IAufbewahrungCleanup"/>: gelöschte GPS-Daten nach 30 Tagen, Benachrichtigungen nach 90).
/// Bauart wie <see cref="PlanRegenerationBackgroundService"/> (nächster Lauf über <see cref="NaechsterLauf"/>),
/// aber eine halbe Stunde später, damit sich beide nicht um die Datenbank streiten. Kein Lauf beim Start:
/// Ein Deploy am Abend soll nicht auch noch aufräumen, und die Fristen sind in Tagen gerechnet.
/// </summary>
public class AufbewahrungBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AufbewahrungBackgroundService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeOnly Laufzeit = new(3, 30);

    /// <summary>Der nächste Lauf: das nächste 03:30 Uhr Vereinszeit nach <paramref name="jetzt"/> (genau um 03:30 schon der folgende Tag).</summary>
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
                var jetzt = timeProvider.GetUtcNow();
                await Task.Delay(NaechsterLauf(jetzt) - jetzt, timeProvider, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IAufbewahrungCleanup>();
                var ergebnis = await cleanup.CleanupAsync(timeProvider.GetUtcNow(), stoppingToken);
                logger.LogInformation(
                    "Aufräumen: {Faehrten} gelöschte Fährten und {Benachrichtigungen} Benachrichtigungen endgültig entfernt.",
                    ergebnis.Faehrten, ergebnis.Benachrichtigungen);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // App fährt herunter - sauber beenden.
            }
            catch (Exception ex)
            {
                // Ein Fehler darf den Dienst nicht dauerhaft stoppen; die nächste Nacht versucht es erneut.
                logger.LogError(ex, "Aufräumen nach den Aufbewahrungsfristen fehlgeschlagen.");
            }
        }
    }
}
