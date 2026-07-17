using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Admin;

/// <summary>
/// Plattform-Admin-Übersicht: Kennzahlen, Nutzerliste, Pflege der
/// Prüfungsordnungs-Metadaten (siehe <see cref="ISportCatalogService"/>
/// Hinweis "folgt in einem späteren Admin-Modul"). Volle Vereinsverwaltung
/// ist bewusst nicht Teil dieses Moduls (siehe PRODUCT_REQUIREMENTS.md
/// "Vereine" - Später/Post-MVP).
/// </summary>
public class AdminService(IApplicationDbContext db, IUserLookupService userLookup, IRefreshTokenService refreshTokens) : IAdminService
{
    public async Task<Result<AdminStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new AdminStatsDto(
            await userLookup.CountAsync(ct),
            await db.Dogs.CountAsync(ct),
            await db.Groups.CountAsync(ct),
            await db.TrainingSessions.CountAsync(ct),
            await db.GpsTracks.CountAsync(ct));

        return Result<AdminStatsDto>.Success(stats);
    }

    public async Task<Result<AdminUserPageDto>> GetUsersAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (users, total) = await userLookup.ListPagedAsync(page, pageSize, ct);
        var dtos = users.Select(u => new AdminUserDto(u.UserId, u.Email, u.FirstName, u.LastName, u.Roles, u.IsLockedOut)).ToList();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        return Result<AdminUserPageDto>.Success(new AdminUserPageDto(dtos, total, totalPages, page, pageSize));
    }

    public async Task<Result> LockUserAsync(Guid userId, CancellationToken ct = default)
    {
        var ok = await userLookup.LockUserAsync(userId, ct);
        if (!ok) return Result.Failure("Benutzer nicht gefunden.");
        // Refresh-Tokens widerrufen, damit die Sperre sofort greift - sonst
        // könnte der Nutzer bis zum Ablauf seines Access-Tokens (60 min)
        // weiter neue Access-Tokens nachladen.
        await refreshTokens.RevokeAllForUserAsync(userId, ct);
        return Result.Success();
    }

    public async Task<Result> UnlockUserAsync(Guid userId, CancellationToken ct = default)
    {
        var ok = await userLookup.UnlockUserAsync(userId, ct);
        return ok ? Result.Success() : Result.Failure("Benutzer nicht gefunden.");
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var ok = await userLookup.DeleteUserAsync(userId, ct);
        if (!ok) return Result.Failure("Benutzer nicht gefunden oder Löschung fehlgeschlagen.");
        await refreshTokens.RevokeAllForUserAsync(userId, ct);
        return Result.Success();
    }

    public async Task<Result> SetUserPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure("Passwort ist erforderlich.");

        var (success, errors) = await userLookup.SetPasswordAsync(userId, newPassword, ct);
        return success ? Result.Success() : Result.Failure(errors);
    }

    public async Task<Result> UpdateRegulationSourceAsync(Guid regulationId, UpdateRegulationSourceRequest request, CancellationToken ct = default)
    {
        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == regulationId, ct);
        if (regulation is null)
            return Result.Failure("Prüfungsordnung nicht gefunden.");

        regulation.SourceUrl = request.SourceUrl;
        regulation.LatestKnownVersionLabel = request.LatestKnownVersionLabel;
        regulation.LastSyncedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
