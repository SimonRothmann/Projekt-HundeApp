using Dogity.Domain.Community;

namespace Dogity.Application.Community;

// ---- Baustein (wiederverwendbare Übung) ----

public record GroupTrainingExerciseDto(
    Guid Id,
    Guid ClubId,
    GroupTrainingCategory Category,
    string Title,
    string? Focus,
    int? DurationMinutes,
    string? Description,
    GroupExamTarget ExamTargets);

public record UpsertExerciseRequest(
    GroupTrainingCategory Category,
    string Title,
    string? Focus,
    int? DurationMinutes,
    string? Description,
    GroupExamTarget ExamTargets = GroupExamTarget.None);

// ---- Einheit (geordnete Zusammenstellung von Bausteinen) ----

public record GroupTrainingUnitItemDto(Guid Id, Guid ExerciseId, int SortOrder, GroupTrainingExerciseDto Exercise);

public record GroupTrainingUnitDto(
    Guid Id,
    Guid ClubId,
    GroupTrainingCategory Category,
    string Title,
    string? Description,
    int TotalMinutes,
    IReadOnlyList<GroupTrainingUnitItemDto> Items);

/// <summary>ExerciseIds in der gewünschten Reihenfolge der Einheit.</summary>
public record UpsertUnitRequest(
    GroupTrainingCategory Category,
    string Title,
    string? Description,
    IReadOnlyList<Guid> ExerciseIds);

// ---- Bibliothek eines Vereins ----

public record GroupTrainingLibraryDto(
    Guid ClubId,
    string ClubName,
    IReadOnlyList<GroupTrainingExerciseDto> Exercises,
    IReadOnlyList<GroupTrainingUnitDto> Units);
