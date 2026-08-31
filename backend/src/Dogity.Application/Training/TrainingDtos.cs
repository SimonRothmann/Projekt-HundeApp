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
    Guid? TrainingPlanItemId,
    /// <summary>
    /// Bewertung eines zugewiesenen Trainers (1-5), getrennt von der
    /// Selbstbewertung <see cref="Rating"/>. Null, solange kein Trainer die
    /// Übung bewertet hat (siehe TrainingExercise.TrainerRating).
    /// </summary>
    int? TrainerRating,
    string? TrainerNote);

public record TrainingSessionDto(
    Guid Id,
    Guid DogId,
    DateOnly Date,
    int DurationMinutes,
    string? Notes,
    IReadOnlyList<TrainingExerciseDto> Exercises,
    string? TrainerFeedback,
    DateTimeOffset? FeedbackAt,
    // Uhrzeit + Ort: Grundlage der automatischen Wetter-Ermittlung. Beide
    // optional, weil Trainings auch nachgetragen werden.
    TimeOnly? StartTime,
    double? Latitude,
    double? Longitude,
    string? LocationName,
    double? TemperatureC,
    int? RelativeHumidity,
    double? WindSpeedKmh,
    int? WeatherCode,
    /// <summary>
    /// Ob zu diesem Training mindestens eine Fährte (GpsTrack) existiert.
    /// Erspart dem Frontend einen GPS-Request pro Trainings-Karte, nur um
    /// festzustellen, dass es nichts anzuzeigen gibt (HTTP-N+1 auf der
    /// Hundeseite, siehe TODO.md Roadmap 5).
    /// </summary>
    bool HasGpsTrack);

public record SetFeedbackRequest(string Feedback);

/// <summary>
/// Strukturierte Trainer-Bewertung einer einzelnen Übung (1-5 Sterne + optionale
/// Notiz), siehe TrainingService.SetExerciseTrainerRatingAsync.
/// </summary>
public record SetExerciseTrainerRatingRequest(int Rating, string? Note);

public record UpdateExerciseNotesRequest(string? Notes);

/// <summary>
/// Eine bereits erfasste Übung nachträglich korrigieren - Bewertung, Erfolg
/// und Notiz in einem Zug. Bis hierher ließ sich nur die Notiz ändern: wer
/// sich beim Eintragen vertippt hatte, musste den ganzen Trainingstag löschen
/// und neu erfassen.
/// </summary>
public record UpdateTrainingExerciseRequest(int Rating, bool Success, string? Notes);

public record UpdateSessionNotesRequest(string? Notes);

/// <summary>
/// Ein vom Trainer zu bewertendes Training eines betreuten Hundes: Gesamt-
/// Feedback UND alle Übungen in einer Ansicht, damit der Trainer alles auf
/// einen Blick bewerten kann. Erscheint auf der Trainerseite, solange noch
/// etwas offen ist - kein Gesamt-Feedback ODER mindestens eine unbewertete
/// Übung. HandlerName = Hundeführer, Rating je Übung = dessen Selbstbewertung.
/// </summary>
public record TrainerSessionToRateDto(
    Guid SessionId,
    Guid DogId,
    string DogName,
    string HandlerName,
    DateOnly Date,
    int DurationMinutes,
    string? TrainerFeedback,
    IReadOnlyList<TrainerSessionExerciseDto> Exercises);

public record TrainerSessionExerciseDto(
    Guid ExerciseId,
    string ExerciseName,
    int Rating,
    bool Success,
    int? TrainerRating,
    string? TrainerNote);

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
    /// Trainingseinträgen statt einem separaten Haken ergibt. Die Art muss
    /// zum Plan-Ziel passen: Katalog-Übung zu Katalog-Plan-Ziel, Freitext zu
    /// Freitext-Plan-Ziel (geprüft in TrainingService.ValidatePlanItemsAsync).
    /// </summary>
    Guid? TrainingPlanItemId = null,
    string? FreeTextLabel = null);

/// <summary>
/// Ort + Uhrzeit eines Trainings setzen (nachträglich möglich) - löst die
/// automatische Wetter-Ermittlung aus.
/// </summary>
public record UpdateSessionContextRequest(
    TimeOnly? StartTime,
    double? Latitude,
    double? Longitude,
    string? LocationName);

/// <summary>
/// Datum eines Trainings korrigieren - Trainings werden oft erst abends oder
/// Tage später nachgetragen und landen dann auf dem falschen Tag.
///
/// Bewusst ein eigener Request und nicht Teil von
/// <see cref="UpdateSessionContextRequest"/>: das Tagebuch verschiebt einen
/// ganzen Trainingstag und hat Ort und Uhrzeit der einzelnen Einheiten dabei
/// gar nicht in der Hand - müsste es sie mitschicken, könnte es sie mit einem
/// veralteten Stand überschreiben.
/// </summary>
public record UpdateSessionDateRequest(DateOnly Date);

/// <summary>
/// Ein Ort, an dem schon trainiert wurde. Hundeführer trainieren fast immer an
/// denselben zwei bis fünf Plätzen - deshalb ist die Liste der letzten Orte in
/// der Praxis wertvoller als jede Suche: beim zweiten Mal genügt ein Tippen.
/// </summary>
public record RecentLocationDto(string Name, double Latitude, double Longitude, DateOnly LastUsed);

public record CreateTrainingSessionRequest(
    Guid DogId,
    DateOnly Date,
    int DurationMinutes,
    string? Notes,
    IReadOnlyList<CreateTrainingExerciseRequest> Exercises,
    TimeOnly? StartTime = null,
    double? Latitude = null,
    double? Longitude = null,
    string? LocationName = null,
    /// <summary>
    /// Optional vom Client vorgegebene Id (siehe ARCHITECTURE.md "Offline
    /// Architektur"): erlaubt es dem Frontend, die Id schon beim Start einer
    /// Fährtenaufnahme zu kennen und sie sofort für den zugehörigen
    /// GpsTrack zu verwenden, ohne auf die Server-Antwort warten zu müssen
    /// - wichtig für die Offline-Warteschlange, da sonst zwei voneinander
    /// abhängige Requests nicht unabhängig nachsynchronisiert werden könnten.
    /// </summary>
    Guid? Id = null);
