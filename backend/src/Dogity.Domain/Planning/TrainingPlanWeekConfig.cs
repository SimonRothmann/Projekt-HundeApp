using Dogity.Domain.Common;

namespace Dogity.Domain.Planning;

/// <summary>
/// Pro-Woche-Überschreibung der Trainingstage eines Plans. Ohne Eintrag gilt
/// der Plan-Default <see cref="Goal.TrainingDaysPerWeek"/>; mit Eintrag zählt
/// für genau diese Woche der hier gesetzte Wert (z.B. Woche 1 zwei Tage,
/// Woche 2 drei Tage). Steuert, auf wie viele Trainingstage der adaptive
/// Generator die Übungen dieser Woche verteilt und wie viele Tage im Frontend
/// beim Hinzufügen einer Übung wählbar sind.
/// </summary>
public class TrainingPlanWeekConfig : Entity
{
    public Guid TrainingPlanId { get; set; }
    public TrainingPlan? TrainingPlan { get; set; }

    public int WeekNumber { get; set; }
    public int TrainingDaysPerWeek { get; set; }
}
