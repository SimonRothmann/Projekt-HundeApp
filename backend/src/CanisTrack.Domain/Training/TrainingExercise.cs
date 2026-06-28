using CanisTrack.Domain.Common;
using CanisTrack.Domain.Sports;

namespace CanisTrack.Domain.Training;

/// <summary>
/// Eine einzelne durchgeführte Übung innerhalb einer Trainingseinheit,
/// inkl. Bewertung (1-5 Sterne, siehe DATABASE.md "Trainingsbewertung").
/// </summary>
public class TrainingExercise : Entity
{
    public Guid TrainingSessionId { get; set; }
    public TrainingSession? TrainingSession { get; set; }

    public Guid ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    /// <summary>1-5 Sterne.</summary>
    public int Rating { get; set; }
    public ExerciseDifficulty Difficulty { get; set; }
    public bool Success { get; set; }
    public string? Notes { get; set; }
}
