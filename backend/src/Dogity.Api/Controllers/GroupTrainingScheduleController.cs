using Dogity.Application.Community;
using Dogity.Domain.Community;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Terminplanung fürs Gruppentraining (siehe docs/GROUP_TRAINING_SCHEDULE.md).
/// Trainer-Routen sind ClubTrainer-gated (im Service geprüft); die
/// Mitglieder-Route liefert nur Termine der eigenen Gruppen.
/// </summary>
[Route("api/group-training/schedule")]
public class GroupTrainingScheduleController(IGroupTrainingScheduleService service) : ApiControllerBase
{
    [HttpGet("clubs/{clubId:guid}")]
    public async Task<ActionResult<IReadOnlyList<GroupTrainingSessionDto>>> GetClubSchedule(
        Guid clubId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? groupId,
        [FromQuery] GroupTrainingCategory? category,
        [FromQuery] bool mineOnly,
        CancellationToken ct)
    {
        var result = await service.GetClubScheduleAsync(CurrentUserId, clubId, from, to, groupId, category, mineOnly, ct);
        return FromResult(result);
    }

    /// <summary>Kommende Termine der eigenen Gruppen (Mitglieder-Sicht, read-only).</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<GroupTrainingSessionDto>>> GetMySchedule([FromQuery] DateOnly from, CancellationToken ct)
    {
        var result = await service.GetMemberScheduleAsync(CurrentUserId, from, ct);
        return FromResult(result);
    }

    [HttpPost("clubs/{clubId:guid}/sessions")]
    public async Task<ActionResult<GroupTrainingSessionDto>> Create(Guid clubId, CreateSessionRequest request, CancellationToken ct)
    {
        var result = await service.CreateSessionAsync(CurrentUserId, clubId, request, ct);
        return FromResult(result);
    }

    [HttpPost("clubs/{clubId:guid}/series")]
    public async Task<ActionResult<IReadOnlyList<GroupTrainingSessionDto>>> GenerateSeries(Guid clubId, GenerateSeriesRequest request, CancellationToken ct)
    {
        var result = await service.GenerateSeriesAsync(CurrentUserId, clubId, request, ct);
        return FromResult(result);
    }

    /// <summary>Mix-Generator: liefert einen Baustein-Entwurf für die Kategorie.</summary>
    [HttpGet("clubs/{clubId:guid}/generate-content")]
    public async Task<ActionResult<IReadOnlyList<GroupTrainingExerciseDto>>> GenerateContent(Guid clubId, [FromQuery] GroupTrainingCategory category, CancellationToken ct)
    {
        var result = await service.GenerateContentAsync(CurrentUserId, clubId, category, ct);
        return FromResult(result);
    }

    /// <summary>Vereinstrainer:innen für die Co-Trainer-Zuweisung.</summary>
    [HttpGet("clubs/{clubId:guid}/trainers")]
    public async Task<ActionResult<IReadOnlyList<SessionTrainerDto>>> GetClubTrainers(Guid clubId, CancellationToken ct)
    {
        var result = await service.GetClubTrainersAsync(CurrentUserId, clubId, ct);
        return FromResult(result);
    }

    [HttpPut("sessions/{id:guid}")]
    public async Task<ActionResult<GroupTrainingSessionDto>> Update(Guid id, UpdateSessionRequest request, CancellationToken ct)
    {
        var result = await service.UpdateSessionAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpPost("sessions/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await service.CancelSessionAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteSessionAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }
}
