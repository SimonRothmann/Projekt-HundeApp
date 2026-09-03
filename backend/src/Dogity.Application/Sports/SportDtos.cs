using Dogity.Domain.Sports;

namespace Dogity.Application.Sports;

public record SportDto(Guid Id, string Code, string Name, string? Description, Guid? ClubId);

public record CreateSportRequest(string Code, string Name, string? Description, Guid? ClubId);

public record ExerciseDto(
    Guid Id,
    Guid? SportId,
    string Name,
    string? Description,
    ExerciseDifficulty Difficulty,
    string? Category,
    string? ScoringCriteria,
    Guid? ClubId);

public record CreateExerciseRequest(
    Guid? SportId,
    string Name,
    string? Description,
    ExerciseDifficulty Difficulty,
    string? Category,
    string? ScoringCriteria,
    Guid? ClubId);

public record UpdateExerciseRequest(
    string Name,
    string? Description,
    ExerciseDifficulty Difficulty,
    string? Category,
    string? ScoringCriteria);

public record UpdateSportRequest(string Name, string? Description);

public record RegulationDto(Guid Id, string Name, string? SourceUrl, DateTimeOffset? LastSyncedAt, string? LatestKnownVersionLabel, string? Description, string? CountryCode);

public record UpdateRegulationRequest(string Name, string? Description, string? SourceUrl, string? CountryCode);

public record AddRegulationExerciseRequest(Guid ExerciseId, bool IsMandatory, int MaxPoints, string? ScoringNotes);

public record UpdateRegulationExerciseRequest(bool IsMandatory, int MaxPoints, string? ScoringNotes);

public record RegulationVersionDto(Guid Id, string VersionLabel, DateOnly ValidFrom);

public record RegulationExerciseDto(
    Guid ExerciseId,
    string ExerciseName,
    bool IsMandatory,
    int MaxPoints,
    string? ScoringNotes);

public record RegulationDetailDto(
    RegulationDto Regulation,
    RegulationVersionDto CurrentVersion,
    IReadOnlyList<RegulationExerciseDto> Exercises);

/// <summary>
/// Ein wählbarer Geltungsbereich.
/// </summary>
/// <param name="Code">ISO 3166-1 alpha-2, z.B. "DE".</param>
/// <param name="RegulationCount">
/// Wie viele Prüfungsordnungen hier gelten - null mitgezählt, denn eine
/// international gültige Ordnung gilt auch hier. Null bedeutet nicht
/// "kaputt", sondern "noch keine Inhalte"; die Oberfläche sagt das auch so.
/// </param>
public record CountryDto(string Code, int RegulationCount);
