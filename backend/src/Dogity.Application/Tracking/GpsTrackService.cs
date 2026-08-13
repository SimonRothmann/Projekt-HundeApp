using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tracking;

/// <summary>
/// Use Cases für die Fährtenaufzeichnung (siehe FEATURE_MODULE.md "Fährte":
/// GPS, Karten, Fährtenhistorie). Eine Fährte gehört immer zu einer
/// Trainingseinheit; Zugriff folgt daher der Zugriffsprüfung des Hundes
/// dieser Trainingseinheit (Besitzer oder betreuender Trainer).
/// </summary>
public class GpsTrackService(IApplicationDbContext db) : IGpsTrackService
{
    public async Task<Result<IReadOnlyList<GpsTrackDto>>> GetByTrainingSessionAsync(Guid userId, Guid trainingSessionId, CancellationToken ct = default)
    {
        if (!await HasSessionAccessAsync(userId, trainingSessionId, ct))
            return Result<IReadOnlyList<GpsTrackDto>>.Failure("Training nicht gefunden.");

        // AsSplitQuery: zwei Collection-Includes (Points + WalkRuns.Points) in
        // EINEM Query erzeugen im JOIN eine kartesische Zeilenexplosion
        // (Zeilen ~ TrackPoints x WalkRunPoints) - bei 300 Punkten Legung und
        // 3 Abläufen à 300 Punkten überträgt Postgres ein Vielfaches der
        // Nutzdaten. Split zerlegt das in schlanke Einzelqueries pro
        // Collection; die Tracks sind append-only pro Nutzer, das
        // Konsistenzfenster zwischen den Teilqueries ist daher unkritisch.
        var tracks = await db.GpsTracks
            .Where(t => t.TrainingSessionId == trainingSessionId)
            .Include(t => t.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Stops)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        // Vereinfachung nur auf dem Lesepfad - der Client hat die Rohpunkte
        // einer soeben erstellten Aufzeichnung (CreateAsync/AddWalkRunAsync)
        // bereits selbst im Speicher, eine Vereinfachung dort brächte nichts.
        return Result<IReadOnlyList<GpsTrackDto>>.Success(tracks.Select(t => ToDto(t, simplify: true)).ToList());
    }

    public async Task<Result<GpsTrackDto>> CreateAsync(Guid userId, CreateGpsTrackRequest request, CancellationToken ct = default)
    {
        if (!await HasSessionAccessAsync(userId, request.TrainingSessionId, ct))
            return Result<GpsTrackDto>.Failure("Training nicht gefunden.");

        if (request.Points.Count == 0)
            return Result<GpsTrackDto>.Failure("Eine Fährte benötigt mindestens einen GPS-Punkt.");

        var track = new GpsTrack
        {
            TrainingSessionId = request.TrainingSessionId,
            LengthMeters = request.LengthMeters,
            AgeMinutes = request.AgeMinutes,
            Surface = request.Surface,
            Weather = request.Weather,
            Wind = request.Wind,
            Comment = request.Comment
        };

        foreach (var point in request.Points)
        {
            track.Points.Add(new GpsPoint
            {
                TrackId = track.Id,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Timestamp = point.Timestamp,
                Accuracy = point.Accuracy,
                PointType = point.PointType,
                Label = point.Label,
                MarkerType = point.MarkerType
            });
        }

        db.GpsTracks.Add(track);
        await db.SaveChangesAsync(ct);

        return Result<GpsTrackDto>.Success(ToDto(track));
    }

    public async Task<Result<GpsWalkRunDto>> AddWalkRunAsync(Guid userId, Guid trackId, CreateGpsWalkRunRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.Failure("Fährte nicht gefunden.");

        if (request.Points.Count == 0)
            return Result<GpsWalkRunDto>.Failure("Ein Ablauf-Versuch benötigt mindestens einen GPS-Punkt.");

        var walkRun = new GpsWalkRun
        {
            TrackId = trackId,
            LengthMeters = request.LengthMeters,
            Comment = request.Comment
        };

        foreach (var point in request.Points)
        {
            walkRun.Points.Add(new GpsWalkPoint
            {
                WalkRunId = walkRun.Id,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Timestamp = point.Timestamp,
                Accuracy = point.Accuracy
            });
        }

        db.GpsWalkRuns.Add(walkRun);
        await db.SaveChangesAsync(ct);

        // Auswertung direkt nach dem Speichern - die gelegten Punkte liegen
        // dafür ohnehin schon in der Datenbank.
        await EvaluateAndPersistAsync(walkRun, ct);
        await db.SaveChangesAsync(ct);

        return Result<GpsWalkRunDto>.Success(ToWalkRunDto(walkRun));
    }

    public async Task<Result<GpsWalkRunDto>> EvaluateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.Failure("Fährte nicht gefunden.");

        var walkRun = await db.GpsWalkRuns
            .Include(r => r.Points)
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunDto>.Failure("Ablauf-Versuch nicht gefunden.");

        await EvaluateAndPersistAsync(walkRun, ct);
        await db.SaveChangesAsync(ct);

