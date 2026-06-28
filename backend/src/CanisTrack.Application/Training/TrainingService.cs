using CanisTrack.Application.Abstractions;
using CanisTrack.Application.Common;
using CanisTrack.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Application.Training;

/// <summary>
/// Use Cases für das Trainingstagebuch (siehe FEATURE_MODULE.md "Training").
/// Zugriff ist immer auf Trainingseinheiten beschränkt, deren Hund dem
/// aufrufenden Benutzer über <see cref="Domain.Dogs.DogOwner"/> zugeordnet ist.
/// </summary>
public class TrainingService(IApplicationDbContext db) : ITrainingService
{
    public async Task<Result<IReadOnlyList<TrainingSessionDto>>> GetByDogAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<TrainingSessionDto>>.Failure("Hund nicht gefunden.");

        var sessions = await db.TrainingSessions
            .Where(s => s.DogId == dogId)
            .OrderByDescending(s => s.Date)
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Exercise)
            .ToListAsync(ct);

        return Result<IReadOnlyList<TrainingSessionDto>>.Success(sessions.Select(ToDto).ToList());
    }

    public async Task<Result<TrainingSessionDto>> GetByIdAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        return session is null
            ? Result<TrainingSessionDto>.Failure("Training nicht gefunden.")
            : Result<TrainingSessionDto>.Success(ToDto(session));
    }

    public async Task<Result<TrainingSessionDto>> CreateAsync(Guid userId, CreateTrainingSessionRequest request, CancellationToken ct = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return Result<TrainingSessionDto>.Failure(validationError);

        if (!await db.HasDogAccessAsync(userId, request.DogId, ct))
            return Result<TrainingSessionDto>.Failure("Hund nicht gefunden.");

        var exerciseIds = request.Exercises.Select(e => e.ExerciseId).ToList();
        var existingExerciseCount = await db.Exercises.CountAsync(e => exerciseIds.Contains(e.Id), ct);
        if (existingExerciseCount != exerciseIds.Distinct().Count())
            return Result<TrainingSessionDto>.Failure("Eine oder mehrere Übungen wurden nicht gefunden.");

        var session = new TrainingSession
        {
            UserId = userId,
            DogId = request.DogId,
            Date = request.Date,
            DurationMinutes = request.DurationMinutes,
            Notes = request.Notes
        };

        foreach (var exercise in request.Exercises)
        {
            session.Exercises.Add(new TrainingExercise
            {
                TrainingSessionId = session.Id,
                ExerciseId = exercise.ExerciseId,
                Rating = exercise.Rating,
                Difficulty = exercise.Difficulty,
                Success = exercise.Success,
                Notes = exercise.Notes
            });
        }

        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var created = await GetOwnedSessionAsync(userId, session.Id, ct);
        return Result<TrainingSessionDto>.Success(ToDto(created!));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        if (session is null)
            return Result.Failure("Training nicht gefunden.");

        session.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetFeedbackAsync(Guid trainerId, Guid sessionId, SetFeedbackRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Feedback))
            return Result.Failure("Feedback darf nicht leer sein.");

        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
            return Result.Failure("Training nicht gefunden.");

        var isAssignedTrainer = await db.TrainerAssignments.AnyAsync(t => t.DogId == session.DogId && t.TrainerId == trainerId, ct);
        if (!isAssignedTrainer)
            return Result.Failure("Nur ein für diesen Hund zugewiesener Trainer kann Feedback geben.");

        session.TrainerFeedback = request.Feedback.Trim();
        session.FeedbackByTrainerId = trainerId;
        session.FeedbackAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<TrainingSession?> GetOwnedSessionAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        await db.TrainingSessions
            .Where(s => s.Id == sessionId)
            .Where(s =>
                db.DogOwners.Any(o => o.DogId == s.DogId && o.UserId == userId) ||
                db.TrainerAssignments.Any(t => t.DogId == s.DogId && t.TrainerId == userId))
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Exercise)
            .FirstOrDefaultAsync(ct);

    private static string? Validate(CreateTrainingSessionRequest request)
    {
        if (request.Date == default)
            return "Datum ist erforderlich.";
        if (request.DurationMinutes <= 0)
            return "Dauer muss größer als 0 sein.";
        if (request.Exercises.Any(e => e.Rating < 1 || e.Rating > 5))
            return "Bewertung muss zwischen 1 und 5 liegen.";
        return null;
    }

    private static TrainingSessionDto ToDto(TrainingSession s) => new(
        s.Id,
        s.DogId,
        s.Date,
        s.DurationMinutes,
        s.Notes,
        s.Exercises.Select(e => new TrainingExerciseDto(
            e.Id,
            e.ExerciseId,
            e.Exercise?.Name ?? string.Empty,
            e.Rating,
            e.Difficulty,
            e.Success,
            e.Notes)).ToList(),
        s.TrainerFeedback,
        s.FeedbackAt);
}
