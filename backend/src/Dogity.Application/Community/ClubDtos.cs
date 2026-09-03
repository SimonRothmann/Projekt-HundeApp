using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record ClubDto(Guid Id, string Name, string? Description, int TrainerCount, int GroupCount);

public record ClubTrainerDto(Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset AssignedAt);

public record ClubDetailDto(ClubDto Club, IReadOnlyList<ClubTrainerDto> Trainers, IReadOnlyList<ClubMemberDto> Members);

public record CreateClubRequest(string Name, string? Description);

public record AssignClubTrainerRequest(string Email);
public record AssignClubMemberRequest(string Email);

/// <summary>Schlanke, für jeden eingeloggten User browsbare Vereinsliste ohne Trainer-/Gruppendetails.</summary>
public record ClubSummaryDto(Guid Id, string Name, string? Description);

public record ClubMembershipDto(Guid Id, Guid ClubId, string ClubName, ClubMembershipStatus Status, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt);

/// <summary>
/// Ein Vereinsmitglied - <paramref name="IsTrainer"/> sagt, ob es zugleich
/// Trainer:in dieses Vereins ist.
///
/// Das Kennzeichen kommt vom Server und wird nicht im Frontend hergeleitet:
/// Trainer:innen stehen in einer eigenen Tabelle (ClubTrainers), nicht in den
/// Mitgliedschaften. Wer die Liste ohne dieses Wissen anzeigt, bietet
/// "Zum Trainer machen" auch bei denen an, die es längst sind.
/// </summary>
public record ClubMemberDto(Guid MembershipId, Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt, bool IsTrainer);
