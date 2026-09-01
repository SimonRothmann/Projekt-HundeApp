using Dogity.Domain.Common;

namespace Dogity.Domain.Learning;

/// <summary>Wie eine Frage beantwortet wird.</summary>
public enum QuizQuestionKind
{
    /// <summary>Genau eine Antwort ist richtig.</summary>
    SingleChoice,

    /// <summary>Mehrere Antworten können richtig sein.</summary>
    MultipleChoice,

    /// <summary>
    /// Zuordnungsaufgabe (Begriffe zu Merkmalen). Wird als Karte gelernt:
    /// nachdenken, Lösung aufdecken, selbst einschätzen.
    /// </summary>
    Assignment,

    /// <summary>Offene Frage mit Musterlösung - ebenfalls selbst eingeschätzt.</summary>
    FreeText
}

/// <summary>
/// Eine Frage aus einem <see cref="QuizCatalog"/>.
///
/// <see cref="Number"/> ist der Schlüssel innerhalb des Katalogs ("A1", "12")
/// und bleibt über Neuimporte stabil - daran hängt der Lernstand der Nutzer.
/// Ändert der Herausgeber den Fragentext, wird die vorhandene Zeile
/// aktualisiert und nicht ersetzt, sonst wäre der Lernstand weg.
/// </summary>
public class QuizQuestion : Entity
{
    public Guid CatalogId { get; set; }
    public QuizCatalog? Catalog { get; set; }

    /// <summary>Themenkomplex innerhalb des Katalogs ("A".."E", "J").</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// Klartextname des Komplexes ("Recht", "Prüfungswesen"). Bewusst an der
    /// Frage statt in einer eigenen Tabelle: ein Wort je Zeile ist billiger
    /// als eine Tabelle, die nur zwei Spalten hätte.
    /// </summary>
    public string SectionName { get; set; } = string.Empty;

    /// <summary>Fragennummer laut Katalog - im Katalog eindeutig.</summary>
    public string Number { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public string Text { get; set; } = string.Empty;

    public QuizQuestionKind Kind { get; set; } = QuizQuestionKind.SingleChoice;

    /// <summary>
    /// Musterlösung für <see cref="QuizQuestionKind.Assignment"/> und
    /// <see cref="QuizQuestionKind.FreeText"/>; sonst leer.
    /// </summary>
    public string? SampleSolution { get; set; }

    /// <summary>
    /// Dateiname eines Bildes unter /sachkunde/ im Frontend (nur wo die Frage
    /// ohne Abbildung nicht beantwortbar ist).
    /// </summary>
    public string? ImageName { get; set; }

    public ICollection<QuizOption> Options { get; set; } = new List<QuizOption>();
}
