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

    /// <summary>
    /// Zählt die Daten von Konten, die es nicht mehr gibt (Altlast aus der Zeit
    /// vor der vollständigen Kontolöschung). <paramref name="callerId"/> ist die
    /// aufrufende Admin-Person und gilt nie als verwaist.
    /// </summary>
    Task<Result<OrphanedDataDto>> GetOrphanedDataAsync(Guid callerId, CancellationToken ct = default);

    /// <summary>
    /// Löscht diese Daten endgültig, über <c>IAccountDataService.PurgeAsync</c>.
    /// Idempotent. Bei unbrauchbarer Konto-Liste passiert nichts.
    /// </summary>
    Task<Result<OrphanedDataPurgeDto>> PurgeOrphanedDataAsync(Guid callerId, CancellationToken ct = default);

    /// <summary>Setzt das Passwort eines Benutzers administrativ neu.</summary>
    Task<Result> SetUserPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);
}
