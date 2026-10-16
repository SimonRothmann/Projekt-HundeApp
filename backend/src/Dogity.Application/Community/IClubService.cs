using Dogity.Domain.Community;
using Dogity.Application.Common;

namespace Dogity.Application.Community;

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
    Task<Result> AssignTrainerAsync(Guid callerId, bool isAdmin, Guid clubId, AssignClubTrainerRequest request, CancellationToken ct = default);
    Task<Result> UpdateClubAsync(Guid callerId, bool isAdmin, Guid clubId, UpdateClubRequest request, CancellationToken ct = default);
    Task<Result> UpdateTrainerRoleAsync(Guid callerId, bool isAdmin, Guid clubId, Guid userId, ClubRole role, CancellationToken ct = default);
    Task<Result> RemoveTrainerAsync(Guid callerId, bool isAdmin, Guid clubId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Holt jemanden per E-Mail-Adresse in den Verein.
    ///
    /// Der Betreiber (Admin) nimmt direkt auf. Trainer:innen DIESES Vereins
    /// laden ein - Mitglied wird die Person erst mit ihrer Zusage
    /// (<see cref="RespondToInvitationAsync"/>); hatte sie selbst schon
    /// angefragt, wird die Anfrage gleich freigegeben.
    /// </summary>
    Task<Result<AddClubMemberResultDto>> AddMemberAsync(Guid callerId, bool isAdmin, Guid clubId, AssignClubMemberRequest request, CancellationToken ct = default);

    /// <summary>Offene Einladungen eines Vereins (nur eingegebene Adresse, kein Name) - nur für Trainer dieses Vereins.</summary>
    Task<Result<IReadOnlyList<ClubInvitationDto>>> GetInvitationsAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>Eine offene Einladung zurückziehen - nur für Trainer dieses Vereins.</summary>
    Task<Result> WithdrawInvitationAsync(Guid callerId, Guid clubId, Guid membershipId, CancellationToken ct = default);

    /// <summary>Die eigene Einladung in einen Verein annehmen (Mitglied) oder ablehnen.</summary>
    Task<Result> RespondToInvitationAsync(Guid userId, Guid clubId, bool accept, CancellationToken ct = default);

    /// <summary>Die eigenen offenen Vereinseinladungen für die Startseite (Verein-Id und -Name).</summary>
    Task<IReadOnlyList<(Guid ClubId, string ClubName)>> GetMyOpenInvitationsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Admin-Weg zum Entfernen eines aktiven Mitglieds aus einem Verein.</summary>
    Task<Result> RemoveMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default);

    /// <summary>Browsbare Liste aller Vereine für jeden eingeloggten User (nur Stammdaten, keine Trainer-/Mitgliederdetails).</summary>
    Task<Result<IReadOnlyList<ClubSummaryDto>>> GetBrowsableClubsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Eigene Mitgliedschaften/Beitrittsanfragen des aufrufenden Users, über alle Vereine.</summary>
    Task<Result<IReadOnlyList<ClubMembershipDto>>> GetMyMembershipsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Beitritt zu einem Verein anfragen (legt eine Pending-Mitgliedschaft an).
    /// <paramref name="source"/> hält fest, auf welchem Weg die Anfrage kam.
    /// </summary>
    Task<Result<ClubMembershipDto>> RequestJoinAsync(Guid userId, Guid clubId, ClubMembershipSource source = ClubMembershipSource.Directory, CancellationToken ct = default);

    /// <summary>Offene Beitrittsanfragen eines Vereins - nur für Trainer dieses Vereins.</summary>
    Task<Result<IReadOnlyList<ClubMemberDto>>> GetJoinRequestsAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// Beitrittsanfrage annehmen oder ablehnen - nur für Trainer des Vereins.
    /// Mit <paramref name="groupId"/> (nur beim Annehmen) wird die Person
    /// zugleich in diese Gruppe EINGELADEN - nie aufgenommen, denn die
    /// Vereinsanfrage ist keine Zustimmung zur Gruppe. Scheitert die Gruppe,
    /// bleibt auch die Anfrage offen.
    /// </summary>
    Task<Result> DecideJoinRequestAsync(Guid callerId, Guid clubId, Guid membershipId, bool approve, Guid? groupId = null, CancellationToken ct = default);

    /// <summary>Aktive Mitglieder eines Vereins - nur für Trainer dieses Vereins (kein Zugriff auf fremde Vereine).</summary>
    Task<Result<IReadOnlyList<ClubMemberDto>>> GetMembersAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>Ein bereits genehmigtes Mitglied des eigenen Vereins zum Trainer befördern.</summary>
    Task<Result> PromoteMemberToTrainerAsync(Guid callerId, Guid clubId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Eigene aktive Mitgliedschaft beenden (Selbstbedienung). Löst über DetachFromClubAsync auch Gruppen- und Betreuungszuordnungen in diesem Verein.</summary>
    Task<Result> LeaveClubAsync(Guid userId, Guid clubId, CancellationToken ct = default);
}
