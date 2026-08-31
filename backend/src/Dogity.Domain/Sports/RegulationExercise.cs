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
    /// Position der Übung innerhalb der Prüfungsordnung - kleinere Werte
    /// zuerst. Ohne das lieferte die Datenbank die Übungen in beliebiger
    /// Reihenfolge aus, "Sitz mit Abholen" stand dann vor der
    /// "Leinenführigkeit". Gefüllt wird der Wert aus der Reihenfolge im
    /// SportCatalogSeeder (= Reihenfolge der Prüfungsordnung); von Hand
    /// ergänzte Übungen hängen sich hinten an. Bei gleichem Wert entscheidet
    /// der Übungsname (siehe SportCatalogService.GetRegulationDetailAsync).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Prüfungsspezifische Anforderungen/Bewertungshinweise, z.B. bei
    /// Fährtenübungen die geforderte Fährtenlänge, das Fährtenalter und
    /// die Anzahl Winkel/Gegenstände dieser konkreten Prüfungsstufe.
    /// </summary>
    public string? ScoringNotes { get; set; }
}
