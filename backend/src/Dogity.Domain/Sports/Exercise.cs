using Dogity.Domain.Common;

namespace Dogity.Domain.Sports;

public enum ExerciseDifficulty
{
    Beginner,
    Intermediate,
    Advanced
}

/// <summary>
/// Eine Übung innerhalb einer Sportart (z.B. "Fußarbeit", "Abrufen").
/// Übungen sind sportartübergreifend wiederverwendbar und werden über
/// <see cref="RegulationExercise"/> einer konkreten Prüfungsordnung zugeordnet.
/// </summary>
public class Exercise : Entity
{
    /// <summary>
    /// Optional: eine Übung kann einer Sportart zugeordnet sein (Standardfall,
    /// z.B. "Fußarbeit" gehört zu IBGH) oder sportartlos existieren (z.B.
    /// vereinseigene Grundlagen-Übungen wie "Aufmerksamkeit halten", die
    /// in mehreren Trainings-Kontexten genutzt werden). Sportartlose Übungen
    /// erscheinen im Trainingstagebuch unter einer eigenen Kategorie
    /// "Ohne Sportart" statt in einer Sport-Sparte.
    /// </summary>
    public Guid? SportId { get; set; }
    public Sport? Sport { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ExerciseDifficulty Difficulty { get; set; } = ExerciseDifficulty.Beginner;
    public string? Category { get; set; }

    /// <summary>
    /// Allgemeine, sportartübergreifende Bewertungskriterien dieser Übung
    /// (z.B. "Tempo, Leinenführigkeit, Position zum Hundeführer"). Werden
    /// beim Üben angezeigt. Sparten-/prüfungsspezifische Abweichungen
    /// stehen in <see cref="RegulationExercise.ScoringNotes"/>.
    /// </summary>
    public string? ScoringCriteria { get; set; }

    /// <summary>
    /// Null = globale Übung, von einem Admin gepflegt und für alle
    /// sichtbar. Gesetzt = vereinsspezifische Übung, von einem für diesen
    /// Verein zugewiesenen Trainer angelegt (siehe ClubTrainer) und nur für
    /// dessen Vereinsmitglieder/-trainer sichtbar.
    /// </summary>
    public Guid? ClubId { get; set; }

    public ICollection<RegulationExercise> RegulationExercises { get; set; } = new List<RegulationExercise>();
}
