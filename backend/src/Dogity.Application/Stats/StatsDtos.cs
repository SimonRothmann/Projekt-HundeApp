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

/// <summary>
/// Kennzahlen pro Übung eines Hundes - Grundlage der lokalen (rein
/// regelbasierten) Stärken-/Schwächen-Analyse: schwächste Übung zuerst
/// (aufsteigend nach <see cref="AvgRating"/>).
/// </summary>
public record DogExerciseStatDto(
    string ExerciseName,
    int Count,
    double AvgRating,
    /// <summary>Anteil erfolgreicher Durchgänge, 0..1.</summary>
    double SuccessRate,
    /// <summary>
    /// Bewertungstrend: Ø der jüngeren Hälfte minus Ø der älteren Hälfte der
    /// Durchgänge (positiv = Verbesserung). Null bei zu wenigen Durchgängen
    /// (unter 4), um Zufallsschwankungen nicht als Trend zu deuten.
    /// </summary>
    double? RatingTrend,
    DateOnly LastTrained);