        return Result<GpsWalkRunDto>.Success(ToWalkRunDto(walkRun));
    }

    /// <summary>
    /// Wertet einen Ablauf gegen seine gelegte Fährte aus und schreibt das
    /// Ergebnis auf die Entitäten (ohne SaveChanges - der Aufrufer entscheidet,
    /// wann gespeichert wird). Bestehende Halte werden ersetzt.
    /// </summary>
    private async Task EvaluateAndPersistAsync(GpsWalkRun walkRun, CancellationToken ct)
    {
        var laidPoints = await db.GpsPoints
            .Where(p => p.TrackId == walkRun.TrackId)
            .ToListAsync(ct);

        var walkPoints = walkRun.Points.Count > 0
            ? walkRun.Points.ToList()
            : await db.GpsWalkPoints.Where(p => p.WalkRunId == walkRun.Id).ToListAsync(ct);

        var evaluation = GpsTrackEvaluator.Evaluate(laidPoints, walkPoints);

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

        // Alte Halte hart entfernen statt soft-deleten: sie sind reine
        // Rechenergebnisse ohne eigenen Verlauf und würden sonst bei jeder
        // Neuberechnung als Leichen mitwachsen.
        var existingStops = await db.GpsWalkStops.Where(s => s.WalkRunId == walkRun.Id).ToListAsync(ct);
        if (existingStops.Count > 0) db.GpsWalkStops.RemoveRange(existingStops);
        walkRun.Stops.Clear();

        foreach (var stop in evaluation.Stops)
        {
            // Über das DbSet mit explizitem FK, nicht über die getrackte
            // Navigation (sonst stuft EF die neuen Zeilen per Collection-Fixup
            // als Modified statt Added ein - siehe TrainingService.CreateAsync).
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
    }

    public async Task<Result<GpsWalkRunDto>> UpdateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, UpdateGpsWalkRunRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.Failure("Fährte nicht gefunden.");

        var walkRun = await db.GpsWalkRuns
            .Include(r => r.Points)
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunDto>.Failure("Ablauf-Versuch nicht gefunden.");

        // Nur der Kommentar ist editierbar - die GPS-Punkte einer Aufzeichnung
        // sind Messdaten und werden bewusst nicht nachträglich verändert.
        var comment = request.Comment?.Trim();
        walkRun.Comment = string.IsNullOrEmpty(comment) ? null : comment;
        await db.SaveChangesAsync(ct);

        return Result<GpsWalkRunDto>.Success(ToWalkRunDto(walkRun));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid trackId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result.Failure("Fährte nicht gefunden.");

        track.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<bool> HasSessionAccessAsync(Guid userId, Guid trainingSessionId, CancellationToken ct)
    {
        var dogId = await db.TrainingSessions
            .Where(s => s.Id == trainingSessionId)
            .Select(s => s.DogId)
            .FirstOrDefaultAsync(ct);

        return dogId != Guid.Empty && await db.HasDogAccessAsync(userId, dogId, ct);
    }

    private static GpsTrackDto ToDto(GpsTrack t, bool simplify = false)
    {
        var ordered = t.Points.OrderBy(p => p.Timestamp).ToList();
        var points = ordered;
        if (simplify)
        {
            // Manuelle Marker (gelegte Gegenstände) bleiben immer vollständig
            // erhalten - nur die automatische GPS-Linie wird reduziert.
            var automatic = ordered.Where(p => p.PointType != GpsPointType.Manual).ToList();
            var manual = ordered.Where(p => p.PointType == GpsPointType.Manual);
            points = GpsTrackSimplifier.Simplify(automatic).Concat(manual).OrderBy(p => p.Timestamp).ToList();
        }

        return new(
            t.Id,
            t.TrainingSessionId,
            t.LengthMeters,
            t.AgeMinutes,
            t.Surface,
            t.Weather,
            t.Wind,
            t.Comment,
            points.Select(p => new GpsPointDto(p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, p.PointType, p.Label, p.MarkerType)).ToList(),
            t.WalkRuns.OrderBy(r => r.CreatedAt).Select(r => ToWalkRunDto(r, simplify)).ToList());
    }

    private static GpsWalkRunDto ToWalkRunDto(GpsWalkRun r, bool simplify = false)
    {
        var ordered = r.Points.OrderBy(p => p.Timestamp).ToList();
        var points = simplify ? GpsTrackSimplifier.Simplify(ordered) : ordered;

        return new(
            r.Id,
            r.TrackId,
            r.CreatedAt,
            r.LengthMeters,
            r.Comment,
            points.Select(p => new GpsWalkPointDto(p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, p.DeviationMeters)).ToList(),
            r.AvgDeviationMeters,
            r.MaxDeviationMeters,
            r.OnTrackPercent,
            r.ArticlesFound,
            r.ArticlesTotal,
            r.EvaluatedAt,
            r.Stops.OrderByDescending(s => s.DurationSeconds)
                .Select(s => new GpsWalkStopDto(s.Latitude, s.Longitude, s.DurationSeconds, s.Kind, s.MarkerLabel))
                .ToList());
    }
}
