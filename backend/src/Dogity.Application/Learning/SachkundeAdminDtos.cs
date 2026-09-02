namespace Dogity.Application.Learning;

/// <summary>
/// Eine Frage in der Verwaltungsansicht - mit allem, was zum Überarbeiten
/// nötig ist, inklusive der Zeilenrollen und der Auffälligkeiten.
/// </summary>
public record AdminQuizQuestionDto(
    Guid Id,
    string CatalogCode,
    string CatalogName,
    string Number,
    string Section,
    string SectionName,
    string Kind,
    string Text,
    string? SampleSolution,
    string? ImageName,
    DateTimeOffset? EditedAt,
    IReadOnlyList<AdminQuizOptionDto> Options,
    IReadOnlyList<string> Flags);

/// <param name="Kind">Answer, Term oder Label.</param>
/// <param name="MatchKey">Bei Zuordnungen der Schlüssel; sonst leer.</param>
public record AdminQuizOptionDto(
    Guid Id,
    string Kind,
    string Text,
    bool IsCorrect,
    string? MatchKey,
    string? ImageName,
    int SortOrder,
    IReadOnlyList<string> Flags);

/// <summary>
/// Neue Fassung einer Frage. <see cref="Options"/> ersetzt die bestehende
/// Liste vollständig: Zeilen mit <c>Id</c> werden geändert, Zeilen ohne
/// angelegt, fehlende entfernt.
/// </summary>
public record UpdateQuizQuestionRequest(
    string Text,
    string? SampleSolution,
    IReadOnlyList<UpdateQuizOptionRequest> Options);

public record UpdateQuizOptionRequest(
    Guid? Id,
    string Kind,
    string Text,
    bool IsCorrect,
    string? MatchKey,
    string? ImageName);
