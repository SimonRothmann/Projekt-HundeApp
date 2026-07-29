using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Eine Position im Inhalt eines <see cref="GroupTrainingSession"/>: entweder
/// ein Verweis auf einen wiederverwendbaren <see cref="GroupTrainingExercise"/>
/// (Baustein) ODER ein Freitext (Ad-hoc-Inhalt). Genau eines von beiden ist
/// gesetzt. Reihenfolge über <see cref="SortOrder"/>.
/// </summary>
public class GroupTrainingSessionItem : Entity
{
    public Guid GroupTrainingSessionId { get; set; }
    public GroupTrainingSession? Session { get; set; }

    public Guid? GroupTrainingExerciseId { get; set; }
    public GroupTrainingExercise? Exercise { get; set; }

    public string? FreeText { get; set; }

    public int SortOrder { get; set; }
}
