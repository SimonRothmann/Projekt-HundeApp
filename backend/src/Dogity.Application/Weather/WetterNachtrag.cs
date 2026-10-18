using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dogity.Application.Weather;

/// <summary>
/// Erledigt EINEN Wetterauftrag aus der Warteschlange: fragt den Wetterdienst
/// und trägt das Ergebnis in das Feld ein, das der Auftrag meint. Wirft nie -
/// der Hintergrunddienst läuft mit dem nächsten Auftrag weiter.
///
/// Der Datensatz kann sich zwischen Einreihen und Eintragen geändert haben
/// (Ort oder Zeit korrigiert, Fährte gelöscht). Deshalb wird nach der Abfrage
/// neu abgeleitet, was der Datensatz JETZT an Abfrage bräuchte: weicht das vom
/// Auftrag ab, gehören die Werte zu einem anderen Ort oder Zeitpunkt und werden
/// verworfen (für die Änderung wurde ein eigener Auftrag eingereiht). Ebenso
/// wird nichts überschrieben, was schon einen Wert hat. Ein gelöschter
/// Datensatz bleibt dank des Query-Filters unsichtbar und wird nie wiederbelebt.
/// </summary>
public class WetterNachtrag(
    IApplicationDbContext db,
    IWeatherProvider weather,
    TimeProvider time,
    ILogger<WetterNachtrag> logger)
{
    /// <returns>true, wenn ein Wert eingetragen wurde.</returns>
    public async Task<bool> NachtragenAsync(WetterAuftrag auftrag, CancellationToken ct = default)
    {
        try
        {
            // Erst fragen, dann den Datensatz laden: Die Abfrage kann Sekunden
            // dauern, und der Vergleich soll den Stand von jetzt sehen.
            var wetter = await weather.GetAtAsync(auftrag.Breite, auftrag.Laenge, auftrag.Zeitpunkt, ct);
            if (wetter is null) return false;

            var jetzt = time.GetUtcNow();
            return auftrag.Ziel == WetterZiel.Training
                ? await TrainingEintragenAsync(auftrag, wetter, jetzt, ct)
                : await FaehrteEintragenAsync(auftrag, wetter, jetzt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Nur Art des Ziels, keine Koordinaten: Der Ort eines Trainings ist personenbezogen.
            logger.LogWarning(ex, "Wetter nachtragen fehlgeschlagen ({Ziel}).", auftrag.Ziel);
            return false;
        }
    }

    private async Task<bool> FaehrteEintragenAsync(WetterAuftrag auftrag, WeatherReading wetter, DateTimeOffset jetzt, CancellationToken ct)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == auftrag.Id, ct);
        if (track is null) return false;
        if (!WetterAuftraege.IstLeer(track, auftrag.Ziel)) return false;

        var aktuell = await WetterAuftraege.FuerFaehrteAsync(db, track, ct);
        if (!aktuell.Contains(auftrag)) return false;

        WetterAuftraege.Eintragen(track, auftrag.Ziel, wetter, jetzt);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> TrainingEintragenAsync(WetterAuftrag auftrag, WeatherReading wetter, DateTimeOffset jetzt, CancellationToken ct)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == auftrag.Id, ct);
        if (session is null || session.TemperatureC is not null) return false;
        if (WetterAuftraege.FuerTraining(session) != auftrag) return false;

        WetterAuftraege.Eintragen(session, wetter, jetzt);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
