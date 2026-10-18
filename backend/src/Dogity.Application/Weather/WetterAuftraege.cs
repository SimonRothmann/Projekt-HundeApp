using Dogity.Application.Abstractions;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Weather;

/// <summary>
/// Wie aus einer Fährte oder einem Training die Wetterabfragen entstehen und
/// wohin das Ergebnis gehört. Eine Stelle für alle drei Wege: das Einreihen
/// nach dem Speichern, das Nachtragen im Hintergrund (das dieselbe Ableitung
/// zur Gegenprobe nutzt) und die sofortige Aktualisierung per Knopf.
/// </summary>
internal static class WetterAuftraege
{
    /// <summary>
    /// Fährte: Legen (Ort und Zeit des ersten automatischen Punkts - manuelle
    /// Marker tragen ggf. spätere Zeitstempel und liegen neben der Spur) und,
    /// sobald es einen Ablauf gibt, Suchen (Beginn des ersten Ablaufs). Solange
    /// nicht abgelaufen wurde, bleibt Suchen aus und kommt mit dem ersten Ablauf
    /// (siehe GpsTrackService.AddWalkRunAsync).
    /// </summary>
    public static async Task<IReadOnlyList<WetterAuftrag>> FuerFaehrteAsync(
        IApplicationDbContext db, GpsTrack track, CancellationToken ct)
    {
        var start = track.Points.Count > 0
            ? track.Points.Where(p => p.PointType != GpsPointType.Manual).OrderBy(p => p.Timestamp)
                .Select(p => new { p.Latitude, p.Longitude, p.Timestamp }).FirstOrDefault()
            : await db.GpsPoints
                .Where(p => p.TrackId == track.Id && p.PointType != GpsPointType.Manual)
                .OrderBy(p => p.Timestamp)
                .Select(p => new { p.Latitude, p.Longitude, p.Timestamp })
                .FirstOrDefaultAsync(ct);
        if (start is null) return [];

        var auftraege = new List<WetterAuftrag>
        {
            new(WetterZiel.FaehrteGelegt, track.Id, start.Latitude, start.Longitude, AufSekunden(start.Timestamp))
        };

        var suchzeitpunkt = await db.GpsWalkPoints
            .Where(p => p.WalkRun!.TrackId == track.Id)
            .OrderBy(p => p.Timestamp)
            .Select(p => (DateTimeOffset?)p.Timestamp)
            .FirstOrDefaultAsync(ct);
        if (suchzeitpunkt is { } gesucht)
            auftraege.Add(new(WetterZiel.FaehrteGesucht, track.Id, start.Latitude, start.Longitude, AufSekunden(gesucht)));

        return auftraege;
    }

    /// <summary>
    /// Training: Datum + Startzeit am hinterlegten Ort. Ohne Ort oder ohne
    /// Startzeit fehlt der Bezug - dann gibt es keinen Auftrag.
    /// </summary>
    public static WetterAuftrag? FuerTraining(TrainingSession session)
    {
        if (session.Latitude is not { } breite || session.Longitude is not { } laenge) return null;
        if (session.StartTime is not { } startzeit) return null;

        // Lokale Eingabe als UTC interpretieren: die App speichert bewusst
        // keine Zeitzone je Training. Für Mitteleuropa liegt der Fehler bei
        // 1-2 Stunden - bei stündlichen Wetterwerten vertretbar, und deutlich
        // ehrlicher als eine Zeitzone zu erfinden.
        var zeitpunkt = AufSekunden(new DateTimeOffset(session.Date.ToDateTime(startzeit), TimeSpan.Zero));
        return new WetterAuftrag(WetterZiel.Training, session.Id, breite, laenge, zeitpunkt);
    }

    /// <summary>
    /// Schneidet den Zeitpunkt auf ganze Sekunden: Die Gegenprobe vergleicht den
    /// Auftrag (Wert im Speicher) mit dem aus der Datenbank gelesenen Wert, und
    /// timestamptz kennt nur Mikrosekunden. Der Wetterdienst liefert ohnehin
    /// Stundenwerte.
    /// </summary>
    private static DateTimeOffset AufSekunden(DateTimeOffset zeitpunkt) =>
        zeitpunkt.AddTicks(-(zeitpunkt.Ticks % TimeSpan.TicksPerSecond));

    /// <summary>Ob das Zielfeld des Auftrags noch leer ist - nur Leeres wird je gefüllt.</summary>
    public static bool IstLeer(GpsTrack track, WetterZiel ziel) => ziel switch
    {
        WetterZiel.FaehrteGelegt => track.LaidTemperatureC is null,
        WetterZiel.FaehrteGesucht => track.SearchTemperatureC is null,
        _ => false
    };

    public static void Eintragen(GpsTrack track, WetterZiel ziel, WeatherReading wetter, DateTimeOffset jetzt)
    {
        if (ziel == WetterZiel.FaehrteGelegt)
        {
            track.LaidTemperatureC = wetter.TemperatureC;
            track.LaidRelativeHumidity = wetter.RelativeHumidity;
            track.LaidWindSpeedKmh = wetter.WindSpeedKmh;
            track.LaidWeatherCode = wetter.WeatherCode;
        }
        else
        {
            track.SearchTemperatureC = wetter.TemperatureC;
            track.SearchRelativeHumidity = wetter.RelativeHumidity;
            track.SearchWindSpeedKmh = wetter.WindSpeedKmh;
            track.SearchWeatherCode = wetter.WeatherCode;
        }

        track.WeatherFetchedAt = jetzt;
    }

    public static void Eintragen(TrainingSession session, WeatherReading wetter, DateTimeOffset jetzt)
    {
        session.TemperatureC = wetter.TemperatureC;
        session.RelativeHumidity = wetter.RelativeHumidity;
        session.WindSpeedKmh = wetter.WindSpeedKmh;
        session.WeatherCode = wetter.WeatherCode;
        session.WeatherFetchedAt = jetzt;
    }
}
