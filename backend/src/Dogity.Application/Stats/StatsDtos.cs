namespace Dogity.Application.Stats;

public record WeeklyActivityDto(string Week, int Count);

public record DogStatsDto(
    Guid DogId,
    string DogName,
    int SessionCount,
    int SessionsLast30d,
    int ActiveGoals,
    double? AvgRating30d,
    int PlanItemsCompleted,
    int PlanItemsTotal);

public record DashboardStatsDto(
    IReadOnlyList<WeeklyActivityDto> WeeklyActivity,
    IReadOnlyList<DogStatsDto> PerDog);
