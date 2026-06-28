using CanisTrack.Domain.Common;

namespace CanisTrack.Domain.Training;

/// <summary>
/// Eine komplette Trainingseinheit für einen Hund (siehe DATABASE.md
/// "Samstag Training Hundeplatz"). UserId verweist bewusst nur als Guid
/// auf die Identity-Tabelle in CanisTrack.Infrastructure, ohne dass das
/// Domain-Projekt eine Abhängigkeit zu ASP.NET Identity bekommt.
/// </summary>
public class TrainingSession : Entity
{
    public Guid UserId { get; set; }
    public Guid DogId { get; set; }

    public DateOnly Date { get; set; }
    public int DurationMinutes { get; set; }
    public string? Notes { get; set; }

    public ICollection<TrainingExercise> Exercises { get; set; } = new List<TrainingExercise>();
}
