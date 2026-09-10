using Dogity.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Die Startseite in einem Aufruf statt einer Anfrage je Hund und Abschnitt
/// (siehe DashboardService).
/// </summary>
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) =>
        FromResult(await dashboard.GetAsync(CurrentUserId, ct));
}
