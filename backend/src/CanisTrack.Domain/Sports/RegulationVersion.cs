using CanisTrack.Domain.Common;

namespace CanisTrack.Domain.Sports;

/// <summary>
/// Eine zeitlich gültige Version einer Prüfungsordnung
/// (z.B. "BH Version 2025, gültig ab 01.01.2025").
/// </summary>
public class RegulationVersion : Entity
{
    public Guid RegulationId { get; set; }
    public Regulation? Regulation { get; set; }

    public string VersionLabel { get; set; } = string.Empty;
    public DateOnly ValidFrom { get; set; }

    public ICollection<RegulationExercise> RegulationExercises { get; set; } = new List<RegulationExercise>();
}
