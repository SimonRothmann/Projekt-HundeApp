using Dogity.Domain.Common;
using Dogity.Domain.Sports;

namespace Dogity.Domain.Planning;

/// <summary>
/// Ein Wochenziel innerhalb eines Trainingsplans. Bei einer Pausenwoche
/// (<see cref="IsRestWeek"/>) ist <see cref="ExerciseId"/> null.
/// </summary>
public class TrainingPlanItem : Entity
{
    public Guid TrainingPlanId { get; set; }
    public TrainingPlan? TrainingPlan { get; set; }

    public int WeekNumber { get; set; }

    public Guid? ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    /// <summary>
    /// Alternative zu <see cref="ExerciseId"/>: freier Text für spontane
    /// Wochenziele, die nicht als eigene Katalog-Übung angelegt werden
    /// sollen (z.B. "Kopfarbeit ausprobieren"). Genau eines von
    /// ExerciseId/FreeTextLabel muss gesetzt sein, außer bei Pausenwochen.
    /// Analog zu <see cref="Training.TrainingExercise.FreeTextLabel"/>.
    /// </summary>
    public string? FreeTextLabel { get; set; }

    public int RepetitionsTarget { get; set; }
    public bool IsRestWeek { get; set; }
}
