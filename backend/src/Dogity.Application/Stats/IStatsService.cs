using Dogity.Application.Common;

namespace Dogity.Application.Stats;

public interface IStatsService
{
    Task<Result<DashboardStatsDto>> GetDashboardAsync(Guid userId, CancellationToken ct = default);
}
