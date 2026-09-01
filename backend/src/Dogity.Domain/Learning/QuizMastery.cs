using Dogity.Domain.Common;

namespace Dogity.Domain.Learning;

/// <summary>
/// Lernstand einer Frage - je NUTZER, nicht je Hund.
///
/// Das ist der Unterschied zu <c>ExerciseMastery</c>: eine Übung wird mit
/// einem bestimmten Hund trainiert, die Sachkunde ist der Nachweis des
/// Hundeführers und gilt für jeden weiteren Hund mit. Die Leitner-Mechanik
/// dahinter ist dieselbe, die Bezugsgröße nicht.
/// </summary>
public class QuizMastery : Entity
{
    public Guid UserId { get; set; }

    public Guid QuestionId { get; set; }
    public QuizQuestion? Question { get; set; }

    /// <summary>Leitner-Fach 1..5; bestimmt, wann die Frage wiederkommt.</summary>
    public int Box { get; set; } = 1;

    public DateTimeOffset? LastAnsweredAt { get; set; }

    /// <summary>Ab wann die Frage wieder abgefragt wird.</summary>
    public DateTimeOffset? DueAt { get; set; }

    public int CorrectCount { get; set; }

    public int WrongCount { get; set; }

    /// <summary>
    /// Ob die letzte Antwort richtig war. Trägt den Fehlerspeicher: eine Frage
    /// bleibt darin, bis sie wieder gesessen hat.
    /// </summary>
    public bool LastWasCorrect { get; set; }
}
