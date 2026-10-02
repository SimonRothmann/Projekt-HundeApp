using Dogity.Application.Common;

namespace Dogity.Application.Community;

public interface ITrainerOpenCountsService
{
    Task<Result<TrainerOpenCountsDto>> GetAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Offene Beitrittsanfragen einer Gruppe (nur Gruppen mit mindestens einer).</summary>
public record GroupOpenCountDto(Guid GroupId, int JoinRequests);

/// <summary>
/// Offenes bei einer Trainer:in: Beitrittsanfragen an Gruppen und Vereine
/// und Trainings, die noch Feedback oder Bewertungen brauchen.
/// </summary>
public record TrainerOpenCountsDto(
    int GroupJoinRequests,
    int ClubJoinRequests,
    int SessionsToRate,
    IReadOnlyList<GroupOpenCountDto> Groups);
