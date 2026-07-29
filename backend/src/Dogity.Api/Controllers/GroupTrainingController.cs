using Dogity.Application.Community;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Vereins-Trainingsbibliothek: verein-weit geteilte Bausteine + Einheiten
/// (siehe docs/GROUP_TRAINING_LIBRARY.md). Alle Routen setzen ClubTrainer-
/// Berechtigung voraus (im Service geprüft).
/// </summary>
[Route("api/group-training")]
public class GroupTrainingController(IGroupTrainingService service) : ApiControllerBase
{
    [HttpGet("clubs/{clubId:guid}/library")]
    public async Task<ActionResult<GroupTrainingLibraryDto>> GetLibrary(Guid clubId, CancellationToken ct)
    {
        var result = await service.GetLibraryAsync(CurrentUserId, clubId, ct);
        return FromResult(result);
    }

    // ---- Bausteine ----

    [HttpPost("clubs/{clubId:guid}/exercises")]
    public async Task<ActionResult<GroupTrainingExerciseDto>> CreateExercise(Guid clubId, UpsertExerciseRequest request, CancellationToken ct)
    {
        var result = await service.CreateExerciseAsync(CurrentUserId, clubId, request, ct);
        return FromResult(result);
    }

    [HttpPut("exercises/{id:guid}")]
    public async Task<ActionResult<GroupTrainingExerciseDto>> UpdateExercise(Guid id, UpsertExerciseRequest request, CancellationToken ct)
    {
        var result = await service.UpdateExerciseAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("exercises/{id:guid}")]
    public async Task<IActionResult> DeleteExercise(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteExerciseAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    // ---- Einheiten ----

    [HttpPost("clubs/{clubId:guid}/units")]
    public async Task<ActionResult<GroupTrainingUnitDto>> CreateUnit(Guid clubId, UpsertUnitRequest request, CancellationToken ct)
    {
        var result = await service.CreateUnitAsync(CurrentUserId, clubId, request, ct);
        return FromResult(result);
    }

    [HttpPut("units/{id:guid}")]
    public async Task<ActionResult<GroupTrainingUnitDto>> UpdateUnit(Guid id, UpsertUnitRequest request, CancellationToken ct)
    {
        var result = await service.UpdateUnitAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("units/{id:guid}")]
    public async Task<IActionResult> DeleteUnit(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteUnitAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost("units/{id:guid}/duplicate")]
    public async Task<ActionResult<GroupTrainingUnitDto>> DuplicateUnit(Guid id, CancellationToken ct)
    {
        var result = await service.DuplicateUnitAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }
}
