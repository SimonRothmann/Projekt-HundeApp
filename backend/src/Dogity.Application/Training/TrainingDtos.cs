using Dogity.Domain.Sports;

namespace Dogity.Application.Training;

public record TrainingExerciseDto(
    Guid Id,
    Guid ExerciseId,
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
    Guid ExerciseId,
    int Rating,
    ExerciseDifficulty Difficulty,
    bool Success,
    string? Notes,
    /// <summary>
    /// Optionaler Bezug zu einem Wochenziel im Trainingsplan (siehe
    /// TrainingExercise.TrainingPlanItemId) - ordnet diesen Tagebucheintrag
    /// einem Plan-Ziel zu, damit dessen Fortschritt sich aus echten
    /// Trainingseinträgen statt einem separaten Haken ergibt.
    /// </summary>
    Guid? TrainingPlanItemId = null);

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
