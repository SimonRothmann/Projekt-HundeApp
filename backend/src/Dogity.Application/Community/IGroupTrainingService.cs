using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Verwaltung von Gruppen-Trainingseinheiten: vorgefertigte Vorlagen für
/// Welpen/Junghunde und eigene, vom Trainer pro Gruppe zusammengestellte
/// Einheiten (siehe docs/GROUP_TRAINING_PLANS.md).
/// </summary>
public interface IGroupTrainingService
{
    Task<Result<GroupTrainingLibraryDto>> GetLibraryAsync(Guid userId, CancellationToken ct = default);
    Task<Result<GroupTrainingUnitDto>> GetUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GroupTrainingUnitDto>>> GetGroupUnitsAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task<Result<GroupTrainingUnitDto>> CreateUnitAsync(Guid userId, CreateGroupTrainingUnitRequest request, CancellationToken ct = default);
    Task<Result<GroupTrainingUnitDto>> UpdateUnitAsync(Guid userId, Guid unitId, UpdateGroupTrainingUnitRequest request, CancellationToken ct = default);
    Task<Result> DeleteUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default);
    Task<Result<GroupTrainingUnitDto>> CopyUnitToGroupAsync(Guid userId, Guid unitId, Guid groupId, CancellationToken ct = default);
}
