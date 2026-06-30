using Dogity.Domain.Sports;

namespace Dogity.Application.Training;

public record TrainingExerciseDto(
    Guid Id,
    Guid? ExerciseId,
    /// <summary>
    /// Name der Katalog-Übung, oder bei einem Freitext-Eintrag (ExerciseId
    /// null) direkt der eingegebene Freitext - die Anzeige unterscheidet
    /// beide Fälle nicht, einzig ExerciseId verrät, ob es sich um eine
    /// Katalog-Übung handelt.
    /// </summary>
    string ExerciseName,
    int Rating,
    ExerciseDifficulty Difficulty,
    bool Success,
    string? Notes,
    Guid? TrainingPlanItemId);

public record TrainingSessionDto(
    Guid Id,
    Guid DogId,
    DateOnly Date,
    int DurationMinutes,
    string? Notes,
    IReadOnlyList<TrainingExerciseDto> Exercises,
    string? TrainerFeedback,
    DateTimeOffset? FeedbackAt);

public record SetFeedbackRequest(string Feedback);

public record CreateTrainingExerciseRequest(
    /// <summary>
    /// Genau eines von ExerciseId/FreeTextLabel muss gesetzt sein (siehe
    /// TrainingService.Validate) - FreeTextLabel deckt spontane Spaß-/
    /// Sonstige Übungen ab, die nicht Teil des Katalogs/einer
    /// Prüfungsordnung sind.
    /// </summary>
    Guid? ExerciseId,
    int Rating,
    ExerciseDifficulty Difficulty,
    bool Success,
    string? Notes,
    /// <summary>
    /// Optionaler Bezug zu einem Wochenziel im Trainingsplan (siehe
    /// TrainingExercise.TrainingPlanItemId) - ordnet diesen Tagebucheintrag
    /// einem Plan-Ziel zu, damit dessen Fortschritt sich aus echten
    /// Trainingseinträgen statt einem separaten Haken ergibt. Nur zusammen
    /// mit ExerciseId zulässig, nicht mit FreeTextLabel (ein Plan-Ziel
    /// bezieht sich immer auf eine konkrete Katalog-Übung).
    /// </summary>
    Guid? TrainingPlanItemId = null,
    string? FreeTextLabel = null);

public record CreateTrainingSessionRequest(
    Guid DogId,
    DateOnly Date,
    int DurationMinutes,
    string? Notes,
    IReadOnlyList<CreateTrainingExerciseRequest> Exercises,
    /// <summary>
    /// Optional vom Client vorgegebene Id (siehe ARCHITECTURE.md "Offline
    /// Architektur"): erlaubt es dem Frontend, die Id schon beim Start einer
    /// Fährtenaufnahme zu kennen und sie sofort für den zugehörigen
    /// GpsTrack zu verwenden, ohne auf die Server-Antwort warten zu müssen
    /// - wichtig für die Offline-Warteschlange, da sonst zwei voneinander
    /// abhängige Requests nicht unabhängig nachsynchronisiert werden könnten.
    /// </summary>
    Guid? Id = null);
