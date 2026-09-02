namespace Dogity.Application.Learning;

/// <summary>Ein Fragenkatalog in der Übersicht.</summary>
public record QuizCatalogDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Publisher,
    string? SourceUrl,
    string? Edition,
    string Audience,
    int QuestionCount,
    IReadOnlyList<QuizSectionDto> Sections);

/// <summary>Ein Themenkomplex innerhalb eines Katalogs.</summary>
public record QuizSectionDto(string Key, string Name, int QuestionCount);

/// <summary>
/// Eine Frage samt Antwortmöglichkeiten. <see cref="Options"/> enthält auch,
/// welche Antwort richtig ist - der Katalog ist veröffentlichtes Lernmaterial,
/// keine laufende Prüfung.
/// </summary>
public record QuizQuestionDto(
    Guid Id,
    string Number,
    string Section,
    string SectionName,
    string Kind,
    string Text,
    string? ImageName,
    string? SampleSolution,
    IReadOnlyList<QuizOptionDto> Options,
    IReadOnlyList<QuizTermDto> Terms,
    IReadOnlyList<QuizKeyDto> Keys,
    QuizQuestionStateDto? State);

public record QuizOptionDto(Guid Id, string Text, bool IsCorrect, string? ImageName);

/// <summary>Ein zuzuordnender Begriff einer Zuordnungsaufgabe.</summary>
public record QuizTermDto(Guid Id, string Text, string SolutionKey);

/// <summary>
/// Ein wählbarer Schlüssel einer Zuordnungsaufgabe. <see cref="Label"/> ist
/// leer, wenn die Schlüssel aus einer Abbildung stammen (die Ziffern im Bild).
/// </summary>
public record QuizKeyDto(string Key, string? Label);

/// <summary>Der Lernstand des Nutzers zu einer Frage; null für anonyme Aufrufer.</summary>
public record QuizQuestionStateDto(int Box, bool LastWasCorrect, int CorrectCount, int WrongCount, DateTimeOffset? DueAt);

/// <summary>Antwort des Servers auf eine beantwortete Frage.</summary>
/// <param name="TermResults">Bei Zuordnungen: welche Begriffe richtig zugeordnet waren.</param>
/// <param name="Progress">
/// Der Lernstand NACH dieser Antwort. Ohne ihn müsste die Oberfläche den Stand
/// nachladen - sie tat es nicht, und der Balken stand die ganze Runde still.
/// </param>
public record QuizAnswerResultDto(
    bool Correct,
    int Box,
    DateTimeOffset? DueAt,
    IReadOnlyList<Guid> CorrectOptionIds,
    IReadOnlyDictionary<Guid, bool> TermResults,
    QuizProgressDto? Progress);

/// <summary>
/// Lernstand über einen ganzen Katalog.
///
/// Zwei Zahlen, weil sie zwei Fragen beantworten:
/// <see cref="Correct"/> ist "wie viele sitzen gerade" - sie bewegt sich mit
/// jeder Antwort und ist die Zahl, die man beim Lernen sehen will.
/// <see cref="Mastered"/> ist "wie viele sitzen sicher" (Fach 4 aufwärts, also
/// mehrfach richtig an verschiedenen Tagen) - die bewegt sich erst über Tage.
///
/// Anfangs stand nur <see cref="Mastered"/> in der Oberfläche. Wer zwanzig
/// Fragen richtig beantwortet hatte, las weiter "0 von 72" - richtig gerechnet,
/// aber als Rückmeldung unbrauchbar.
/// </summary>
public record QuizProgressDto(
    string CatalogCode,
    int Total,
    int Answered,
    int Correct,
    int Mastered,
    int InMistakes,
    int DueNow,
    int NeverSeen,
    double PercentCorrect,
    double PercentMastered,
    IReadOnlyList<QuizSectionProgressDto> Sections);

public record QuizSectionProgressDto(
    string Key, string Name, int Total, int Answered, int Correct, int Mastered, int InMistakes);

/// <summary>Was der Lernmodus als Nächstes vorlegt.</summary>
public record QuizSessionDto(
    string CatalogCode,
    string Mode,
    IReadOnlyList<QuizQuestionDto> Questions,
    QuizProgressDto Progress,
    bool RoundComplete);
