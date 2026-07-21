using Dogity.Application.Stats;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[Route("api/stats")]
public class StatsController(IStatsService statsService) : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboard(CancellationToken ct)
    {
        var result = await statsService.GetDashboardAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpGet("dogs/{dogId:guid}/exercises")]
    public async Task<ActionResult<IReadOnlyList<DogExerciseStatDto>>> GetDogExerciseStats(Guid dogId, CancellationToken ct)
    {
        var result = await statsService.GetDogExerciseStatsAsync(CurrentUserId, dogId, ct);
        return FromResult(result);
    }
}
