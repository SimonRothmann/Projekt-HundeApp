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

/// <summary>
/// Ein ausgewerteter Fährten-Ablauf für die Entwicklungsansicht.
/// Achtung: gemessen wird die Linie des HUNDEFÜHRERS (siehe GpsTrackEvaluator).
/// </summary>
public record DogTrackRunDto(
    DateOnly Date,
    double AvgDeviationMeters,
    double OnTrackPercent,
    int ArticlesFound,
    int ArticlesTotal,
    int UnexplainedStops);

/// <summary>
/// Fährten-Entwicklung eines Hundes: die jüngsten ausgewerteten Abläufe
/// (chronologisch aufsteigend) plus Trend nach demselben Muster wie
/// <see cref="DogExerciseStatDto.RatingTrend"/> - jüngere gegen ältere Hälfte,
/// erst ab 4 Abläufen, damit Ausreißer nicht als Trend gelten.
/// </summary>
public record DogTrackStatsDto(
    IReadOnlyList<DogTrackRunDto> Runs,
    /// <summary>Negativ = Abweichung sinkt = Verbesserung.</summary>
    double? DeviationTrend,
    /// <summary>Positiv = mehr Zeit auf der Fährte = Verbesserung.</summary>
    double? OnTrackTrend);
