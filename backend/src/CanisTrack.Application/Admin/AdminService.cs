using CanisTrack.Application.Abstractions;
using CanisTrack.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Application.Admin;

/// <summary>
/// Plattform-Admin-Übersicht: Kennzahlen, Nutzerliste, Pflege der
/// Prüfungsordnungs-Metadaten (siehe <see cref="ISportCatalogService"/>
/// Hinweis "folgt in einem späteren Admin-Modul"). Volle Vereinsverwaltung
/// ist bewusst nicht Teil dieses Moduls (siehe PRODUCT_REQUIREMENTS.md
/// "Vereine" - Später/Post-MVP).
/// </summary>
public class AdminService(IApplicationDbContext db, IUserLookupService userLookup) : IAdminService
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

    public async Task<Result<IReadOnlyList<AdminUserDto>>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await userLookup.ListAllAsync(ct);
        var dtos = users.Select(u => new AdminUserDto(u.UserId, u.Email, u.FirstName, u.LastName, u.Roles)).ToList();
        return Result<IReadOnlyList<AdminUserDto>>.Success(dtos);
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
