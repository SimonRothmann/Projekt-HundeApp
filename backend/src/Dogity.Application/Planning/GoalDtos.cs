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
    // Freitext-Alternative zu ExerciseId (siehe TrainingPlanItem.FreeTextLabel).
    string? FreeTextLabel,
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
    bool IsCustom,
    TrainingPlanDto? TrainingPlan);

public record CreateGoalRequest(Guid DogId, Guid SportId, Guid? RegulationId, DateOnly TargetDate, string? Notes, bool IsCustom = false);

public record UpdateGoalStatusRequest(GoalStatus Status);

/// <summary>
/// Entweder <paramref name="ExerciseId"/> ODER <paramref name="FreeTextLabel"/>
/// setzen. Freitext-Plan-Items landen ohne Exercise-Referenz im Plan und
/// tragen auch keinen Fortschritts-Fortschritt aus Tagebucheinträgen (die
/// verknüpfen sich per PlanItem+ExerciseId).
/// </summary>
public record AddTrainingPlanItemRequest(int WeekNumber, Guid? ExerciseId, string? FreeTextLabel, int RepetitionsTarget);

/// <summary>
/// Übung / Freitext / Woche / Zielwert eines Plan-Ziels bearbeiten. Genau
/// eines von <paramref name="ExerciseId"/> oder <paramref name="FreeTextLabel"/>
/// muss gesetzt sein. Bereits verknüpfte Tagebucheinträge
/// (TrainingExercise.TrainingPlanItemId) bleiben auf dem Plan-Item bestehen -
/// ihr Fortschritt zählt danach für die neue Übung. Das ist bewusst so:
/// eine Umbenennung "Sitz" → "Sitz-Distanz" darf den bisherigen Fortschritt
/// nicht auf null zurücksetzen. Ein echter Wechsel der Übungssemantik sollte
/// als "altes Item entfernen + neues anlegen" gemacht werden.
/// </summary>
public record UpdateTrainingPlanItemRequest(int WeekNumber, Guid? ExerciseId, string? FreeTextLabel, int RepetitionsTarget);
