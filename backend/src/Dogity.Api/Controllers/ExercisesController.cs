using Dogity.Application.Sports;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Schreibender Zugriff auf den Übungskatalog. Admins legen globale
/// Übungen an (ClubId = null), Vereinstrainer ausschließlich
/// vereinsspezifische Übungen ihres Vereins - die Berechtigungsprüfung
/// erfolgt im Service (siehe ExerciseManagementService), da dieselbe
/// Route je nach ClubId unterschiedliche Rollen erfordert.
/// </summary>
[Route("api/exercises")]
public class ExercisesController(
    IExerciseManagementService exerciseService,
    ISportCatalogService catalogService) : ApiControllerBase
{
    [HttpGet("uncategorized")]
    public async Task<ActionResult<IReadOnlyList<ExerciseDto>>> GetUncategorized(CancellationToken ct)
    {
        var result = await catalogService.GetUncategorizedExercisesAsync(CurrentUserId, ct);
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<ExerciseDto>> Create(CreateExerciseRequest request, CancellationToken ct)
    {
        var result = await exerciseService.CreateAsync(CurrentUserId, IsAdmin, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExerciseDto>> Update(Guid id, UpdateExerciseRequest request, CancellationToken ct)
    {
        var result = await exerciseService.UpdateAsync(CurrentUserId, IsAdmin, id, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await exerciseService.DeleteAsync(CurrentUserId, IsAdmin, id, ct);
        return FromResult(result);
    }
}
