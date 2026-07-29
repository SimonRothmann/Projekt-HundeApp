using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Altersstufe/Ausbildungsstand, nach der die Bausteine und Einheiten der
/// Vereins-Trainingsbibliothek gegliedert sind. Progression: Welpen →
/// Junghunde → Basis (Grundausbildung, führt Richtung BH/IBGH).
/// </summary>
public enum GroupTrainingCategory
{
    Puppy,     // Welpen
    YoungDog,  // Junghunde
    Basis      // Basis-Training (Grundausbildung Richtung BH/IBGH)
}

/// <summary>
/// Optionaler Prüfungs-Bezug eines Bausteins ("bereitet auf diese Prüfung(en)
/// vor"). Bewusst reine Labels (Flags), keine harte Kopplung an den Sport-/
/// Prüfungskatalog - gemischte Gruppen (Hunde mit unterschiedlichen Zielen)
/// werden so abgebildet, ohne Community und Sports zu verzahnen.
/// </summary>
[Flags]
public enum GroupExamTarget
{
    None = 0,
    BH = 1,
    IBGH1 = 2,
    IBGH2 = 4,
    IBGH3 = 8
}

/// <summary>
/// Ein wiederverwendbarer Übungs-Baustein der Vereins-Trainingsbibliothek.
/// Verein-weit sichtbar und von jeder/jedem Vereinstrainer:in pflegbar
/// (siehe docs/GROUP_TRAINING_LIBRARY.md). Trainer stellen aus Bausteinen
/// geordnete <see cref="GroupTrainingUnit"/> (Einheiten) zusammen.
/// </summary>
public class GroupTrainingExercise : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public GroupTrainingCategory Category { get; set; } = GroupTrainingCategory.Puppy;

    public string Title { get; set; } = string.Empty;
    public string? Focus { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Description { get; set; }

    /// <summary>Auf welche Prüfung(en) der Baustein vorbereitet (optional, meist bei Basis).</summary>
    public GroupExamTarget ExamTargets { get; set; } = GroupExamTarget.None;

    /// <summary>Nur zur Info/Anzeige - bearbeiten darf jede:r Vereinstrainer:in.</summary>
    public Guid? CreatedByUserId { get; set; }
}
