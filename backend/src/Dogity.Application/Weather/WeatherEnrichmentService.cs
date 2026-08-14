using Dogity.Application.Abstractions;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Weather;

/// <summary>
/// Reichert Trainings und Fährten um Wetterdaten an (siehe
/// <see cref="IWeatherProvider"/>). Schreibt nur auf die Entitäten - das
/// Speichern übernimmt der Aufrufer, damit die Anreicherung im selben
/// SaveChanges landet wie der eigentliche Vorgang.
/// </summary>
public interface IWeatherEnrichmentService
{
    /// <summary>
    /// Fährte: Wetter zum Zeitpunkt des LEGENS und des SUCHENS. Genau diese
    /// Differenz ist fachlich entscheidend - sie bestimmt maßgeblich, wie sich
    /// die Geruchsspur hält. Ort und beide Zeitpunkte stecken bereits in den
    /// aufgezeichneten Punkten, es muss also nichts eingetippt werden.
    /// </summary>
    Task EnrichTrackAsync(GpsTrack track, CancellationToken ct = default);

    /// <summary>
    /// Training: Wetter zu Datum + Startzeit am hinterlegten Ort. Ohne Ort
    /// oder ohne Startzeit passiert nichts (dann fehlt der Bezug).
    /// </summary>
    Task EnrichSessionAsync(TrainingSession session, CancellationToken ct = default);
}

/// <inheritdoc />
public class WeatherEnrichmentService(IApplicationDbContext db, IWeatherProvider weather) : IWeatherEnrichmentService
{
    public async Task EnrichTrackAsync(GpsTrack track, CancellationToken ct = default)
    {
        var points = track.Points.Count > 0
            ? track.Points.ToList()
            : await db.GpsPoints.Where(p => p.TrackId == track.Id).ToListAsync(ct);

        // Ort und Legezeit aus der automatischen Linie (manuelle Marker tragen
        // ggf. spätere Zeitstempel und liegen neben der Spur).
        var automatic = points.Where(p => p.PointType != GpsPointType.Manual).OrderBy(p => p.Timestamp).ToList();
        if (automatic.Count == 0) return;

        var latitude = automatic[0].Latitude;
        var longitude = automatic[0].Longitude;

        var laid = await weather.GetAtAsync(latitude, longitude, automatic[0].Timestamp, ct);
        if (laid is not null)
        {
            track.LaidTemperatureC = laid.TemperatureC;
            track.LaidRelativeHumidity = laid.RelativeHumidity;
            track.LaidWindSpeedKmh = laid.WindSpeedKmh;
            track.LaidWeatherCode = laid.WeatherCode;
        }

        // Suchzeitpunkt = Beginn des ersten Ablaufs. Solange nicht abgelaufen
        // wurde, bleibt der zweite Wert leer und wird beim ersten Ablauf
        // nachgeholt (siehe GpsTrackService.AddWalkRunAsync).
        var searchStart = await db.GpsWalkPoints
            .Where(p => p.WalkRun!.TrackId == track.Id)
            .OrderBy(p => p.Timestamp)
            .Select(p => (DateTimeOffset?)p.Timestamp)
            .FirstOrDefaultAsync(ct);

        if (searchStart is { } searchAt)
        {
            var search = await weather.GetAtAsync(latitude, longitude, searchAt, ct);
            if (search is not null)
            {
                track.SearchTemperatureC = search.TemperatureC;
                track.SearchRelativeHumidity = search.RelativeHumidity;
                track.SearchWindSpeedKmh = search.WindSpeedKmh;
                track.SearchWeatherCode = search.WeatherCode;
            }
        }

        track.WeatherFetchedAt = DateTimeOffset.UtcNow;
    }

    public async Task EnrichSessionAsync(TrainingSession session, CancellationToken ct = default)
    {
        if (session.Latitude is not { } lat || session.Longitude is not { } lon) return;
        if (session.StartTime is not { } startTime) return;

        // Lokale Eingabe als UTC interpretieren: die App speichert bewusst
        // keine Zeitzone je Training. Für Mitteleuropa liegt der Fehler bei
        // 1-2 Stunden - bei stündlichen Wetterwerten vertretbar, und deutlich
        // ehrlicher als eine Zeitzone zu erfinden.
        var instant = new DateTimeOffset(session.Date.ToDateTime(startTime), TimeSpan.Zero);

        var reading = await weather.GetAtAsync(lat, lon, instant, ct);
        if (reading is null) return;

        session.TemperatureC = reading.TemperatureC;
        session.RelativeHumidity = reading.RelativeHumidity;
        session.WindSpeedKmh = reading.WindSpeedKmh;
        session.WeatherCode = reading.WeatherCode;
        session.WeatherFetchedAt = DateTimeOffset.UtcNow;
    }
}
