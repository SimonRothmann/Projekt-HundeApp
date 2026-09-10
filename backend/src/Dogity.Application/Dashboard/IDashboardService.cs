using Dogity.Application.Common;

namespace Dogity.Application.Dashboard;

public interface IDashboardService
{
    /// <summary>Die aktiven Hunde des Nutzers mit allem, was die Startseite je Hund zeigt.</summary>
    Task<Result<DashboardDto>> GetAsync(Guid userId, CancellationToken ct = default);
}
