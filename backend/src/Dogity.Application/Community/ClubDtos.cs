using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record ClubDto(Guid Id, string Name, string? Description, int TrainerCount, int GroupCount);

public record ClubTrainerDto(Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset AssignedAt);

public record ClubDetailDto(ClubDto Club, IReadOnlyList<ClubTrainerDto> Trainers, IReadOnlyList<ClubMemberDto> Members);

public record CreateClubRequest(string Name, string? Description);

/// <param name="Role">
/// Standardmäßig <see cref="ClubRole.Training"/>: Wer neu dazukommt, soll
/// erst einmal trainieren dürfen, nicht sofort andere abberufen können.
///
/// ACHTUNG: Die API überträgt Aufzählungen NUMERISCH - es ist kein
/// JsonStringEnumConverter eingerichtet (wie bei ExerciseDifficulty,
/// GoalStatus und DogCondition). Also 0 = Training, 1 = Verwaltung; ein
/// gesendetes "Training" wird mit 400 abgewiesen.
/// </param>
public record AssignClubTrainerRequest(string Email, ClubRole Role = ClubRole.Training);
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

public record UpdateClubRequest(string? Name, string? Description);
public record UpdateTrainerRoleRequest(ClubRole Role);
