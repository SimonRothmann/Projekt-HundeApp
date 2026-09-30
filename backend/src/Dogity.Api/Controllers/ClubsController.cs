using Dogity.Application.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Api.Controllers;

/// <summary>
/// Mitglieder-/Trainer-facing Vereinsfunktionen: Vereine browsen, Beitritt
/// anfragen, eigene Mitgliedschaften einsehen. Trainer-Aktionen (Anfragen
/// einsehen/entscheiden, Mitgliederliste, Beförderung) sind im Service
/// jeweils auf den eigenen Verein beschränkt (siehe ClubService -
/// IsClubTrainerAsync-Prüfung). Getrennt von AdminController, der
/// vereinsübergreifende Admin-Operationen abdeckt (Verein anlegen,
/// Trainer ohne Mitgliedschaftsvoraussetzung zuweisen).
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
        var result = await clubService.GetBrowsableClubsAsync(ct);
        return FromResult(result);
    }

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
    public async Task<IActionResult> ApproveJoinRequest(Guid id, Guid membershipId, CancellationToken ct)
    {
        var result = await clubService.DecideJoinRequestAsync(CurrentUserId, id, membershipId, approve: true, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/join-requests/{membershipId:guid}/reject")]
    public async Task<IActionResult> RejectJoinRequest(Guid id, Guid membershipId, CancellationToken ct)
    {
        var result = await clubService.DecideJoinRequestAsync(CurrentUserId, id, membershipId, approve: false, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ClubMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        var result = await clubService.GetMembersAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Jemanden direkt in den Verein aufnehmen - ohne dass er selbst eine
    /// Anfrage stellen muss. Für Trainer:innen dieses Vereins.
    /// </summary>
    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, AssignClubMemberRequest request, CancellationToken ct) =>
        FromResult(await clubService.AddMemberAsync(CurrentUserId, IsAdmin, id, request, ct));

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
    /// Trainer:in berufen. Lag bisher ausschließlich beim globalen Admin -
    /// ein Verein konnte sich damit nicht selbst organisieren.
    /// </summary>
    [HttpPost("{id:guid}/trainers")]
    public async Task<IActionResult> AssignTrainer(Guid id, AssignClubTrainerRequest request, CancellationToken ct) =>
        FromResult(await clubService.AssignTrainerAsync(CurrentUserId, IsAdmin, id, request, ct));

    /// <summary>
    /// Trainer:in abberufen. Der Dienst verhindert dabei, dass die letzte
    /// verwaltende Person geht - sonst bliebe ein Verein zurück, an den
    /// niemand mehr herankommt.
    /// </summary>
    [HttpDelete("{id:guid}/trainers/{userId:guid}")]
    public async Task<IActionResult> RemoveTrainer(Guid id, Guid userId, CancellationToken ct) =>
        FromResult(await clubService.RemoveTrainerAsync(CurrentUserId, IsAdmin, id, userId, ct));

    [HttpPut("{id:guid}/trainers/{userId:guid}/role")]
    public async Task<IActionResult> UpdateTrainerRole(Guid id, Guid userId, UpdateTrainerRoleRequest request, CancellationToken ct) =>
        FromResult(await clubService.UpdateTrainerRoleAsync(CurrentUserId, IsAdmin, id, userId, request.Role, ct));

    /// <summary>
    /// Der Einladungscode des Vereins, oder 204, wenn es keinen gibt. Für
    /// Trainer:innen - dieselben, die Beitrittsanfragen entscheiden dürfen.
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
