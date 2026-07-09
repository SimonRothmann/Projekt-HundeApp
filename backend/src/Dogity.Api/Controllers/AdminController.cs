using Dogity.Application.Abstractions;
using Dogity.Application.Admin;
using Dogity.Application.Community;
using Dogity.Application.Sports;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[Authorize(Roles = Roles.Admin)]
[Route("api/admin")]
public class AdminController(IAdminService adminService, IClubService clubService, IRegulationImportService importService) : ApiControllerBase
{
    [HttpGet("clubs")]
    public async Task<ActionResult<IReadOnlyList<ClubDto>>> GetClubs(CancellationToken ct)
    {
        var result = await clubService.GetClubsAsync(ct);
        return FromResult(result);
    }

    [HttpGet("clubs/{id:guid}")]
    public async Task<ActionResult<ClubDetailDto>> GetClubDetail(Guid id, CancellationToken ct)
    {
        var result = await clubService.GetDetailAsync(id, ct);
        return FromResult(result);
    }

    [HttpPost("clubs")]
    public async Task<ActionResult<ClubDto>> CreateClub(CreateClubRequest request, CancellationToken ct)
    {
        var result = await clubService.CreateAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(GetClubDetail), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("clubs/{id:guid}/trainers")]
    public async Task<IActionResult> AssignClubTrainer(Guid id, AssignClubTrainerRequest request, CancellationToken ct)
    {
        var result = await clubService.AssignTrainerAsync(id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("clubs/{id:guid}/trainers/{userId:guid}")]
    public async Task<IActionResult> RemoveClubTrainer(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await clubService.RemoveTrainerAsync(id, userId, ct);
        return FromResult(result);
    }

    [HttpPost("clubs/{id:guid}/members")]
    public async Task<IActionResult> AddClubMember(Guid id, AssignClubMemberRequest request, CancellationToken ct)
    {
        var result = await clubService.AddMemberAsync(id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("clubs/{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveClubMember(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await clubService.RemoveMemberAsync(id, userId, ct);
        return FromResult(result);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats(CancellationToken ct)
    {
        var result = await adminService.GetStatsAsync(ct);
        return FromResult(result);
    }

    [HttpGet("users")]
    public async Task<ActionResult<AdminUserPageDto>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await adminService.GetUsersAsync(page, pageSize, ct);
        return FromResult(result);
    }

    [HttpPost("users/{id:guid}/lock")]
    public async Task<IActionResult> LockUser(Guid id, CancellationToken ct)
    {
        if (id == CurrentUserId)
            return BadRequest(new { errors = new[] { "Eigenes Konto kann nicht gesperrt werden." } });

        var result = await adminService.LockUserAsync(id, ct);
        return FromResult(result);
    }

    [HttpPost("users/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockUser(Guid id, CancellationToken ct)
    {
        var result = await adminService.UnlockUserAsync(id, ct);
        return FromResult(result);
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        if (id == CurrentUserId)
            return BadRequest(new { errors = new[] { "Eigenes Konto kann nicht gelöscht werden." } });

        var result = await adminService.DeleteUserAsync(id, ct);
        return FromResult(result);
    }

    [HttpPost("users/{id:guid}/set-password")]
    public async Task<IActionResult> SetUserPassword(Guid id, SetUserPasswordRequest request, CancellationToken ct)
    {
        var result = await adminService.SetUserPasswordAsync(id, request.NewPassword, ct);
        return FromResult(result);
    }

    [HttpPut("regulations/{id:guid}/source")]
    public async Task<IActionResult> UpdateRegulationSource(Guid id, UpdateRegulationSourceRequest request, CancellationToken ct)
    {
        var result = await adminService.UpdateRegulationSourceAsync(id, request, ct);
        return FromResult(result);
    }

    [HttpPost("regulation-import/scan")]
    public async Task<ActionResult<IReadOnlyList<ParsedExerciseCandidate>>> ScanRegulationImport(CancellationToken ct)
    {
        var result = await importService.ScanAsync(ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Value);
    }

    [HttpPost("regulation-import/apply")]
    public async Task<IActionResult> ApplyRegulationImport(ApplyRegulationImportRequest request, CancellationToken ct)
    {
        var result = await importService.ApplyAsync(request, ct);
        return FromResult(result);
    }
}
