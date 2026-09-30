using Dogity.Domain.Common;

namespace Dogity.Domain.Training;

/// <summary>
/// Eine komplette Trainingseinheit für einen Hund (siehe DATABASE.md
/// "Samstag Training Hundeplatz"). UserId verweist bewusst nur als Guid
/// auf die Identity-Tabelle in Dogity.Infrastructure, ohne dass das
/// Domain-Projekt eine Abhängigkeit zu ASP.NET Identity bekommt.
/// </summary>
public class TrainingSession : Entity
{
    public Guid UserId { get; set; }
    public Guid DogId { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>
    /// Startzeit des Trainings (lokal). Optional, weil Trainings auch
    /// nachgetragen werden - dann ist die Uhrzeit ggf. nicht mehr bekannt.
    /// Zusammen mit <see cref="Latitude"/>/<see cref="Longitude"/> die
    /// Grundlage für die automatische Wetter-Ermittlung.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>Trainingsort - per aktuellem Standort oder Ortssuche gesetzt.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationName { get; set; }

    // ---- Wetter zum Trainingszeitpunkt (siehe IWeatherProvider) ----
    public double? TemperatureC { get; set; }
    public int? RelativeHumidity { get; set; }
    public double? WindSpeedKmh { get; set; }
    /// <summary>WMO-Wettercode (siehe Open-Meteo weather_code).</summary>
    public int? WeatherCode { get; set; }
    public DateTimeOffset? WeatherFetchedAt { get; set; }

    public int DurationMinutes { get; set; }

    /// <summary>
    /// Verfassung des Hundes an diesem Trainingstag. Optional - ein Pflichtfeld
    /// mehr würde die Hürde beim Eintragen wieder anheben, die zuletzt mühsam
    /// gesenkt wurde.
    /// </summary>
    public DogCondition? Condition { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Rückmeldung eines betreuenden Trainers zu dieser Trainingseinheit
    /// (siehe DATABASE.md "Berechtigungen": Trainer kann "Feedback geben").
    /// Nur von einem über <see cref="Domain.Community.TrainerAssignment"/>
    /// zugeordneten Trainer setzbar, nicht vom Hundebesitzer selbst.
    /// </summary>
    public string? TrainerFeedback { get; set; }
    public Guid? FeedbackByTrainerId { get; set; }
    public DateTimeOffset? FeedbackAt { get; set; }

    /// <summary>
    /// Antwort des Hundebesitzers auf das Trainer-Feedback: ein Danke bzw.
    /// Verstanden und/oder eine kurze Rückfrage. Bewusst kein Gespräch - je
    /// Eintrag höchstens eine Reaktion und eine Rückfrage, ohne Verlauf.
    /// Sie beziehen sich auf den Text des Feedbacks und werden deshalb
    /// zurückgesetzt, sobald die Trainer:in neues Feedback gibt.
    /// </summary>
    public FeedbackReaction? OwnerReaction { get; set; }
    public string? OwnerReply { get; set; }
    public DateTimeOffset? OwnerReplyAt { get; set; }

    public ICollection<TrainingExercise> Exercises { get; set; } = new List<TrainingExercise>();
}

/// <summary>Schnelle Reaktion des Besitzers auf ein Trainer-Feedback (siehe <see cref="TrainingSession.OwnerReaction"/>).</summary>
public enum FeedbackReaction
{
    /// <summary>Danke - hat gefreut / geholfen.</summary>
    Thanks,

    /// <summary>Verstanden - zur Kenntnis genommen, wird umgesetzt.</summary>
    Understood
}
