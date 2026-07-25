using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Eine einzelne Übung/Aktivität innerhalb einer <see cref="GroupTrainingUnit"/>.
/// Freitext, damit Trainer beliebige Gruppeninhalte festlegen können.
/// </summary>
public class GroupTrainingUnitItem : Entity
{
    public Guid GroupTrainingUnitId { get; set; }
    public GroupTrainingUnit? Unit { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Themenschwerpunkt (z.B. "Leinenführigkeit", "Sozialisierung",
    /// "Impulskontrolle"). Freies Textfeld, weil die Themen je nach Gruppe
    /// und Alter stark variieren; im Frontend als Badge dargestellt.
    /// </summary>
    public string? Focus { get; set; }

    public int? DurationMinutes { get; set; }

    public int SortOrder { get; set; }
}
