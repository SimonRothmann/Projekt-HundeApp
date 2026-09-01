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
    QuizQuestionStateDto? State);

public record QuizOptionDto(Guid Id, string Text, bool IsCorrect);

/// <summary>Der Lernstand des Nutzers zu einer Frage; null für anonyme Aufrufer.</summary>
public record QuizQuestionStateDto(int Box, bool LastWasCorrect, int CorrectCount, int WrongCount, DateTimeOffset? DueAt);

/// <summary>Antwort des Servers auf eine beantwortete Frage.</summary>
public record QuizAnswerResultDto(
    bool Correct,
    int Box,
    DateTimeOffset? DueAt,
    IReadOnlyList<Guid> CorrectOptionIds);

/// <summary>Lernstand über einen ganzen Katalog.</summary>
public record QuizProgressDto(
    string CatalogCode,
    int Total,
    int Answered,
    int Mastered,
    int InMistakes,
    int DueNow,
    int NeverSeen,
    double PercentMastered,
    IReadOnlyList<QuizSectionProgressDto> Sections);

public record QuizSectionProgressDto(string Key, string Name, int Total, int Answered, int Mastered, int InMistakes);

/// <summary>Was der Lernmodus als Nächstes vorlegt.</summary>
public record QuizSessionDto(
    string CatalogCode,
    string Mode,
    IReadOnlyList<QuizQuestionDto> Questions,
    QuizProgressDto Progress,
    bool RoundComplete);
