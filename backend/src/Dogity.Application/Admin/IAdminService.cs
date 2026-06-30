using Dogity.Application.Common;

namespace Dogity.Application.Admin;

public interface IAdminService
{
    Task<Result<AdminStatsDto>> GetStatsAsync(CancellationToken ct = default);
    Task<Result<AdminUserPageDto>> GetUsersAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Result> UpdateRegulationSourceAsync(Guid regulationId, UpdateRegulationSourceRequest request, CancellationToken ct = default);

    Task<Result> LockUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> UnlockUserAsync(Guid userId, CancellationToken ct = default);
    Task<Result> DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
