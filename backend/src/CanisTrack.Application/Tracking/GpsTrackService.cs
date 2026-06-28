using CanisTrack.Application.Abstractions;
using CanisTrack.Application.Common;
using CanisTrack.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Application.Tracking;

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

        var tracks = await db.GpsTracks
            .Where(t => t.TrainingSessionId == trainingSessionId)
            .Include(t => t.Points)
            .ToListAsync(ct);

        return Result<IReadOnlyList<GpsTrackDto>>.Success(tracks.Select(ToDto).ToList());
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
                Accuracy = point.Accuracy
            });
        }

        db.GpsTracks.Add(track);
        await db.SaveChangesAsync(ct);

        return Result<GpsTrackDto>.Success(ToDto(track));
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

    private static GpsTrackDto ToDto(GpsTrack t) => new(
        t.Id,
        t.TrainingSessionId,
        t.LengthMeters,
        t.AgeMinutes,
        t.Surface,
        t.Weather,
        t.Wind,
        t.Comment,
        t.Points
            .OrderBy(p => p.Timestamp)
            .Select(p => new GpsPointDto(p.Latitude, p.Longitude, p.Timestamp, p.Accuracy))
            .ToList());
}
