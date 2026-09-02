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

    [HttpGet("dogs/{dogId:guid}/tracks")]
    public async Task<ActionResult<DogTrackStatsDto>> GetDogTrackStats(Guid dogId, CancellationToken ct)
    {
        var result = await statsService.GetDogTrackStatsAsync(CurrentUserId, dogId, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Verfassung gegen Bewertung, und was die Trainingsdichte damit macht.
    /// </summary>
    [HttpGet("dogs/{dogId:guid}/condition")]
    public async Task<ActionResult<DogConditionStatsDto>> GetDogCondition(Guid dogId, CancellationToken ct) =>
        FromResult(await statsService.GetDogConditionStatsAsync(CurrentUserId, dogId, ct));
}
