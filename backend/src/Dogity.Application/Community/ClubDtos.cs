using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record ClubDto(Guid Id, string Name, string? Description, int TrainerCount, int GroupCount);

public record ClubTrainerDto(Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset AssignedAt);

public record ClubDetailDto(ClubDto Club, IReadOnlyList<ClubTrainerDto> Trainers);

public record CreateClubRequest(string Name, string? Description);

public record AssignClubTrainerRequest(string Email);

/// <summary>Schlanke, für jeden eingeloggten User browsbare Vereinsliste ohne Trainer-/Gruppendetails.</summary>
public record ClubSummaryDto(Guid Id, string Name, string? Description);

public record ClubMembershipDto(Guid Id, Guid ClubId, string ClubName, ClubMembershipStatus Status, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt);

public record ClubMemberDto(Guid MembershipId, Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt);
