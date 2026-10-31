using Dogity.Application.Sports;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Lesender Zugriff auf den Sportarten-Katalog. Sport-/Prüfungsordnungs-
/// Stammdaten sind öffentlich (kein personenbezogener Bezug). Die
/// Übungsliste benötigt dagegen einen authentifizierten Nutzer, da sie
/// vereinsspezifische Übungen (Exercise.ClubId) nutzerabhängig einblendet
/// (siehe ClubAccessQueries). Angemeldet ist die Voreinstellung (ApiControllerBase);
/// die öffentlichen Lese-Aktionen sind einzeln mit AllowAnonymous gekennzeichnet.
/// </summary>
[Route("api/sports")]
public class SportsController(ISportCatalogService catalogService, IRegulationManagementService regulationManagement) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SportDto>>> GetSports([FromQuery] bool alle, CancellationToken ct)
    {
        // Vereinsspezifische Sportarten (Sport.ClubId != null) sind nur für
        // eingeloggte Nutzer sichtbar, die im entsprechenden Verein Trainer
        // oder aktives Mitglied sind. Anonyme Aufrufer sehen nur globale.
        // Angemeldete bekommen nur die Sportarten ihres Landes (siehe
        // GetSportsAsync); "alle" hebt das auf, für die Katalogpflege. Das ist
        // keine Zugriffsbeschränkung - Sportarten und Ordnungen sind öffentlich.
        Guid? userId = User.Identity?.IsAuthenticated == true ? CurrentUserId : null;
        var result = await catalogService.GetSportsAsync(userId, alle, ct);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<SportDto>> CreateSport(CreateSportRequest request, CancellationToken ct)
    {
        var result = await catalogService.CreateSportAsync(CurrentUserId, IsAdmin, request, ct);
        return FromResult(result);
    }

    [HttpGet("{sportId:guid}/exercises")]
    public async Task<ActionResult<IReadOnlyList<ExerciseDto>>> GetExercises(
        Guid sportId, [FromQuery] string? katalog, [FromQuery] Guid? clubId, CancellationToken ct)
    {
        // Ohne "katalog" alles Sichtbare (z. B. Trainingsformular); "global"
        // bzw. "verein" (mit clubId) schränken auf einen einzelnen Katalog ein.
        ExerciseScope? scope;
        switch (katalog)
        {
            case null:
                scope = null;
                break;
            case "global":
                scope = ExerciseScope.Global;
                break;
            case "verein" when clubId is { } id:
                scope = ExerciseScope.ForClub(id);
                break;
            default:
                return BadRequest(new { errors = new[] { "Ungültiger Katalog: erlaubt sind \"global\" oder \"verein\" mit clubId." } });
        }

        var result = await catalogService.GetExercisesAsync(sportId, CurrentUserId, scope, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Die wählbaren Geltungsbereiche. Anonym erreichbar, weil die
    /// öffentliche Prüfungsordnungsseite sie ebenfalls braucht.
    /// </summary>
    [HttpGet("countries")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CountryDto>>> GetCountries(CancellationToken ct)
    {
        var result = await catalogService.GetCountriesAsync(ct);
        return Ok(result.Value);
    }

    /// <param name="country">
    /// Optionales Länderkürzel. Ohne Angabe kommen alle Ordnungen - der
    /// Katalogpflege dient das, nicht der Anzeige.
    /// </param>
    [HttpGet("{sportId:guid}/regulations")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RegulationDto>>> GetRegulations(Guid sportId, [FromQuery] string? country, CancellationToken ct)
    {
        var result = await catalogService.GetRegulationsAsync(sportId, country, ct);
        return FromResult(result);
    }

    [HttpGet("regulations/{regulationId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RegulationDetailDto>> GetRegulationDetail(Guid regulationId, CancellationToken ct)
    {
        var result = await catalogService.GetRegulationDetailAsync(regulationId, ct);
        // 404 bei jedem Fehlschlag, auch bei "Keine gültige Version" (ein Failure): so war die Antwort immer.
        if (!result.Succeeded)
            return NotFound(new { errors = result.Errors });

        return Ok(result.Value);
    }

    // ---- Bearbeiten (Admin global, Vereinstrainer für Vereins-Sportarten) ----

    [HttpPut("{sportId:guid}")]
    public async Task<ActionResult<SportDto>> UpdateSport(Guid sportId, UpdateSportRequest request, CancellationToken ct)
    {
        var result = await regulationManagement.UpdateSportAsync(CurrentUserId, IsAdmin, sportId, request, ct);
        return FromResultAlsBadRequest(result);
    }

    [HttpPut("regulations/{regulationId:guid}")]
    public async Task<ActionResult<RegulationDto>> UpdateRegulation(Guid regulationId, UpdateRegulationRequest request, CancellationToken ct)
    {
        var result = await regulationManagement.UpdateRegulationAsync(CurrentUserId, IsAdmin, regulationId, request, ct);
        return FromResultAlsBadRequest(result);
    }

    [HttpPost("regulations/{regulationId:guid}/exercises")]
    public async Task<ActionResult<RegulationExerciseDto>> AddRegulationExercise(Guid regulationId, AddRegulationExerciseRequest request, CancellationToken ct)
    {
        var result = await regulationManagement.AddRegulationExerciseAsync(CurrentUserId, IsAdmin, regulationId, request, ct);
        return FromResultAlsBadRequest(result);
    }

    [HttpPut("regulations/{regulationId:guid}/exercises/{exerciseId:guid}")]
    public async Task<IActionResult> UpdateRegulationExercise(Guid regulationId, Guid exerciseId, UpdateRegulationExerciseRequest request, CancellationToken ct)
    {
        var result = await regulationManagement.UpdateRegulationExerciseAsync(CurrentUserId, IsAdmin, regulationId, exerciseId, request, ct);
        return FromResultAlsBadRequest(result);
    }

    [HttpDelete("regulations/{regulationId:guid}/exercises/{exerciseId:guid}")]
    public async Task<IActionResult> RemoveRegulationExercise(Guid regulationId, Guid exerciseId, CancellationToken ct)
    {
        var result = await regulationManagement.RemoveRegulationExerciseAsync(CurrentUserId, IsAdmin, regulationId, exerciseId, ct);
        return FromResultAlsBadRequest(result);
    }

    // ---- Handänderung zurücknehmen (nur Admin; Vereinsordnungen kennt der Katalog nicht) ----

    /// <summary>
    /// Nimmt die Handänderung einer globalen Prüfungsordnung zurück. Die
    /// Katalogfassung kommt beim nächsten Start der Anwendung wieder.
    /// </summary>
    [HttpPost("regulations/{regulationId:guid}/revert")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RevertRegulation(Guid regulationId, CancellationToken ct)
    {
        var result = await regulationManagement.RevertRegulationAsync(CurrentUserId, IsAdmin, regulationId, ct);
        return FromResultAlsBadRequest(result);
    }

    /// <summary>Wie <see cref="RevertRegulation"/> für eine Übungszuordnung der Prüfungsordnung.</summary>
    [HttpPost("regulations/{regulationId:guid}/exercises/{exerciseId:guid}/revert")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RevertRegulationExercise(Guid regulationId, Guid exerciseId, CancellationToken ct)
    {
        var result = await regulationManagement.RevertRegulationExerciseAsync(CurrentUserId, IsAdmin, regulationId, exerciseId, ct);
        return FromResultAlsBadRequest(result);
    }
}
