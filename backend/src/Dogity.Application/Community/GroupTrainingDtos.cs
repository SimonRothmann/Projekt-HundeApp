using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record GroupTrainingItemDto(
    Guid Id,
    string Title,
    string? Description,
    string? Focus,
    int? DurationMinutes,
    int SortOrder);

public record GroupTrainingUnitDto(
    Guid Id,
    string Title,
    string? Description,
    GroupTrainingCategory Category,
    Guid? GroupId,
    // true = vorgefertigte System-Vorlage (nicht bearbeitbar, für alle Trainer sichtbar).
    bool IsTemplate,
    // true = vom aktuellen Trainer erstellt (bearbeit-/löschbar).
    bool IsMine,
    int TotalMinutes,
    IReadOnlyList<GroupTrainingItemDto> Items);

/// <summary>
/// Die Bibliothek, die ein Trainer sieht: vorgefertigte Vorlagen (Welpen/
/// Junghunde/...) plus die selbst zusammengestellten Einheiten.
/// </summary>
public record GroupTrainingLibraryDto(
    IReadOnlyList<GroupTrainingUnitDto> Templates,
    IReadOnlyList<GroupTrainingUnitDto> Mine);

public record GroupTrainingItemInput(
    string Title,
    string? Description = null,
    string? Focus = null,
    int? DurationMinutes = null);

public record CreateGroupTrainingUnitRequest(
    string Title,
    string? Description,
    GroupTrainingCategory Category,
    Guid? GroupId,
    IReadOnlyList<GroupTrainingItemInput> Items);

public record UpdateGroupTrainingUnitRequest(
    string Title,
    string? Description,
    GroupTrainingCategory Category,
    IReadOnlyList<GroupTrainingItemInput> Items);

public record CopyGroupTrainingUnitRequest(Guid GroupId);
