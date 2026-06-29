using Dogity.Domain.Common;

namespace Dogity.Domain.Planning;

/// <summary>
/// Automatisch generierter Trainingsplan zu einem <see cref="Goal"/>
/// (siehe DATABASE.md "training_plans", Beispiel "KW 12: 3x Fußarbeit,
/// 2x Ablage, 1x Spaßtraining, 1x Pause"). Die Generierung ist bewusst
/// regelbasiert und deterministisch - echte KI-Trainingsanalyse ist laut
/// PRODUCT_REQUIREMENTS.md "Nicht-MVP" und folgt erst später.
/// </summary>
public class TrainingPlan : Entity
{
    public Guid GoalId { get; set; }
    public Goal? Goal { get; set; }

    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TrainingPlanItem> Items { get; set; } = new List<TrainingPlanItem>();
}
