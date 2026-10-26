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
    /// Nimmt eine Antwort entgegen und schreibt den Lernstand fort.
    ///
    /// Über richtig/falsch entscheidet der Server, wo er es kann: bei
    /// Auswahlfragen anhand der Antwortmöglichkeiten, bei Zuordnungen anhand
    /// der Schlüssel. Nur die offenen Freitextfragen werden selbst
    /// eingeschätzt - die kann niemand automatisch prüfen.
    /// </summary>
    Task<Result<QuizAnswerResultDto>> SubmitAnswerAsync(
        Guid userId, Guid questionId, IReadOnlyList<Guid>? selectedOptionIds, bool? selfAssessedCorrect,
        IReadOnlyDictionary<Guid, string>? assignments, CancellationToken ct = default);

    /// <summary>Lernstand über einen Katalog.</summary>
    Task<Result<QuizProgressDto>> GetProgressAsync(Guid userId, string catalogCode, CancellationToken ct = default);

    /// <summary>
    /// Von vorne anfangen: setzt den Lernstand des Nutzers für diesen Katalog
    /// auf Anfang zurück.
    /// </summary>
    Task<Result> ResetAsync(Guid userId, string catalogCode, CancellationToken ct = default);

    /// <summary>
    /// Zieht eine Übungsprüfung: Fragen anteilig nach Komplex, ohne Lösungen.
    /// Ohne Anmeldung nutzbar. Katalog ohne Übungsprüfung: nicht gefunden.
    /// </summary>
    Task<Result<QuizExamDto>> StartExamAsync(string catalogCode, CancellationToken ct = default);

    /// <summary>
    /// Wertet eine Übungsprüfung aus. Angemeldet (<paramref name="userId"/>
    /// gesetzt) wird das Ergebnis gespeichert, und falsch beantwortete Fragen
    /// kommen in den Fehlerspeicher; richtig beantwortete ändern den Lernstand
    /// nicht. Anonym wird nur gewertet.
    ///
    /// <paramref name="submissionId"/> ist die Abgabe-Kennung aus
    /// <see cref="StartExamAsync"/>. Kommt sie für denselben Nutzer ein zweites
    /// Mal, bleibt es bei dem einen gespeicherten Ergebnis und es wird dieses
    /// zurückgegeben; der Fehlerspeicher bleibt unberührt. Ohne Kennung (alte
    /// Clients) wird wie bisher angenommen.
    /// </summary>
    Task<Result<QuizExamResultDto>> SubmitExamAsync(
        Guid? userId, string catalogCode, IReadOnlyList<QuizExamAnswerDto> answers,
        Guid? submissionId = null, CancellationToken ct = default);

    /// <summary>Die letzten Ergebnisse des Nutzers in diesem Katalog.</summary>
    Task<Result<QuizExamHistoryDto>> GetExamHistoryAsync(Guid userId, string catalogCode, CancellationToken ct = default);
}
