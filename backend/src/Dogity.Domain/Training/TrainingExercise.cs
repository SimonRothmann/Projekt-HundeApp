using Dogity.Domain.Common;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Domain.Training;

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

    /// <summary>
    /// Optionaler Bezug zu einem Wochenziel im Trainingsplan (siehe
    /// TrainingPlanItem) - verknüpft einen echten Tagebucheintrag mit dem
    /// Plan-Ziel, das er erfüllt, statt eine zweite, separate "erledigt"-
    /// Verwaltung im Plan selbst zu führen. Fortschritt/Bewertung/Kommentar
    /// eines Plan-Ziels ergeben sich dadurch direkt aus den verknüpften
    /// Tagebucheinträgen (Rating/Success/Notes oben).
    /// </summary>
    public Guid? TrainingPlanItemId { get; set; }
    public TrainingPlanItem? TrainingPlanItem { get; set; }
}
