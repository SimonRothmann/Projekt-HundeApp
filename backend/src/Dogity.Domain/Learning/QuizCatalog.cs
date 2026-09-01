using Dogity.Domain.Common;

namespace Dogity.Domain.Learning;

/// <summary>Für wen ein Fragenkatalog gedacht ist.</summary>
public enum QuizAudience
{
    /// <summary>Erwachsenenfassung.</summary>
    Adults,

    /// <summary>Fassung für Kinder und Jugendliche unter 15 Jahren.</summary>
    Youth
}

/// <summary>
/// Ein Fragenkatalog zum Lernen, z.B. die Sachkundeprüfung zur BH/VT.
///
/// Der Katalog trägt seinen Herausgeber, die Quelle und den Stand mit sich.
/// Das ist keine Zierde: die Verbände aktualisieren ihre Kataloge, und beim
/// Nachziehen muss nachvollziehbar sein, welche Fassung eine Instanz gerade
/// führt (siehe scripts/import-sachkunde.py).
/// </summary>
public class QuizCatalog : Entity
{
    /// <summary>Stabiler Schlüssel, über den der Seeder abgleicht (z.B. "SWHV-BHVT-ERW").</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Herausgeber des Katalogs - wird in der Oberfläche genannt.</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>Fundstelle des Originals.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Fassung/Stand der übernommenen Ausgabe (z.B. "2024-03").</summary>
    public string? Edition { get; set; }

    public QuizAudience Audience { get; set; }

    public int SortOrder { get; set; }

    public ICollection<QuizQuestion> Questions { get; set; } = new List<QuizQuestion>();
}
