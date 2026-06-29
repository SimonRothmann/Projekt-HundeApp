using Dogity.Domain.Sports;

namespace Dogity.Application.Sports;

public record SportDto(Guid Id, string Code, string Name, string? Description);

public record ExerciseDto(
    Guid Id,
    Guid SportId,
    string Name,
    string? Description,
    ExerciseDifficulty Difficulty,
    string? Category,
    string? ScoringCriteria,
    Guid? ClubId);

public record CreateExerciseRequest(
    Guid SportId,
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

public record RegulationDto(Guid Id, string Name, string? SourceUrl, DateTimeOffset? LastSyncedAt, string? LatestKnownVersionLabel);

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
