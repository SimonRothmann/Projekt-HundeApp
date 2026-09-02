using Dogity.Application.Onboarding;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Der geführte Erststart. Ein einzelner Aufruf statt fünf: Das Dashboard
/// müsste sonst Hunde, Ziele, Trainings, Vereins- und Gruppenmitgliedschaften
/// einzeln abfragen, nur um zu wissen, was als Nächstes dran ist.
/// </summary>
[Route("api/onboarding")]
public class OnboardingController(IOnboardingService onboarding) : ApiControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<OnboardingStatusDto>> GetStatus(CancellationToken ct) =>
        FromResult(await onboarding.GetStatusAsync(CurrentUserId, ct));

    [HttpPost("dismiss")]
    public async Task<ActionResult> Dismiss(CancellationToken ct) =>
        FromResult(await onboarding.DismissAsync(CurrentUserId, ct));
}
