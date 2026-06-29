using Dogity.Domain.Common;

namespace Dogity.Domain.Planning;

public enum GoalStatus
{
    Active,
    Achieved,
    Cancelled
}

/// <summary>
/// Ein Trainingsziel für einen Hund, z.B. "BH Prüfung am 01.05.2027"
/// (siehe DATABASE.md "Zielsystem"). SportId verweist auf die Zielsportart;
/// eine konkrete Prüfungsanmeldung (Competition-Modul) ist noch nicht
/// Teil des MVP-Funktionsumfangs.
/// </summary>
public class Goal : Entity
{
    public Guid DogId { get; set; }
    public Guid SportId { get; set; }

    public DateOnly TargetDate { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;
    public string? Notes { get; set; }

    public TrainingPlan? TrainingPlan { get; set; }
}
