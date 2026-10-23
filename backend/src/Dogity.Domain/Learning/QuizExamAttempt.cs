using Dogity.Domain.Common;

namespace Dogity.Domain.Learning;

/// <summary>
/// Das Ergebnis einer abgegebenen Übungsprüfung eines angemeldeten Nutzers.
///
/// Gespeichert wird nur das Ergebnis, nicht jede einzelne Antwort: der Verlauf
/// ("wie ist es zuletzt gelaufen?") und später die Prüfungsreife brauchen
/// Punkte und Komplexe, nicht den Bogen. Was falsch war, steht danach im
/// Fehlerspeicher (<see cref="QuizMastery"/>) und wird dort gelernt.
///
/// Je NUTZER, wie der Lernstand: die Sachkunde ist der Nachweis des
/// Hundeführers.
/// </summary>
public class QuizExamAttempt : Entity
{
    public Guid UserId { get; set; }

    public Guid CatalogId { get; set; }
    public QuizCatalog? Catalog { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public int Points { get; set; }

    /// <summary>Was bei dieser Zusammenstellung höchstens zu erreichen war.</summary>
    public int MaxPoints { get; set; }

    /// <summary>
    /// Bestanden nach der Übungsregel (ab 70 % der möglichen Punkte). Wird
    /// mitgespeichert, damit ein Verlauf auch dann stimmt, wenn die Grenze
    /// einmal angepasst wird.
    /// </summary>
    public bool Passed { get; set; }

    public int QuestionCount { get; set; }

    /// <summary>Fragen, die mit voller Punktzahl beantwortet wurden.</summary>
    public int CorrectCount { get; set; }

    /// <summary>
    /// Punkte je Komplex als JSON-Liste (Schlüssel, Name, erreicht, möglich).
    ///
    /// Bewusst eine Spalte statt einer Kindtabelle: die Zahlen werden nur
    /// zusammen mit dem Ergebnis gelesen, nie einzeln abgefragt - eine Tabelle
    /// bräuchte dazu eigenen Soft Delete, Filter, Export und Löschlauf für
    /// fünf Zahlenpaare.
    /// </summary>
    public string SectionResultsJson { get; set; } = "[]";
}
