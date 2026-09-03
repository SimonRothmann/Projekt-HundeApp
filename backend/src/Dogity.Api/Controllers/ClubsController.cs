using Dogity.Application.Community;
using Microsoft.AspNetCore.Mvc;

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
public class ClubsController(IClubService clubService, IGroupService groupService) : ApiControllerBase
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
        var result = await clubService.RequestJoinAsync(CurrentUserId, id, ct);
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

    [HttpGet("{id:guid}/groups")]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> GetClubGroups(Guid id, CancellationToken ct)
    {
        var result = await groupService.GetGroupsByClubAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }
}
