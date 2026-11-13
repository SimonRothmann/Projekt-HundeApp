using Dogity.Application.Abstractions;
using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dogity.Application.Tracking;

/// <inheritdoc />
public class GpsTrackEvaluationBackfill(IApplicationDbContext db, ILogger<GpsTrackEvaluationBackfill> logger)
    : IGpsTrackEvaluationBackfill
{
    /// <summary>
    /// Nach so vielen Abläufen wird gespeichert: Der Lauf hängt beim Start vor dem Serving, ein
    /// Abbruch mittendrin soll das Geschaffte behalten, und kein einzelnes SaveChanges soll
    /// den ganzen Bestand auf einmal schreiben.
    /// </summary>
    private const int SpeichernAlle = 50;

    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        var pendingIds = await db.GpsWalkRuns
            .Where(r => r.EvaluatedAt == null || r.EvaluationVersion < GpsTrackEvaluator.AktuelleVersion)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (pendingIds.Count == 0)
            return 0;

        // Punkte je gelegter Fährte einmal laden und wiederverwenden - mehrere
        // Abläufe teilen sich dieselbe Legung.
        var laidCache = new Dictionary<Guid, List<GpsPoint>>();
        var kindCache = new Dictionary<Guid, GpsTrackKind>();
        var count = 0;

        foreach (var walkRunId in pendingIds)
        {
            var walkRun = await db.GpsWalkRuns
                .Include(r => r.Points)
                .FirstOrDefaultAsync(r => r.Id == walkRunId, ct);
            if (walkRun is null) continue;

            if (!laidCache.TryGetValue(walkRun.TrackId, out var laidPoints))
            {
                laidPoints = await db.GpsPoints.Where(p => p.TrackId == walkRun.TrackId).ToListAsync(ct);
                laidCache[walkRun.TrackId] = laidPoints;
            }

            // Zu groß für die Auswertung (siehe MaxAuswertungsAufwand): als
            // erledigt markieren, ohne Kennzahlen - wie beim Speichern. Sonst
            // käme derselbe Ablauf bei jedem Serverstart wieder hierher.
            if (!kindCache.TryGetValue(walkRun.TrackId, out var kind))
            {
                kind = await db.GpsTracks.Where(t => t.Id == walkRun.TrackId).Select(t => t.Kind).FirstOrDefaultAsync(ct);
                kindCache[walkRun.TrackId] = kind;
            }

            WalkRunEvaluation? ergebnis;
            try
            {
                ergebnis = GpsTrackEvaluator.TryEvaluate(laidPoints, walkRun.Points.ToList(), kind);
            }
            catch (Exception ex)
            {
                // Ein auffälliger Ablauf darf den Start der App nicht verhindern. Die Version bleibt
                // unverändert, der nächste Start versucht es erneut.
                logger.LogWarning(ex, "Nachauswertung des Ablaufs {WalkRunId} fehlgeschlagen, übersprungen.", walkRunId);
                continue;
            }

            if (ergebnis is not { } evaluation)
            {
                walkRun.EvaluatedAt = DateTimeOffset.UtcNow;
                walkRun.EvaluationVersion = GpsTrackEvaluator.AktuelleVersion;
                continue;
            }

            await WalkRunEvaluationWriter.WriteAsync(db, walkRun, walkRun.Points.ToList(), evaluation, ct);

            count++;
            if (count % SpeichernAlle == 0)
                await db.SaveChangesAsync(ct);
        }

        await db.SaveChangesAsync(ct);
        return count;
    }
}
