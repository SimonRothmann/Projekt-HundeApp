using Dogity.Application.Common;

namespace Dogity.Application.Learning;

/// <summary>
/// Fragentrainer für die Sachkundeprüfung - Lernen mit Wiedervorlage,
/// Fehlerspeicher und Neustart.
/// </summary>
public interface ISachkundeService
{
    /// <summary>Alle Kataloge mit Fragenzahl je Komplex. Ohne Anmeldung lesbar.</summary>
    Task<Result<IReadOnlyList<QuizCatalogDto>>> GetCatalogsAsync(CancellationToken ct = default);

    /// <summary>
    /// Alle Fragen eines Katalogs zum Durchblättern, wahlweise auf einen
    /// Komplex eingegrenzt. Ohne Anmeldung lesbar; mit Anmeldung kommt der
    /// eigene Lernstand je Frage mit.
    /// </summary>
    Task<Result<IReadOnlyList<QuizQuestionDto>>> GetQuestionsAsync(
        string catalogCode, string? section, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Die nächsten Fragen für den Lernmodus.
    /// </summary>
    /// <param name="mode">
    /// <c>learn</c> - fällige zuerst, dann noch nie gesehene;
    /// <c>mistakes</c> - nur der Fehlerspeicher;
    /// <c>all</c> - der ganze Katalog der Reihe nach, unabhängig von der Wiedervorlage.
    /// </param>
    Task<Result<QuizSessionDto>> GetSessionAsync(
        Guid userId, string catalogCode, string mode, int limit, CancellationToken ct = default);

    /// <summary>
    /// Nimmt eine Antwort entgegen und schreibt den Lernstand fort. Bei
    /// Auswahlfragen entscheidet der Server über richtig/falsch; bei
    /// Zuordnungs- und Freitextfragen zählt die Selbsteinschätzung.
    /// </summary>
    Task<Result<QuizAnswerResultDto>> SubmitAnswerAsync(
        Guid userId, Guid questionId, IReadOnlyList<Guid>? selectedOptionIds, bool? selfAssessedCorrect,
        CancellationToken ct = default);

    /// <summary>Lernstand über einen Katalog.</summary>
    Task<Result<QuizProgressDto>> GetProgressAsync(Guid userId, string catalogCode, CancellationToken ct = default);

    /// <summary>
    /// Von vorne anfangen: setzt den Lernstand des Nutzers für diesen Katalog
    /// auf Anfang zurück.
    /// </summary>
    Task<Result> ResetAsync(Guid userId, string catalogCode, CancellationToken ct = default);
}
