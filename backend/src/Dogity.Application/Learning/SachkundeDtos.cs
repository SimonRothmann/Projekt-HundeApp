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
    IReadOnlyList<QuizSectionDto> Sections,
    QuizExamInfoDto? Exam = null);

/// <summary>
/// Was die Übungsprüfung eines Katalogs ausmacht - für die Kurzinfo vor dem
/// Start ("25 Fragen · bestanden ab 35 von 50 Punkten"). Null, wenn der
/// Katalog keine hat. <see cref="MaxPoints"/> setzt voraus, dass jede Frage
/// genau eine richtige Antwort hat (so ist der Katalog); die Prüfung selbst
/// trägt die tatsächlichen Höchstpunkte ihrer Zusammenstellung.
/// </summary>
public record QuizExamInfoDto(int QuestionCount, int MaxPoints, int PassPoints, int PassPercent);

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


// ---------- Übungsprüfung ----------

/// <summary>
/// Eine gezogene Übungsprüfung - OHNE Lösungen: keine Kennzeichnung der
/// richtigen Antworten, keine Musterlösung, bei Zuordnungen nur die wählbaren
/// Schlüssel. Wer mehr als einen Blick braucht, sieht sie sich auf der
/// Katalogseite an; die Prüfung selbst soll nichts verraten.
/// </summary>
public record QuizExamDto(
    string CatalogCode,
    int QuestionCount,
    int MaxPoints,
    int PassPoints,
    int PassPercent,
    IReadOnlyList<QuizExamQuestionDto> Questions);

public record QuizExamQuestionDto(
    Guid Id,
    string Number,
    string Section,
    string SectionName,
    string Kind,
    string Text,
    string? ImageName,
    IReadOnlyList<QuizExamOptionDto> Options,
    IReadOnlyList<QuizExamTermDto> Terms,
    IReadOnlyList<QuizKeyDto> Keys);

public record QuizExamOptionDto(Guid Id, string Text, string? ImageName);

public record QuizExamTermDto(Guid Id, string Text);

/// <summary>Die Antwort auf eine Frage der Übungsprüfung; leer = unbeantwortet.</summary>
/// <param name="SelectedOptionIds">Angekreuzte Antworten bei Auswahlfragen.</param>
/// <param name="Assignments">Bei Zuordnungen: Begriffs-Id → gewählter Schlüssel.</param>
public record QuizExamAnswerDto(
    Guid QuestionId,
    IReadOnlyList<Guid>? SelectedOptionIds,
    IReadOnlyDictionary<Guid, string>? Assignments);

/// <summary>Auswertung einer abgegebenen Übungsprüfung.</summary>
/// <param name="Percent">Ganzer Prozentwert, abgerundet.</param>
/// <param name="Saved">Ob das Ergebnis gespeichert wurde (nur angemeldet).</param>
public record QuizExamResultDto(
    int Points,
    int MaxPoints,
    int PassPoints,
    int PassPercent,
    int Percent,
    bool Passed,
    int QuestionCount,
    int CorrectCount,
    bool Saved,
    IReadOnlyList<QuizExamSectionResultDto> Sections,
    IReadOnlyList<QuizExamQuestionResultDto> Questions);

public record QuizExamSectionResultDto(string Key, string Name, int Points, int MaxPoints);

/// <summary>
/// Das Ergebnis einer Frage samt richtiger Lösung (jetzt, nach der Abgabe, darf
/// sie verraten werden). Fragetext und Antworten kennt die Oberfläche aus der
/// Prüfung selbst.
/// </summary>
/// <param name="CorrectAssignments">Bei Zuordnungen: Begriffs-Id → richtiger Schlüssel.</param>
public record QuizExamQuestionResultDto(
    Guid QuestionId,
    bool Correct,
    int Points,
    int MaxPoints,
    IReadOnlyList<Guid> CorrectOptionIds,
    IReadOnlyDictionary<Guid, string> CorrectAssignments);

/// <summary>Ein gespeichertes Ergebnis im Verlauf.</summary>
public record QuizExamAttemptDto(
    Guid Id,
    DateTimeOffset SubmittedAt,
    int Points,
    int MaxPoints,
    int Percent,
    bool Passed,
    int QuestionCount,
    int CorrectCount,
    IReadOnlyList<QuizExamSectionResultDto> Sections);

/// <summary>
/// Die letzten Ergebnisse eines Nutzers in einem Katalog, neueste zuerst.
/// <see cref="PassedOfLastFive"/> von <see cref="LastFiveCount"/> ist die
/// Kurzbilanz, auf die später die Prüfungsreife aufsetzen kann.
/// </summary>
public record QuizExamHistoryDto(
    string CatalogCode,
    IReadOnlyList<QuizExamAttemptDto> Attempts,
    int PassedOfLastFive,
    int LastFiveCount);
