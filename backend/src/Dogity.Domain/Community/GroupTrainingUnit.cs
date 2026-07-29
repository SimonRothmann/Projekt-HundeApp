using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Eine zusammengestellte Trainingseinheit der Vereins-Trainingsbibliothek:
/// eine geordnete Mischung aus <see cref="GroupTrainingExercise"/>-Bausteinen
/// (siehe docs/GROUP_TRAINING_LIBRARY.md). Verein-weit geteilt; jede:r
/// Vereinstrainer:in kann sie nutzen, bearbeiten oder als Kopie anpassen.
/// Eine Einheit ist eine wiederverwendbare Vorlage - nicht an eine konkrete
/// Gruppe oder ein Datum gebunden.
/// </summary>
public class GroupTrainingUnit : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public GroupTrainingCategory Category { get; set; } = GroupTrainingCategory.Puppy;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ICollection<GroupTrainingUnitItem> Items { get; set; } = new List<GroupTrainingUnitItem>();
}
