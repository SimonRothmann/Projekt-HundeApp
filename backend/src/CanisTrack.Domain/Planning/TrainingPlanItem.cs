using CanisTrack.Domain.Common;
using CanisTrack.Domain.Sports;

namespace CanisTrack.Domain.Planning;

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

    public int RepetitionsTarget { get; set; }
    public bool IsRestWeek { get; set; }
}
