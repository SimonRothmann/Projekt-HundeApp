using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Altersklasse/Zielgruppe einer Trainingseinheit. Bestimmt, welche Themen
/// im Vordergrund stehen (Welpen: Sozialisierung/Umweltgewöhnung, Junghunde:
/// Leinenführigkeit/Impulskontrolle usw.).
/// </summary>
public enum GroupTrainingCategory
{
    Puppy,      // Welpen
    YoungDog,   // Junghunde
    General     // Allgemein / gemischte Gruppen
}

/// <summary>
/// Eine komplette Trainingseinheit für eine Gruppe (eine "Stunde" mit mehreren
/// Übungen/Aktivitäten). Zwei Ausprägungen:
///  - <see cref="CreatedByUserId"/> == null: vorgefertigte System-Vorlage
///    (z.B. Welpen/Junghunde), für alle Trainer sichtbar und als Startpunkt
///    kopierbar. <see cref="GroupId"/> ist dann ebenfalls null.
///  - <see cref="CreatedByUserId"/> gesetzt: vom Trainer selbst
///    zusammengestellt, optional an eine konkrete <see cref="Group"/> gebunden.
/// Die Übungen sind bewusst Freitext (nicht der Sport-Übungskatalog), weil
/// Gruppen-/Welpentraining eigene, nicht prüfungsgebundene Inhalte hat.
/// </summary>
public class GroupTrainingUnit : Entity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GroupTrainingCategory Category { get; set; } = GroupTrainingCategory.General;

    public Guid? CreatedByUserId { get; set; }

    public Guid? GroupId { get; set; }
    public Group? Group { get; set; }

    /// <summary>Reihenfolge innerhalb der Kategorie/Bibliothek.</summary>
    public int SortOrder { get; set; }

    public ICollection<GroupTrainingUnitItem> Items { get; set; } = new List<GroupTrainingUnitItem>();
}
