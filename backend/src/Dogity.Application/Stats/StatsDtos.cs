using Dogity.Domain.Training;

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

/// <summary>
/// Was die Verfassung des Hundes mit seinen Bewertungen zu tun hat.
///
/// Zwei Blickwinkel, weil sie zwei verschiedene Fragen beantworten:
/// <see cref="ByCondition"/> - "wie fällt die Bewertung aus, wenn er abgelenkt
/// war?"; <see cref="ByPrecedingDays"/> - "was macht es, wenn schon zwei Tage
/// hintereinander trainiert wurde?". Das Zweite sieht man im Alltag nicht,
/// weil niemand seine Trainingstage im Kopf zusammenzählt.
/// </summary>
public record DogConditionStatsDto(
    IReadOnlyList<ConditionRatingDto> ByCondition,
    IReadOnlyList<TrainingDensityDto> ByPrecedingDays,
    /// <summary>Einheiten mit angegebener Verfassung - Grundlage der Aussagekraft.</summary>
    int SessionsWithCondition,
    int SessionsTotal);

public record ConditionRatingDto(
    DogCondition Condition,
    int SessionCount,
    /// <summary>Ø Selbstbewertung der Übungen dieser Einheiten; null ohne Übungen.</summary>
    double? AvgRating,
    /// <summary>Anteil erfolgreicher Übungen, 0..1; null ohne Übungen.</summary>
    double? SuccessRate);

/// <summary>
/// Bewertungen gruppiert danach, wie viele Tage unmittelbar davor schon
/// trainiert wurde.
/// </summary>
/// <param name="PrecedingTrainingDays">
/// 0 = am Vortag Pause, 1 = ein Trainingstag davor, 2 = zwei oder mehr am Stück.
/// </param>
/// <param name="TiredOrStressedShare">
/// Anteil der Einheiten, in denen der Hund müde oder gestresst war (0..1);
/// null, solange in dieser Gruppe keine Verfassung angegeben wurde.
/// </param>
public record TrainingDensityDto(
    int PrecedingTrainingDays,
    int SessionCount,
    double? AvgRating,
    double? TiredOrStressedShare);
