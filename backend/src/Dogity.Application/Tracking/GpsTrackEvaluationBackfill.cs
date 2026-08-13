using Dogity.Application.Abstractions;
using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tracking;

/// <inheritdoc />
public class GpsTrackEvaluationBackfill(IApplicationDbContext db) : IGpsTrackEvaluationBackfill
{
    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        var pendingIds = await db.GpsWalkRuns
            .Where(r => r.EvaluatedAt == null)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (pendingIds.Count == 0)
            return 0;

        // Punkte je gelegter Fährte einmal laden und wiederverwenden - mehrere
        // Abläufe teilen sich dieselbe Legung.
        var laidCache = new Dictionary<Guid, List<GpsPoint>>();
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

            var evaluation = GpsTrackEvaluator.Evaluate(laidPoints, walkRun.Points.ToList());

            var byId = walkRun.Points.ToDictionary(p => p.Id);
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

            count++;
        }

        await db.SaveChangesAsync(ct);
        return count;
    }
}
