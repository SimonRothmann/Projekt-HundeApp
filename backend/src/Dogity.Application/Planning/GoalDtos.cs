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
    // Id der durchgeführten Übung (TrainingExercise) - nötig, damit die Notiz
    // auch aus dem Plan-Log heraus bearbeitet werden kann (PUT
    // /api/trainings/exercises/{id}/notes), nicht nur im Trainingstagebuch.
    Guid TrainingExerciseId,
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
    IReadOnlyList<TrainingPlanItemLogDto> Logs,
    // Warum der adaptive Generator diese Übung geplant hat (Schwäche/
    // Wiederholung/Neu) - für ein informatives Badge in der UI. Null bei
    // manuellen Einträgen/Pausenwochen.
    PlanItemReason? Reason,
    // Welcher Trainingstag der Woche (1..Goal.TrainingDaysPerWeek).
    int DayIndex);

public record TrainingPlanDto(
    Guid Id,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<TrainingPlanItemDto> Items);

/// <summary>
/// Pro-Woche-Überschreibung der Trainingstage (siehe TrainingPlanWeekConfig).
/// Nur Wochen mit abweichendem Wert sind enthalten; alle übrigen nutzen
/// <see cref="GoalDto.TrainingDaysPerWeek"/>.
/// </summary>
public record WeekConfigDto(int WeekNumber, int TrainingDaysPerWeek);

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
    int WeeklyExerciseCount,
    int TrainingDaysPerWeek,
    IReadOnlyList<WeekConfigDto> WeekConfigs,
    TrainingPlanDto? TrainingPlan);

public record CreateGoalRequest(Guid DogId, Guid SportId, Guid? RegulationId, DateOnly TargetDate, string? Notes, bool IsCustom = false);

public record UpdateGoalStatusRequest(GoalStatus Status);

/// <summary>
/// Eine gewichtbare Übung eines Ziels ("mehr/weniger üben"): alle Übungen der
/// Prüfungsordnung (bzw. der Sportart bei individuellen Zielen) mit ihrer
/// aktuellen manuellen Gewichtung, dem Beherrschungs-Status und ob sie in der
/// laufenden Woche geplant ist.
/// </summary>
public record WeightableExerciseDto(
    Guid ExerciseId,
    string ExerciseName,
    // ExerciseDifficulty als Zahl (0=Einsteiger,1=Fortgeschritten,2=Erfahren).
    int Difficulty,
    // Manuelle Gewichtung −2..+2 (0 = normal).
    int ManualPriority,
    // 0 = noch nie trainiert, 1 = hängt, 2 = mittel, 3 = sitzt (aus Leitner-Box).
    int MasteryStatus,
    bool PlannedThisWeek);

/// <summary>Setzt die manuelle Gewichtung einer Übung (−2..+2).</summary>
public record SetExercisePriorityRequest(int Value);

/// <summary>
/// Generiert die angegebene Woche des Plans adaptiv neu (siehe
/// docs/SMART_TRAINING_PLAN.md). Manuelle/Trainer-Items und Auto-Items mit
/// bereits geloggtem Fortschritt bleiben erhalten; nur fortschrittslose
/// Auto-Items werden durch eine frische, mastery-basierte Auswahl ersetzt.
/// </summary>
public record RegenerateWeekRequest(int WeekNumber);

/// <summary>
/// Plan-Konfiguration eines Ziels (siehe docs/SMART_TRAINING_PLAN.md): wie
/// viele Übungen pro Woche und auf wie viele Trainingstage verteilt der
/// adaptive Generator plant.
/// </summary>
public record UpdateGoalConfigRequest(int WeeklyExerciseCount, int TrainingDaysPerWeek);

/// <summary>
/// Setzt die Trainingstage für EINE Woche abweichend vom Plan-Default (siehe
/// TrainingPlanWeekConfig). Bestehende Übungen der Woche, die auf einem nun
/// nicht mehr vorhandenen Tag lägen, werden auf den letzten gültigen Tag geholt.
/// </summary>
public record UpdateWeekConfigRequest(int TrainingDaysPerWeek);

/// <summary>
/// Entweder <paramref name="ExerciseId"/> ODER <paramref name="FreeTextLabel"/>
/// setzen. Freitext-Plan-Items landen ohne Exercise-Referenz im Plan und
/// tragen auch keinen Fortschritts-Fortschritt aus Tagebucheinträgen (die
/// verknüpfen sich per PlanItem+ExerciseId).
/// </summary>
public record AddTrainingPlanItemRequest(int WeekNumber, Guid? ExerciseId, string? FreeTextLabel, int RepetitionsTarget, int DayIndex = 1);

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
public record UpdateTrainingPlanItemRequest(int WeekNumber, Guid? ExerciseId, string? FreeTextLabel, int RepetitionsTarget, int DayIndex = 1);
