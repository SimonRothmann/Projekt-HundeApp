using Dogity.Application.Common;

namespace Dogity.Application.Community;

public interface IClubRegistrationService
{
    Task<Result<ClubRegistrationDto>> RequestAsync(Guid userId, CreateClubRegistrationRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ClubRegistrationDto>>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ClubRegistrationDto>>> GetPendingAsync(CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid adminId, Guid registrationId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid adminId, Guid registrationId, DecideClubRegistrationRequest request, CancellationToken ct = default);
}
