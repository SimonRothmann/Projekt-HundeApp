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
    public int DurationMinutes { get; set; }
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

    public ICollection<TrainingExercise> Exercises { get; set; } = new List<TrainingExercise>();
}
