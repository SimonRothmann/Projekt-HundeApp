using Dogity.Application.Common;

namespace Dogity.Application.Admin;

public interface IAdminUsageService
{
    /// <summary>Wie und was genutzt wird - nur Zählungen aus vorhandenen Daten (siehe <see cref="AdminUsageDto"/>).</summary>
    Task<Result<AdminUsageDto>> GetAsync(CancellationToken ct = default);
}
