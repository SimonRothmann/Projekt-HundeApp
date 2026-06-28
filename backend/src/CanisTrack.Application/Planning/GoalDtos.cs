using CanisTrack.Domain.Planning;

namespace CanisTrack.Application.Planning;

public record TrainingPlanItemDto(
    Guid Id,
    int WeekNumber,
    Guid? ExerciseId,
    string? ExerciseName,
    int RepetitionsTarget,
    bool IsRestWeek);

public record TrainingPlanDto(
    Guid Id,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TrainingPlanItemDto> Items);

public record GoalDto(
    Guid Id,
    Guid DogId,
    Guid SportId,
    string SportName,
    DateOnly TargetDate,
    GoalStatus Status,
    string? Notes,
    TrainingPlanDto? TrainingPlan);

public record CreateGoalRequest(Guid DogId, Guid SportId, DateOnly TargetDate, string? Notes);

public record UpdateGoalStatusRequest(GoalStatus Status);
