using CanisTrack.Application.Training;
using Microsoft.AspNetCore.Mvc;

namespace CanisTrack.Api.Controllers;

[Route("api/trainings")]
public class TrainingController(ITrainingService trainingService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrainingSessionDto>>> GetByDog([FromQuery] Guid dogId, CancellationToken ct)
    {
        var result = await trainingService.GetByDogAsync(CurrentUserId, dogId, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingSessionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await trainingService.GetByIdAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<TrainingSessionDto>> Create(CreateTrainingSessionRequest request, CancellationToken ct)
    {
        var result = await trainingService.CreateAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await trainingService.DeleteAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }
}
