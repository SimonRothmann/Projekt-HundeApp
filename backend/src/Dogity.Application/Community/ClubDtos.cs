using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <param name="TrainerCount">Personen mit einer Trainer-Rolle (nicht: Mitgliederverwaltung/Vorstand).</param>
/// <param name="CanManage">
/// Ob die anfragende Person den Verein VERWALTEN darf (ClubRole.Verwaltung, siehe
/// ClubAccessQueries.CanManageClubAsync) - der Server entscheidet, das Frontend
/// blendet danach nur Knöpfe aus. Gefüllt nur in "meine Vereine"; überall sonst
/// false, weil dort keine Person gefragt wird. Ebenso die folgenden Angaben:
/// <paramref name="Roles"/> sind die eigenen Rollen (numerisch, siehe
/// <see cref="AssignClubTrainerRequest"/>), die übrigen daraus abgeleitete Rechte
/// (dieselben Rollenmengen wie ClubAccessQueries).
/// </param>
public record ClubDto(
    Guid Id,
    string Name,
    string? Description,
    int TrainerCount,
    int GroupCount,
    bool CanManage = false,
    ClubRole Roles = 0,
    bool CanManageMembers = false,
    bool CanAppointTrainers = false,
    bool IsTrainer = false)
{
    /// <summary>Der Verein aus Sicht einer Person mit diesen Rollen.</summary>
    public static ClubDto Fuer(Guid id, string name, string? description, int trainerCount, int groupCount, ClubRole roles) =>
        new(id, name, description, trainerCount, groupCount,
            CanManage: roles.HasFlag(ClubRole.Verwaltung),
            Roles: roles,
            CanManageMembers: (roles & ClubRollen.Mitglieder) != 0,
            CanAppointTrainers: (roles & ClubRollen.TrainerBerufen) != 0,
            IsTrainer: (roles & ClubRollen.Trainer) != 0);
}

/// <param name="Roles">Die Rollen der Person im Verein (<see cref="ClubRole"/>, numerisch).</param>
public record ClubTrainerDto(Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset AssignedAt, ClubRole Roles = 0);

public record ClubDetailDto(ClubDto Club, IReadOnlyList<ClubTrainerDto> Trainers, IReadOnlyList<ClubMemberDto> Members);

public record CreateClubRequest(string Name, string? Description);

/// <param name="Role">
/// Die Rollen der neuen Person als Menge. Standardmäßig
/// <see cref="ClubRole.Training"/>: Wer neu dazukommt, soll erst einmal
/// trainieren dürfen, nicht sofort andere abberufen können.
///
/// ACHTUNG: Die API überträgt Aufzählungen NUMERISCH - es ist kein
/// JsonStringEnumConverter eingerichtet (wie bei ExerciseDifficulty,
/// GoalStatus und DogCondition). Rollen sind Bits: 1 = Training,
/// 2 = Verwaltung, 4 = Ausbildungswart, 8 = Mitgliederverwaltung,
/// 16 = Vorstand; mehrere werden addiert (3 = Training + Verwaltung). Ein
/// gesendetes "Training" wird mit 400 abgewiesen.
/// </param>
public record AssignClubTrainerRequest(string Email, ClubRole Role = ClubRole.Training);
public record AssignClubMemberRequest(string Email);

/// <summary>
/// Schlanke, für jeden eingeloggten User browsbare Vereinsliste ohne Trainer-/Gruppendetails.
/// <paramref name="IsTrainer"/>: die anfragende Person ist Trainer:in dieses Vereins -
/// dann gehört sie schon dazu, auch ohne Mitgliedschaftszeile. Das gilt für jede
/// Rolle im Verein, nicht nur die Trainer-Rollen; <paramref name="Roles"/> sagt welche.
/// </summary>
public record ClubSummaryDto(Guid Id, string Name, string? Description, bool IsTrainer = false, ClubRole Roles = 0);

/// <summary>Annehmen mit Gruppe: optional, abwärtskompatibel zum Annehmen ohne Körper.</summary>
public record ApproveJoinRequestRequest(Guid? GroupId);

public record ClubMembershipDto(Guid Id, Guid ClubId, string ClubName, ClubMembershipStatus Status, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt);

/// <summary>
/// Ein Vereinsmitglied - <paramref name="IsTrainer"/> sagt, ob es zugleich
/// Trainer:in dieses Vereins ist.
///
/// Das Kennzeichen kommt vom Server und wird nicht im Frontend hergeleitet:
/// Trainer:innen stehen in einer eigenen Tabelle (ClubTrainers), nicht in den
/// Mitgliedschaften. Gemeint ist eine Trainer-Rolle, nicht jede Rolle. Wer die Liste ohne dieses Wissen anzeigt, bietet
/// "Zum Trainer machen" auch bei denen an, die es längst sind.
/// </summary>
/// <param name="HatRolle">Die Person hat irgendeine Rolle im Verein (auch Mitgliederverwaltung/Vorstand) und lässt sich deshalb nicht entfernen, bevor ihr die Rollen genommen sind.</param>
/// <param name="Source">Wie die Anfrage entstanden ist (numerisch: 0 = Liste, 1 = Einladungslink, 2 = Einladung durch eine Trainer:in).</param>
public record ClubMemberDto(Guid MembershipId, Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset RequestedAt, DateTimeOffset? DecidedAt, bool IsTrainer, ClubMembershipSource Source, bool HatRolle);

/// <summary>
/// Eine offene Einladung, wie der einladende Verein sie sieht: nur die Adresse,
/// die er selbst eingegeben hat, und wann - KEIN Name und nicht die Adresse des
/// Kontos. Der Name gehört der eingeladenen Person, bis sie zusagt.
/// </summary>
public record ClubInvitationDto(Guid MembershipId, string InvitedEmail, DateTimeOffset RequestedAt);

public record UpdateClubRequest(string? Name, string? Description);
/// <param name="Role">Die vollständige neue Rollenmenge (siehe <see cref="AssignClubTrainerRequest"/>); 0 entfernt die Person.</param>
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
