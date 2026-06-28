using CanisTrack.Application.Common;
using CanisTrack.Domain.Planning;

namespace CanisTrack.Application.Planning;

public interface IGoalService
{
    Task<Result<IReadOnlyList<GoalDto>>> GetByDogAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result<GoalDto>> GetByIdAsync(Guid userId, Guid goalId, CancellationToken ct = default);
    Task<Result<GoalDto>> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken ct = default);
    Task<Result<GoalDto>> UpdateStatusAsync(Guid userId, Guid goalId, GoalStatus status, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid goalId, CancellationToken ct = default);
}
