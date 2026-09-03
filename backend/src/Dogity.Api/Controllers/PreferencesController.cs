using Dogity.Application.Preferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Persönliche Einstellungen: Sprache, ausgeblendete Module, betriebene
/// Sportarten (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
/// </summary>
[ApiController]
[Authorize]
[Route("api/preferences")]
public class PreferencesController(IPreferenceService preferences) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserPreferenceDto>> Get(CancellationToken ct) =>
        FromResult(await preferences.GetAsync(CurrentUserId, ct));

    [HttpPut("modules")]
    public async Task<IActionResult> UpdateModules(UpdateModulesRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateModulesAsync(CurrentUserId, request, ct));

    [HttpPut("sports")]
    public async Task<IActionResult> UpdateSports(UpdateSportsRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateSportsAsync(CurrentUserId, request, ct));

    [HttpPut("locale")]
    public async Task<IActionResult> UpdateLocale(UpdateLocaleRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateLocaleAsync(CurrentUserId, request, ct));

    /// <summary>
    /// Die Sportarten, die für diesen Hund gelten - eigene Auswahl, sonst die
    /// des Menschen, sonst alle (leere Liste = keine Einschränkung).
    /// </summary>
    [HttpGet("dogs/{dogId:guid}/sports")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetDogSports(Guid dogId, CancellationToken ct) =>
        FromResult(await preferences.GetEffectiveDogSportsAsync(CurrentUserId, dogId, ct));

    [HttpPut("dogs/{dogId:guid}/sports")]
    public async Task<IActionResult> UpdateDogSports(Guid dogId, UpdateDogSportsRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateDogSportsAsync(CurrentUserId, dogId, request, ct));
}
