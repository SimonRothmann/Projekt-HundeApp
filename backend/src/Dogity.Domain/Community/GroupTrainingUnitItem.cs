using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Ein Baustein an einer bestimmten Position innerhalb einer
/// <see cref="GroupTrainingUnit"/>. Referenziert einen wiederverwendbaren
/// <see cref="GroupTrainingExercise"/> (kein Freitext) - die Inhalte kommen
/// aus der Bibliothek, hier zählen nur Zugehörigkeit und Reihenfolge.
/// </summary>
public class GroupTrainingUnitItem : Entity
{
    public Guid GroupTrainingUnitId { get; set; }
    public GroupTrainingUnit? Unit { get; set; }

    public Guid GroupTrainingExerciseId { get; set; }
    public GroupTrainingExercise? Exercise { get; set; }

    public int SortOrder { get; set; }
}
