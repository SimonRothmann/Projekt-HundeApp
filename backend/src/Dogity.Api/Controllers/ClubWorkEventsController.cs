using Dogity.Application.Community;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Arbeitseinsätze des Vereins (Paket A1): Einsätze eintragen, Helfer:innen abhaken, Jahresauswertung. Die Rechte
/// prüft der Dienst (Verwaltung schreibt, Vorstand liest, jedes Mitglied sieht nur die eigenen); wer sie nicht hat,
/// bekommt 404.
/// </summary>
public class ClubWorkEventsController(IClubWorkService service) : ApiControllerBase
{
    [HttpGet("api/clubs/{clubId:guid}/work-events")]
    public async Task<ActionResult<IReadOnlyList<ClubWorkEventListItemDto>>> GetList(Guid clubId, CancellationToken ct) =>
        FromResult(await service.GetListAsync(CurrentUserId, clubId, ct));

    [HttpPost("api/clubs/{clubId:guid}/work-events")]
    public async Task<ActionResult<ClubWorkEventDto>> Create(Guid clubId, SaveClubWorkEventRequest request, CancellationToken ct) =>
        FromResult(await service.CreateAsync(CurrentUserId, clubId, request, ct));

    /// <summary>Die Personen, die sich abhaken lassen - die Auswahl beim Hinzufügen von Helfer:innen.</summary>
    [HttpGet("api/clubs/{clubId:guid}/work-events/candidates")]
    public async Task<ActionResult<IReadOnlyList<ClubWorkCandidateDto>>> GetCandidates(Guid clubId, CancellationToken ct) =>
        FromResult(await service.GetCandidatesAsync(CurrentUserId, clubId, ct));

    [HttpGet("api/clubs/{clubId:guid}/work-events/summary")]
    public async Task<ActionResult<ClubWorkSummaryDto>> GetSummary(Guid clubId, [FromQuery] int? year, CancellationToken ct) =>
        FromResult(await service.GetSummaryAsync(CurrentUserId, clubId, year, ct));

    /// <summary>Die eigenen Einsätze der anfragenden Person - für jedes Mitglied des Vereins.</summary>
    [HttpGet("api/clubs/{clubId:guid}/work-events/mine")]
    public async Task<ActionResult<ClubMyWorkDto>> GetMine(Guid clubId, CancellationToken ct) =>
        FromResult(await service.GetMineAsync(CurrentUserId, clubId, ct));

    [HttpGet("api/clubs/{clubId:guid}/work-events/{eventId:guid}")]
    public async Task<ActionResult<ClubWorkEventDto>> Get(Guid clubId, Guid eventId, CancellationToken ct) =>
        FromResult(await service.GetAsync(CurrentUserId, clubId, eventId, ct));

    [HttpPut("api/clubs/{clubId:guid}/work-events/{eventId:guid}")]
    public async Task<ActionResult<ClubWorkEventDto>> Update(Guid clubId, Guid eventId, SaveClubWorkEventRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(CurrentUserId, clubId, eventId, request, ct));

    [HttpDelete("api/clubs/{clubId:guid}/work-events/{eventId:guid}")]
    public async Task<ActionResult> Delete(Guid clubId, Guid eventId, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(CurrentUserId, clubId, eventId, ct));

    [HttpPost("api/clubs/{clubId:guid}/work-events/{eventId:guid}/participants")]
    public async Task<ActionResult> AddParticipants(Guid clubId, Guid eventId, AddClubWorkParticipantsRequest request, CancellationToken ct) =>
        FromResult(await service.AddParticipantsAsync(CurrentUserId, clubId, eventId, request, ct));

    [HttpPut("api/clubs/{clubId:guid}/work-events/{eventId:guid}/participants/{participationId:guid}")]
    public async Task<ActionResult> SetMinutes(Guid clubId, Guid eventId, Guid participationId, SetClubWorkMinutesRequest request, CancellationToken ct) =>
        FromResult(await service.SetMinutesAsync(CurrentUserId, clubId, eventId, participationId, request, ct));

    [HttpDelete("api/clubs/{clubId:guid}/work-events/{eventId:guid}/participants/{participationId:guid}")]
    public async Task<ActionResult> RemoveParticipant(Guid clubId, Guid eventId, Guid participationId, CancellationToken ct) =>
        FromResult(await service.RemoveParticipantAsync(CurrentUserId, clubId, eventId, participationId, ct));
}
