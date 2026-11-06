using Dogity.Application.Abstractions;
using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tracking;

/// <summary>
/// Schreibt das Ergebnis von <see cref="GpsTrackEvaluator"/> auf einen Ablauf - eine Stelle für
/// den Live-Pfad (GpsTrackService) und den Backfill, damit beide dieselben Felder füllen.
/// </summary>
internal static class WalkRunEvaluationWriter
{
    /// <summary>
    /// Schreibt Kennzahlen, Abweichung je Punkt und ersetzt Halte, Schenkel und Winkel (ohne
    /// SaveChanges - der Aufrufer entscheidet, wann gespeichert wird). Die alten Zeilen werden
    /// hart entfernt statt soft-deleted: Sie sind reine Rechenergebnisse ohne eigenen Verlauf und
    /// würden sonst bei jeder Neuberechnung als Leichen mitwachsen.
    /// </summary>
    public static async Task WriteAsync(
        IApplicationDbContext db,
        GpsWalkRun walkRun,
        IReadOnlyList<GpsWalkPoint> walkPoints,
        WalkRunEvaluation evaluation,
        CancellationToken ct)
    {
        var byId = walkPoints.ToDictionary(p => p.Id);
        foreach (var evaluated in evaluation.Points)
        {
            if (byId.TryGetValue(evaluated.PointId, out var point))
                point.DeviationMeters = evaluated.DeviationMeters;
        }

        walkRun.AvgDeviationMeters = evaluation.AvgDeviationMeters;
        walkRun.MaxDeviationMeters = evaluation.MaxDeviationMeters;
        walkRun.OnTrackPercent = evaluation.OnTrackPercent;
        walkRun.ArticlesFound = evaluation.ArticlesFound;
        walkRun.ArticlesTotal = evaluation.ArticlesTotal;
        walkRun.EvaluatedAt = DateTimeOffset.UtcNow;
        walkRun.EvaluationVersion = GpsTrackEvaluator.AktuelleVersion;

        var alteHalte = await db.GpsWalkStops.Where(s => s.WalkRunId == walkRun.Id).ToListAsync(ct);
        if (alteHalte.Count > 0) db.GpsWalkStops.RemoveRange(alteHalte);
        walkRun.Stops.Clear();

        var alteSchenkel = await db.GpsWalkRunLegs.Where(l => l.WalkRunId == walkRun.Id).ToListAsync(ct);
        if (alteSchenkel.Count > 0) db.GpsWalkRunLegs.RemoveRange(alteSchenkel);
        walkRun.Legs.Clear();

        var alteWinkel = await db.GpsWalkRunCorners.Where(c => c.WalkRunId == walkRun.Id).ToListAsync(ct);
        if (alteWinkel.Count > 0) db.GpsWalkRunCorners.RemoveRange(alteWinkel);
        walkRun.Corners.Clear();

        // Über das DbSet mit explizitem FK, nicht über die getrackte Navigation (sonst stuft EF die
        // neuen Zeilen per Collection-Fixup als Modified statt Added ein - siehe
        // TrainingService.CreateAsync).
        foreach (var stop in evaluation.Stops)
        {
            db.GpsWalkStops.Add(new GpsWalkStop
            {
                WalkRunId = walkRun.Id,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                DurationSeconds = stop.DurationSeconds,
                Kind = stop.Kind,
                MarkerLabel = stop.MarkerLabel
            });
        }

        foreach (var leg in evaluation.Legs)
        {
            db.GpsWalkRunLegs.Add(new GpsWalkRunLeg
            {
                WalkRunId = walkRun.Id,
                Number = leg.Number,
                LengthMeters = leg.LengthMeters,
                DurationSeconds = leg.DurationSeconds,
                SpeedMetersPerMinute = leg.SpeedMetersPerMinute,
                AvgDeviationMeters = leg.AvgDeviationMeters
            });
        }

        foreach (var corner in evaluation.Corners)
        {
            db.GpsWalkRunCorners.Add(new GpsWalkRunCorner
            {
                WalkRunId = walkRun.Id,
                Number = corner.Number,
                Direction = corner.Direction,
                Sharp = corner.Sharp,
                TurnDegrees = corner.TurnDegrees,
                Latitude = corner.Latitude,
                Longitude = corner.Longitude,
                Reached = corner.Reached,
                OvershootMeters = corner.OvershootMeters,
                Overshot = corner.Overshot,
                HoldSeconds = corner.HoldSeconds,
                TimeAtCornerSeconds = corner.TimeAtCornerSeconds
            });
        }
    }
}
