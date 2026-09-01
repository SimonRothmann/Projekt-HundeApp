using Dogity.Domain.Common;

namespace Dogity.Domain.Learning;

/// <summary>Wozu eine Zeile in <see cref="QuizOption"/> gehört.</summary>
public enum QuizOptionKind
{
    /// <summary>Antwortmöglichkeit einer Auswahlfrage.</summary>
    Answer,

    /// <summary>
    /// Ein zuzuordnender Begriff ("Boxer", "Angst"). <see cref="QuizOption.MatchKey"/>
    /// trägt den richtigen Schlüssel.
    /// </summary>
    Term,

    /// <summary>
    /// Die Beschriftung eines Schlüssels ("E" = "kurzköpfig"). Fehlt, wenn die
    /// Schlüssel aus einer Abbildung kommen (A2: die Ziffern 1-5 im Bild).
    /// </summary>
    Label
}

/// <summary>
/// Eine Zeile unterhalb einer Frage: je nach <see cref="Kind"/> eine
/// Antwortmöglichkeit, ein zuzuordnender Begriff oder die Beschriftung eines
/// Zuordnungsschlüssels.
///
/// Bewusst eine Tabelle statt dreier: die Zeilen unterscheiden sich nur in der
/// Rolle, und eine Frage lädt sie ohnehin immer zusammen.
/// </summary>
public class QuizOption : Entity
{
    public Guid QuestionId { get; set; }
    public QuizQuestion? Question { get; set; }

    public QuizOptionKind Kind { get; set; } = QuizOptionKind.Answer;

    public string Text { get; set; } = string.Empty;

    /// <summary>Nur bei Auswahlfragen belegt.</summary>
    public bool IsCorrect { get; set; }

    /// <summary>
    /// Bei <see cref="QuizOptionKind.Term"/> der richtige Schlüssel, bei
    /// <see cref="QuizOptionKind.Label"/> der Schlüssel, den die Beschriftung
    /// benennt. Sonst leer.
    /// </summary>
    public string? MatchKey { get; set; }

    public int SortOrder { get; set; }
}
