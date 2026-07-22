using Dogity.Application.Common;

namespace Dogity.Application.Training;

public interface ITrainingService
{
    /// <summary>
    /// Trainings eines Hundes, optional auf einen Datumsbereich beschränkt
    /// (beide Grenzen inklusiv). Ohne from/to: komplette Historie.
    /// </summary>
    Task<Result<IReadOnlyList<TrainingSessionDto>>> GetByDogAsync(Guid userId, Guid dogId, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);
    Task<Result<TrainingSessionDto>> GetByIdAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<Result<TrainingSessionDto>> CreateAsync(Guid userId, CreateTrainingSessionRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Ändert die Tages-/Einheitsnotiz (TrainingSession.Notes) - im Tagebuch
    /// als "Kommentar für den gesamten Tag" bearbeitbar.
    /// </summary>
    Task<Result> UpdateSessionNotesAsync(Guid userId, Guid sessionId, string? notes, CancellationToken ct = default);

    /// <summary>
    /// Ändert die Notiz einer einzelnen durchgeführten Übung (siehe
    /// TrainingExercise.Notes). Editierbar sowohl aus dem Trainingstagebuch
    /// als auch aus dem Trainingsplan-Log. Auth: der Hund der zugehörigen
    /// Trainingseinheit muss dem Nutzer gehören.
    /// </summary>
    Task<Result> UpdateExerciseNotesAsync(Guid userId, Guid exerciseId, string? notes, CancellationToken ct = default);

    /// <summary>Nur für Trainer mit TrainerAssignment auf den Hund - nicht für den Besitzer selbst.</summary>
    Task<Result> SetFeedbackAsync(Guid trainerId, Guid sessionId, SetFeedbackRequest request, CancellationToken ct = default);

    /// <summary>
    /// Setzt die strukturierte Trainer-Bewertung (1-5 + optionale Notiz) einer
    /// einzelnen Übung. Wie <see cref="SetFeedbackAsync"/> nur für einen für den
    /// Hund zugewiesenen Trainer erlaubt, nicht für den Besitzer selbst.
    /// </summary>
    Task<Result> SetExerciseTrainerRatingAsync(Guid trainerId, Guid exerciseId, int rating, string? note, CancellationToken ct = default);

    /// <summary>
    /// Trainings betreuter Hunde (per TrainerAssignment), bei denen noch etwas
    /// offen ist - kein Gesamt-Feedback ODER mindestens eine unbewertete Übung.
    /// Liefert je Training das Gesamt-Feedback und alle Übungen, damit der
    /// Trainer alles in einer Ansicht bewerten kann. Neueste zuerst.
    /// </summary>
    Task<Result<IReadOnlyList<TrainerSessionToRateDto>>> GetSessionsToRateAsync(Guid trainerId, CancellationToken ct = default);
}
