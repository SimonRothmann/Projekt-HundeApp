using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <param name="CanManage">
/// Ob die anfragende Person den Verein VERWALTEN darf (ClubRole.Verwaltung, siehe
/// ClubAccessQueries.CanManageClubAsync) - der Server entscheidet, das Frontend
/// blendet danach nur Knöpfe aus (Beförderung zur Trainer:in). Gefüllt nur in
/// "meine Vereine"; überall sonst false, weil dort keine Person gefragt wird.
/// </param>
public record ClubDto(Guid Id, string Name, string? Description, int TrainerCount, int GroupCount, bool CanManage = false);

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

/// <summary>
/// Schlanke, für jeden eingeloggten User browsbare Vereinsliste ohne Trainer-/Gruppendetails.
/// <paramref name="IsTrainer"/>: die anfragende Person ist Trainer:in dieses Vereins -
/// dann gehört sie schon dazu, auch ohne Mitgliedschaftszeile.
/// </summary>
public record ClubSummaryDto(Guid Id, string Name, string? Description, bool IsTrainer = false);

/// <summary>Annehmen mit Gruppe: optional, abwärtskompatibel zum Annehmen ohne Körper.</summary>
public record ApproveJoinRequestRequest(Guid? GroupId);

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
/// <param name="Source">Wie die Anfrage entstanden ist (numerisch: 0 = Liste, 1 = Einladungslink, 2 = Einladung durch eine Trainer:in).</param>
public record ClubMemberDto(Guid MembershipId, Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt, bool IsTrainer, ClubMembershipSource Source);

/// <summary>
/// Eine offene Einladung, wie der einladende Verein sie sieht: nur die Adresse,
/// die er selbst eingegeben hat, und wann - KEIN Name und nicht die Adresse des
/// Kontos. Der Name gehört der eingeladenen Person, bis sie zusagt.
/// </summary>
public record ClubInvitationDto(Guid MembershipId, string InvitedEmail, DateTimeOffset RequestedAt);

public record UpdateClubRequest(string? Name, string? Description);
public record UpdateTrainerRoleRequest(ClubRole Role);

/// <summary>Der Einladungscode eines Vereins - nur für dessen Trainer:innen sichtbar.</summary>
public record ClubInviteLinkDto(string Code);

/// <summary>
/// Was die öffentliche Einladungsseite ohne Anmeldung zeigt: nur den Namen.
/// Bewusst nichts weiter - wer den Link hat, soll nichts über Trainer:innen,
/// Gruppen oder Mitglieder erfahren.
/// </summary>
public record ClubInvitePreviewDto(string ClubName);

/// <summary>
/// Was "Mitglied per E-Mail" bewirkt hat: <c>Joined</c> = jetzt Mitglied (die
/// Person hatte selbst angefragt, oder der Betreiber nimmt auf), sonst ist sie
/// nur eingeladen und muss zusagen. Die Oberfläche braucht das für die richtige
/// Meldung - vorher hieß es "eingeladen", auch wenn die Person schon aufgenommen war.
/// </summary>
public record AddClubMemberResultDto(bool Joined);
