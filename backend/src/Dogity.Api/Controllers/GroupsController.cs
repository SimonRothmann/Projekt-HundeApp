using Dogity.Application.Community;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[Route("api/groups")]
public class GroupsController(IGroupService groupService, IClubService clubService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GroupDto>>> GetMyGroups(CancellationToken ct)
    {
        var result = await groupService.GetMyGroupsAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpGet("my-trainer-status")]
    public async Task<ActionResult<object>> GetMyTrainerStatus(CancellationToken ct)
    {
        var isTrainer = await groupService.IsTrainerAsync(CurrentUserId, ct);
        return Ok(new { isTrainer });
    }

    [HttpGet("my-clubs")]
    public async Task<ActionResult<IReadOnlyList<ClubDto>>> GetMyClubs(CancellationToken ct)
    {
        var result = await clubService.GetMyClubsAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GroupDetailDto>> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await groupService.GetDetailAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<GroupDto>> Create(CreateGroupRequest request, CancellationToken ct)
    {
        var result = await groupService.CreateAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(GetDetail), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken ct)
    {
        var result = await groupService.AddMemberAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        var result = await groupService.RemoveMemberAsync(CurrentUserId, id, memberId, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/members/{memberId:guid}/dogs")]
    public async Task<ActionResult<IReadOnlyList<MemberDogDto>>> GetMemberDogs(Guid id, Guid memberId, CancellationToken ct)
    {
        var result = await groupService.GetMemberDogsAsync(CurrentUserId, id, memberId, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/trainer-assignments")]
    public async Task<IActionResult> AssignTrainerToDog(Guid id, AssignTrainerRequest request, CancellationToken ct)
    {
        var result = await groupService.AssignTrainerToDogAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/join-requests")]
    public async Task<IActionResult> RequestJoin(Guid id, CancellationToken ct)
    {
        var result = await groupService.RequestJoinGroupAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/join-requests")]
    public async Task<ActionResult<IReadOnlyList<GroupJoinRequestDto>>> GetJoinRequests(Guid id, CancellationToken ct)
    {
        var result = await groupService.GetGroupJoinRequestsAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/join-requests/{memberId:guid}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(Guid id, Guid memberId, CancellationToken ct)
    {
        var result = await groupService.DecideGroupJoinRequestAsync(CurrentUserId, id, memberId, approve: true, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/join-requests/{memberId:guid}/reject")]
    public async Task<IActionResult> RejectJoinRequest(Guid id, Guid memberId, CancellationToken ct)
    {
        var result = await groupService.DecideGroupJoinRequestAsync(CurrentUserId, id, memberId, approve: false, ct);
        return FromResult(result);
    }
}
