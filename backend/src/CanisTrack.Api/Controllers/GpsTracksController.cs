using CanisTrack.Application.Tracking;
using Microsoft.AspNetCore.Mvc;

namespace CanisTrack.Api.Controllers;

[Route("api/gps-tracks")]
public class GpsTracksController(IGpsTrackService gpsTrackService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GpsTrackDto>>> GetByTrainingSession([FromQuery] Guid trainingSessionId, CancellationToken ct)
    {
        var result = await gpsTrackService.GetByTrainingSessionAsync(CurrentUserId, trainingSessionId, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<GpsTrackDto>> Create(CreateGpsTrackRequest request, CancellationToken ct)
    {
        var result = await gpsTrackService.CreateAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/walk-runs")]
    public async Task<ActionResult<GpsWalkRunDto>> AddWalkRun(Guid id, CreateGpsWalkRunRequest request, CancellationToken ct)
    {
        var result = await gpsTrackService.AddWalkRunAsync(CurrentUserId, id, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await gpsTrackService.DeleteAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }
}
