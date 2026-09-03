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

    /// <summary>Admin-Weg zur direkten Aufnahme eines Mitglieds ohne Beitrittsanfrage-Workflow.</summary>
    /// <summary>
    /// Nimmt jemanden direkt in den Verein auf, ohne den Antragsweg.
    ///
    /// Erlaubt für Admins und für Trainer:innen DIESES Vereins - sie führen
    /// den Verein, und wer eine Beitrittsanfrage freigeben darf, darf auch
    /// jemanden von sich aus aufnehmen.
    /// </summary>
    Task<Result> AddMemberAsync(Guid callerId, bool isAdmin, Guid clubId, AssignClubMemberRequest request, CancellationToken ct = default);
    /// <summary>Admin-Weg zum Entfernen eines aktiven Mitglieds aus einem Verein.</summary>
    Task<Result> RemoveMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default);

    /// <summary>Browsbare Liste aller Vereine für jeden eingeloggten User (nur Stammdaten, keine Trainer-/Mitgliederdetails).</summary>
    Task<Result<IReadOnlyList<ClubSummaryDto>>> GetBrowsableClubsAsync(CancellationToken ct = default);

    /// <summary>Eigene Mitgliedschaften/Beitrittsanfragen des aufrufenden Users, über alle Vereine.</summary>
    Task<Result<IReadOnlyList<ClubMembershipDto>>> GetMyMembershipsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Beitritt zu einem Verein anfragen (legt eine Pending-Mitgliedschaft an).</summary>
    Task<Result<ClubMembershipDto>> RequestJoinAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>Offene Beitrittsanfragen eines Vereins - nur für Trainer dieses Vereins.</summary>
    Task<Result<IReadOnlyList<ClubMemberDto>>> GetJoinRequestsAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>Beitrittsanfrage annehmen oder ablehnen - nur für Trainer des Vereins.</summary>
    Task<Result> DecideJoinRequestAsync(Guid callerId, Guid clubId, Guid membershipId, bool approve, CancellationToken ct = default);

    /// <summary>Aktive Mitglieder eines Vereins - nur für Trainer dieses Vereins (kein Zugriff auf fremde Vereine).</summary>
    Task<Result<IReadOnlyList<ClubMemberDto>>> GetMembersAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>Ein bereits genehmigtes Mitglied des eigenen Vereins zum Trainer befördern.</summary>
    Task<Result> PromoteMemberToTrainerAsync(Guid callerId, Guid clubId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Eigene aktive Mitgliedschaft beenden (Selbstbedienung, betrifft nur die Vereinsmitgliedschaft, keine Gruppen-/Trainer-Zuordnungen).</summary>
    Task<Result> LeaveClubAsync(Guid userId, Guid clubId, CancellationToken ct = default);
    /// <summary>
    /// Löst einen Nutzer aus allem Vereins- und Gruppenzusammenhang: offene und
    /// freigegebene Mitgliedschaften, Vereins- und Gruppentrainer-Zeilen,
    /// Trainerzuweisungen in beide Richtungen.
    ///
    /// Wird beim Löschen des Kontos aufgerufen. Ohne das bleiben die Zeilen
    /// stehen und tauchen mit "(unbekannt)" wieder auf - eine Beitrittsanfrage
    /// eines gelöschten Nutzers steht dann für immer in der Liste eines
    /// Trainers, der sie nicht auflösen kann.
    ///
    /// Nicht angefasst werden Gruppen, die der Nutzer LEITET: was mit ihnen
    /// passieren soll, ist eine Entscheidung, keine Aufräumarbeit.
    /// </summary>
    Task PurgeUserAsync(Guid userId, CancellationToken ct = default);

}
