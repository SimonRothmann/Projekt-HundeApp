using Dogity.Domain.Common;

namespace Dogity.Domain.Sports;

/// <summary>
/// Verknüpft eine Übung mit einer Prüfungsordnungsversion, inkl.
/// Pflicht-Kennzeichen und maximaler Punktzahl (siehe DATABASE.md
/// Beispiel "IBGH3 | Fußarbeit | Pflicht | Bewertung 15 Punkte").
/// </summary>
public class RegulationExercise : Entity
{
    public Guid RegulationVersionId { get; set; }
    public RegulationVersion? RegulationVersion { get; set; }

    public Guid ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    public bool IsMandatory { get; set; } = true;
    public int MaxPoints { get; set; }

    /// <summary>
    /// Prüfungsspezifische Anforderungen/Bewertungshinweise, z.B. bei
    /// Fährtenübungen die geforderte Fährtenlänge, das Fährtenalter und
    /// die Anzahl Winkel/Gegenstände dieser konkreten Prüfungsstufe.
    /// </summary>
    public string? ScoringNotes { get; set; }
}
