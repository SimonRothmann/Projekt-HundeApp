using Dogity.Domain.Planning;

namespace Dogity.Application.Planning;

/// <summary>
/// Ein echter Tagebucheintrag (siehe TrainingExercise), der über
/// TrainingExercise.TrainingPlanItemId als Erfüllung eines Wochenziels
/// markiert wurde - liefert die in TrainingService.ToDto bereits
/// vorhandenen Felder (Bewertung, Erfolg, Kommentar), nur gefiltert auf
/// dieses eine Plan-Ziel statt auf eine ganze Trainingseinheit.
/// </summary>
public record TrainingPlanItemLogDto(
    Guid TrainingSessionId,
    DateOnly Date,
    int Rating,
    bool Success,
    string? Notes);

public record TrainingPlanItemDto(
    Guid Id,
    int WeekNumber,
    Guid? ExerciseId,
    string? ExerciseName,
    int RepetitionsTarget,
    bool IsRestWeek,
    int CompletedCount,
    bool IsComplete,
    IReadOnlyList<TrainingPlanItemLogDto> Logs);

public record TrainingPlanDto(
    Guid Id,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TrainingPlanItemDto> Items);

public record GoalDto(
    Guid Id,
    Guid DogId,
    Guid SportId,
    string SportName,
    Guid? RegulationId,
    string? RegulationName,
    DateOnly TargetDate,
    GoalStatus Status,
    string? Notes,
    TrainingPlanDto? TrainingPlan);

public record CreateGoalRequest(Guid DogId, Guid SportId, Guid? RegulationId, DateOnly TargetDate, string? Notes);

public record UpdateGoalStatusRequest(GoalStatus Status);

public record AddTrainingPlanItemRequest(int WeekNumber, Guid ExerciseId, int RepetitionsTarget);
