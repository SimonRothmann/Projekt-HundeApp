using Dogity.Application.Planning;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[Route("api/goals")]
public class GoalsController(IGoalService goalService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GoalDto>>> GetByDog([FromQuery] Guid dogId, CancellationToken ct)
    {
        var result = await goalService.GetByDogAsync(CurrentUserId, dogId, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GoalDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await goalService.GetByIdAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<GoalDto>> Create(CreateGoalRequest request, CancellationToken ct)
    {
        var result = await goalService.CreateAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<GoalDto>> UpdateStatus(Guid id, UpdateGoalStatusRequest request, CancellationToken ct)
    {
        var result = await goalService.UpdateStatusAsync(CurrentUserId, id, request.Status, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await goalService.DeleteAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/plan-items")]
    public async Task<ActionResult<GoalDto>> AddPlanItem(Guid id, AddTrainingPlanItemRequest request, CancellationToken ct)
    {
        var result = await goalService.AddPlanItemAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/plan-items/{itemId:guid}")]
    public async Task<ActionResult<GoalDto>> RemovePlanItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var result = await goalService.RemovePlanItemAsync(CurrentUserId, id, itemId, ct);
        return FromResult(result);
    }
}
