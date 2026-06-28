using CanisTrack.Application.Common;

namespace CanisTrack.Application.Community;

/// <summary>
/// Admin-Use-Cases für Vereinsverwaltung (siehe USER FLOWS.md "Verein:
/// Admin legt Verein an" -> "Trainingsgruppen"). Autorisierung erfolgt am
/// Controller per [Authorize(Roles = ADMIN)], analog zu IAdminService.
/// </summary>
public interface IClubService
{
    Task<Result<IReadOnlyList<ClubDto>>> GetClubsAsync(CancellationToken ct = default);

    /// <summary>Vereine, für die der Nutzer als Trainer eingetragen ist (siehe ClubTrainer).</summary>
    Task<Result<IReadOnlyList<ClubDto>>> GetMyClubsAsync(Guid userId, CancellationToken ct = default);
    Task<Result<ClubDetailDto>> GetDetailAsync(Guid clubId, CancellationToken ct = default);
    Task<Result<ClubDto>> CreateAsync(CreateClubRequest request, CancellationToken ct = default);
    Task<Result> AssignTrainerAsync(Guid clubId, AssignClubTrainerRequest request, CancellationToken ct = default);
    Task<Result> RemoveTrainerAsync(Guid clubId, Guid userId, CancellationToken ct = default);
}
