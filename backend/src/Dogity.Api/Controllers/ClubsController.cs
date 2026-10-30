using Dogity.Application.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Api.Controllers;

/// <summary>
/// Mitglieder-/Vereins-facing Funktionen: Vereine browsen, Beitritt
/// anfragen, eigene Mitgliedschaften einsehen. Aktionen der Rollen (Anfragen
/// einsehen/entscheiden, Mitgliederliste, Rollen vergeben) sind im Service
/// jeweils auf den eigenen Verein und das passende Recht beschränkt (siehe
/// ClubAccessQueries). Getrennt von AdminController, der
/// vereinsübergreifende Admin-Operationen abdeckt (Verein anlegen,
/// Rollen ohne Mitgliedschaftsvoraussetzung zuweisen).
/// </summary>
[Route("api/clubs")]
public class ClubsController(
    IClubService clubService,
    IGroupService groupService,
    IClubRegistrationService registrations,
    IClubInviteService invites) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClubSummaryDto>>> GetClubs(CancellationToken ct)
    {
        var result = await clubService.GetBrowsableClubsAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    /// <summary>Die Vereine, in denen ich irgendeine Rolle habe - samt Rollen und Rechten.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ClubDto>>> GetMyClubRoles(CancellationToken ct) =>
        FromResult(await clubService.GetMyClubRolesAsync(CurrentUserId, ct));

    [HttpGet("my-memberships")]
    public async Task<ActionResult<IReadOnlyList<ClubMembershipDto>>> GetMyMemberships(CancellationToken ct)
    {
        var result = await clubService.GetMyMembershipsAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/join-requests")]
    public async Task<ActionResult<ClubMembershipDto>> RequestJoin(Guid id, CancellationToken ct)
    {
        var result = await clubService.RequestJoinAsync(CurrentUserId, id, ct: ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/join-requests")]
    public async Task<ActionResult<IReadOnlyList<ClubMemberDto>>> GetJoinRequests(Guid id, CancellationToken ct)
    {
        var result = await clubService.GetJoinRequestsAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/join-requests/{membershipId:guid}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(Guid id, Guid membershipId, ApproveJoinRequestRequest? request, CancellationToken ct)
    {
        // Körper optional: Ohne Gruppe verhält sich der Aufruf wie bisher.
        var result = await clubService.DecideJoinRequestAsync(CurrentUserId, id, membershipId, approve: true, request?.GroupId, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/join-requests/{membershipId:guid}/reject")]
    public async Task<IActionResult> RejectJoinRequest(Guid id, Guid membershipId, CancellationToken ct)
    {
        var result = await clubService.DecideJoinRequestAsync(CurrentUserId, id, membershipId, approve: false, ct: ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ClubMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        var result = await clubService.GetMembersAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Jemanden per E-Mail-Adresse in den Verein einladen. Für die
    /// Mitgliederverwaltung dieses Vereins - Mitglied wird die Person erst mit
    /// ihrer Zusage.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<AddClubMemberResultDto>> AddMember(Guid id, AssignClubMemberRequest request, CancellationToken ct) =>
        FromResult(await clubService.AddMemberAsync(CurrentUserId, IsAdmin, id, request, ct));

    /// <summary>Offene Einladungen des Vereins - nur die eingegebene Adresse, kein Name.</summary>
    [HttpGet("{id:guid}/invitations")]
    public async Task<ActionResult<IReadOnlyList<ClubInvitationDto>>> GetInvitations(Guid id, CancellationToken ct) =>
        FromResult(await clubService.GetInvitationsAsync(CurrentUserId, id, ct));

    [HttpDelete("{id:guid}/invitations/{membershipId:guid}")]
    public async Task<IActionResult> WithdrawInvitation(Guid id, Guid membershipId, CancellationToken ct) =>
        FromResult(await clubService.WithdrawInvitationAsync(CurrentUserId, id, membershipId, ct));

    /// <summary>Die eigene Einladung in den Verein annehmen.</summary>
    [HttpPost("{id:guid}/invitation/accept")]
    public async Task<IActionResult> AcceptInvitation(Guid id, CancellationToken ct) =>
        FromResult(await clubService.RespondToInvitationAsync(CurrentUserId, id, accept: true, ct));

    [HttpPost("{id:guid}/invitation/decline")]
    public async Task<IActionResult> DeclineInvitation(Guid id, CancellationToken ct) =>
        FromResult(await clubService.RespondToInvitationAsync(CurrentUserId, id, accept: false, ct));

    /// <summary>Ein Mitglied aus dem Verein nehmen (Mitgliederverwaltung); wer eine Rolle hat, bleibt bis zu deren Entzug.</summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct) =>
        FromResult(await clubService.RemoveClubMemberAsync(CurrentUserId, id, userId, ct));

    [HttpPost("{id:guid}/members/{userId:guid}/promote")]
    public async Task<IActionResult> PromoteToTrainer(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await clubService.PromoteMemberToTrainerAsync(CurrentUserId, id, userId, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/membership")]
    public async Task<IActionResult> LeaveClub(Guid id, CancellationToken ct)
    {
        var result = await clubService.LeaveClubAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Einen Verein beantragen. Freigegeben wird er von einem Admin - siehe
    /// ClubRegistration, warum es den Umweg braucht.
    /// </summary>
    [HttpPost("registrations")]
    public async Task<ActionResult<ClubRegistrationDto>> RequestClub(CreateClubRegistrationRequest request, CancellationToken ct) =>
        FromResult(await registrations.RequestAsync(CurrentUserId, request, ct));

    /// <summary>Eigene Anträge samt Stand - auch abgelehnte, mit Begründung.</summary>
    [HttpGet("registrations/mine")]
    public async Task<ActionResult<IReadOnlyList<ClubRegistrationDto>>> MyRegistrations(CancellationToken ct) =>
        FromResult(await registrations.GetMineAsync(CurrentUserId, ct));

    /// <summary>
    /// Stammdaten ändern - für Verwaltende des Vereins. Bisher konnte einen
    /// einmal angelegten Verein niemand umbenennen, auch kein Admin.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateClubRequest request, CancellationToken ct) =>
        FromResult(await clubService.UpdateClubAsync(CurrentUserId, IsAdmin, id, request, ct));

    /// <summary>
    /// Jemanden mit Rollen in den Verein berufen. Lag bisher ausschließlich beim
    /// globalen Admin - ein Verein konnte sich damit nicht selbst organisieren.
    /// </summary>
    [HttpPost("{id:guid}/trainers")]
    public async Task<IActionResult> AssignTrainer(Guid id, AssignClubTrainerRequest request, CancellationToken ct) =>
        FromResult(await clubService.AssignTrainerAsync(CurrentUserId, IsAdmin, id, request, ct));

    /// <summary>
    /// Alle Rollen einer Person entziehen. Der Dienst verhindert dabei, dass die
    /// letzte verwaltende Person geht - sonst bliebe ein Verein zurück, an den
    /// niemand mehr herankommt.
    /// </summary>
    [HttpDelete("{id:guid}/trainers/{userId:guid}")]
    public async Task<IActionResult> RemoveTrainer(Guid id, Guid userId, CancellationToken ct) =>
        FromResult(await clubService.RemoveTrainerAsync(CurrentUserId, IsAdmin, id, userId, ct));

    /// <summary>Die Personen mit Rolle samt ihren Rollen (Ausbildungswart:in, Verwaltung).</summary>
    [HttpGet("{id:guid}/roles")]
    public async Task<ActionResult<IReadOnlyList<ClubTrainerDto>>> GetRoles(Guid id, CancellationToken ct) =>
        FromResult(await clubService.GetRoleHoldersAsync(CurrentUserId, IsAdmin, id, ct));

    [HttpPut("{id:guid}/trainers/{userId:guid}/role")]
    public async Task<IActionResult> UpdateTrainerRole(Guid id, Guid userId, UpdateTrainerRoleRequest request, CancellationToken ct) =>
        FromResult(await clubService.UpdateTrainerRoleAsync(CurrentUserId, IsAdmin, id, userId, request.Role, ct));

    /// <summary>
    /// Der Einladungscode des Vereins, oder 204, wenn es keinen gibt. Für die
    /// Mitgliederverwaltung - dieselben, die Beitrittsanfragen entscheiden dürfen.
    /// </summary>
    [HttpGet("{id:guid}/invite-link")]
    public async Task<ActionResult<ClubInviteLinkDto>> GetInviteLink(Guid id, CancellationToken ct)
    {
        var result = await invites.GetAsync(CurrentUserId, id, ct);
        if (result.Succeeded && result.Value is null) return NoContent();
        return FromResult(result)!;
    }

    /// <summary>Neuen Code erzeugen. Der alte wird damit ungültig.</summary>
    [HttpPost("{id:guid}/invite-link")]
    public async Task<ActionResult<ClubInviteLinkDto>> CreateInviteLink(Guid id, CancellationToken ct) =>
        FromResult(await invites.RegenerateAsync(CurrentUserId, id, ct));

    /// <summary>Einladungslink abschalten.</summary>
    [HttpDelete("{id:guid}/invite-link")]
    public async Task<IActionResult> DisableInviteLink(Guid id, CancellationToken ct) =>
        FromResult(await invites.DisableAsync(CurrentUserId, id, ct));

    /// <summary>
    /// Öffentlich: Vereinsname zum Einladungscode, für die Seite hinter dem
    /// QR-Code. Eigene Drosselung je IP, damit sich Codes nicht durchprobieren
    /// lassen - bei 128 Bit Zufall ohnehin aussichtslos, aber der Endpunkt
    /// soll auch keine Last erzeugen.
    /// </summary>
    [HttpGet("invite/{code}")]
    [AllowAnonymous]
    [EnableRateLimiting("invite")]
    public async Task<ActionResult<ClubInvitePreviewDto>> GetInvitePreview(string code, CancellationToken ct) =>
        FromResult(await invites.GetPreviewAsync(code, ct));

    /// <summary>Beitritt über den Einladungslink anfragen (angemeldet). Die Freigabe bleibt beim Verein.</summary>
    [HttpPost("invite/{code}/join")]
    [EnableRateLimiting("invite")]
    public async Task<ActionResult<ClubMembershipDto>> JoinViaInvite(string code, CancellationToken ct) =>
        FromResult(await invites.JoinAsync(CurrentUserId, code, ct));

    [HttpGet("{id:guid}/groups")]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> GetClubGroups(Guid id, CancellationToken ct)
    {
        var result = await groupService.GetGroupsByClubAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }
}
