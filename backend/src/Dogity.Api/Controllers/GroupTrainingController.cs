using Dogity.Application.Community;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[Route("api/group-training")]
public class GroupTrainingController(IGroupTrainingService service) : ApiControllerBase
{
    /// <summary>Vorgefertigte Vorlagen (Welpen/Junghunde) + eigene Einheiten des Trainers.</summary>
    [HttpGet("library")]
    public async Task<ActionResult<GroupTrainingLibraryDto>> GetLibrary(CancellationToken ct)
    {
        var result = await service.GetLibraryAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpGet("units/{id:guid}")]
    public async Task<ActionResult<GroupTrainingUnitDto>> GetUnit(Guid id, CancellationToken ct)
    {
        var result = await service.GetUnitAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpGet("groups/{groupId:guid}/units")]
    public async Task<ActionResult<IReadOnlyList<GroupTrainingUnitDto>>> GetGroupUnits(Guid groupId, CancellationToken ct)
    {
        var result = await service.GetGroupUnitsAsync(CurrentUserId, groupId, ct);
        return FromResult(result);
    }

    [HttpPost("units")]
    public async Task<ActionResult<GroupTrainingUnitDto>> Create(CreateGroupTrainingUnitRequest request, CancellationToken ct)
    {
        var result = await service.CreateUnitAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(GetUnit), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("units/{id:guid}")]
    public async Task<ActionResult<GroupTrainingUnitDto>> Update(Guid id, UpdateGroupTrainingUnitRequest request, CancellationToken ct)
    {
        var result = await service.UpdateUnitAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("units/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteUnitAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost("units/{id:guid}/copy-to-group")]
    public async Task<ActionResult<GroupTrainingUnitDto>> CopyToGroup(Guid id, CopyGroupTrainingUnitRequest request, CancellationToken ct)
    {
        var result = await service.CopyUnitToGroupAsync(CurrentUserId, id, request.GroupId, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(GetUnit), new { id = result.Value!.Id }, result.Value);
    }
}
