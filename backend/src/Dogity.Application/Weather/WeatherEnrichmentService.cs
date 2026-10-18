using Dogity.Application.Abstractions;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Weather;

/// <summary>
/// Reichert Trainings und Fährten um Wetterdaten an (siehe
/// <see cref="IWeatherProvider"/>). Zwei Wege:
///
/// - Nach dem Speichern wird das Wetter nur ANGEFORDERT (Enqueue...) und im
///   Hintergrund nachgetragen (<see cref="WetterNachtrag"/>): Speichern wartet
///   nie auf Open-Meteo. Aufrufen NACH dem SaveChanges, sonst kann der Auftrag
///   den Datensatz noch nicht finden.
/// - Auf ausdrücklichen Wunsch (Knopf "Wetter laden") sofort: schreibt nur auf
///   die Fährte, das Speichern übernimmt der Aufrufer.
/// </summary>
public interface IWeatherEnrichmentService
{
    /// <summary>
    /// Fährte: Wetter zum Zeitpunkt des LEGENS und des SUCHENS im Hintergrund
    /// anfordern. Genau diese Differenz ist fachlich entscheidend - sie bestimmt
    /// maßgeblich, wie sich die Geruchsspur hält. Ort und beide Zeitpunkte
    /// stecken bereits in den aufgezeichneten Punkten, es muss also nichts
    /// eingetippt werden. Was schon einen Wert hat, wird nicht erneut angefragt.
    /// </summary>
    Task EnqueueTrackAsync(GpsTrack track, CancellationToken ct = default);

    /// <summary>
    /// Training: Wetter zu Datum + Startzeit am hinterlegten Ort im Hintergrund
    /// anfordern. Ohne Ort oder ohne Startzeit passiert nichts (dann fehlt der
    /// Bezug), ebenso wenn schon ein Wert da ist (nach Änderungen an Datum, Zeit
    /// oder Ort leert der Aufrufer ihn vorher).
    /// </summary>
    void EnqueueSession(TrainingSession session);

    /// <summary>Fährte: Wetter jetzt abrufen und eintragen (Knopf "Wetter laden").</summary>
    Task EnrichTrackAsync(GpsTrack track, CancellationToken ct = default);
}

/// <inheritdoc />
public class WeatherEnrichmentService(IApplicationDbContext db, IWeatherProvider weather, IWetterQueue queue) : IWeatherEnrichmentService
{
    public async Task EnqueueTrackAsync(GpsTrack track, CancellationToken ct = default)
    {
        foreach (var auftrag in await WetterAuftraege.FuerFaehrteAsync(db, track, ct))
        {
            if (WetterAuftraege.IstLeer(track, auftrag.Ziel))
                queue.Enqueue(auftrag);
        }
    }

    public void EnqueueSession(TrainingSession session)
    {
        if (session.TemperatureC is not null) return;
        if (WetterAuftraege.FuerTraining(session) is { } auftrag)
            queue.Enqueue(auftrag);
    }

    public async Task EnrichTrackAsync(GpsTrack track, CancellationToken ct = default)
    {
        var auftraege = await WetterAuftraege.FuerFaehrteAsync(db, track, ct);
        if (auftraege.Count == 0) return;

        foreach (var auftrag in auftraege)
        {
            var wetter = await weather.GetAtAsync(auftrag.Breite, auftrag.Laenge, auftrag.Zeitpunkt, ct);
            if (wetter is not null)
                WetterAuftraege.Eintragen(track, auftrag.Ziel, wetter, DateTimeOffset.UtcNow);
        }

        // Auch bei leerem Ergebnis: Der Abruf wurde versucht.
        track.WeatherFetchedAt = DateTimeOffset.UtcNow;
    }
}
