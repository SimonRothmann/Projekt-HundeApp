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

    [HttpPut("{id:guid}/config")]
    public async Task<ActionResult<GoalDto>> UpdateConfig(Guid id, UpdateGoalConfigRequest request, CancellationToken ct)
    {
        var result = await goalService.UpdateConfigAsync(CurrentUserId, id, request.WeeklyExerciseCount, request.TrainingDaysPerWeek, ct);
        return FromResult(result);
    }

    [HttpPut("{id:guid}/weeks/{week:int}/config")]
    public async Task<ActionResult<GoalDto>> UpdateWeekConfig(Guid id, int week, UpdateWeekConfigRequest request, CancellationToken ct)
    {
        var result = await goalService.UpdateWeekConfigAsync(CurrentUserId, id, week, request.TrainingDaysPerWeek, ct);
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

    [HttpPut("{id:guid}/plan-items/{itemId:guid}")]
    public async Task<ActionResult<GoalDto>> UpdatePlanItem(Guid id, Guid itemId, UpdateTrainingPlanItemRequest request, CancellationToken ct)
    {
        var result = await goalService.UpdatePlanItemAsync(CurrentUserId, id, itemId, request, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/plan-items/{itemId:guid}")]
    public async Task<ActionResult<GoalDto>> RemovePlanItem(Guid id, Guid itemId, CancellationToken ct)
    {
        var result = await goalService.RemovePlanItemAsync(CurrentUserId, id, itemId, ct);
        return FromResult(result);
    }

    [HttpPut("{id:guid}/regenerate-week")]
    public async Task<ActionResult<GoalDto>> RegenerateWeek(Guid id, RegenerateWeekRequest request, CancellationToken ct)
    {
        var result = await goalService.RegenerateWeekAsync(CurrentUserId, id, request.WeekNumber, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/weightable-exercises")]
    public async Task<ActionResult<IReadOnlyList<WeightableExerciseDto>>> GetWeightableExercises(Guid id, CancellationToken ct)
    {
        var result = await goalService.GetWeightableExercisesAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPut("{id:guid}/exercises/{exerciseId:guid}/priority")]
    public async Task<IActionResult> SetExercisePriority(Guid id, Guid exerciseId, SetExercisePriorityRequest request, CancellationToken ct)
    {
        var result = await goalService.SetExercisePriorityAsync(CurrentUserId, id, exerciseId, request.Value, ct);
        return FromResult(result);
    }
}
