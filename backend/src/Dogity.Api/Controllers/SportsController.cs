using System.Security.Claims;
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
/// (siehe ClubAccessQueries).
/// </summary>
[ApiController]
[Route("api/sports")]
public class SportsController(ISportCatalogService catalogService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SportDto>>> GetSports(CancellationToken ct)
    {
        // Vereinsspezifische Sportarten (Sport.ClubId != null) sind nur für
        // eingeloggte Nutzer sichtbar, die im entsprechenden Verein Trainer
        // oder aktives Mitglied sind. Anonyme Aufrufer sehen nur globale.
        Guid? userId = User.Identity?.IsAuthenticated == true
            ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        var result = await catalogService.GetSportsAsync(userId, ct);
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<SportDto>> CreateSport(CreateSportRequest request, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole(Roles.Admin);
        var result = await catalogService.CreateSportAsync(userId, isAdmin, request, ct);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors });
        return Ok(result.Value);
    }

    [HttpGet("{sportId:guid}/exercises")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ExerciseDto>>> GetExercises(Guid sportId, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await catalogService.GetExercisesAsync(sportId, userId, ct);
        if (!result.Succeeded)
            return NotFound(new { errors = result.Errors });

        return Ok(result.Value);
    }

    [HttpGet("{sportId:guid}/regulations")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<RegulationDto>>> GetRegulations(Guid sportId, CancellationToken ct)
    {
        var result = await catalogService.GetRegulationsAsync(sportId, ct);
        if (!result.Succeeded)
            return NotFound(new { errors = result.Errors });

        return Ok(result.Value);
    }

    [HttpGet("regulations/{regulationId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RegulationDetailDto>> GetRegulationDetail(Guid regulationId, CancellationToken ct)
    {
        var result = await catalogService.GetRegulationDetailAsync(regulationId, ct);
        if (!result.Succeeded)
            return NotFound(new { errors = result.Errors });

        return Ok(result.Value);
    }
}
