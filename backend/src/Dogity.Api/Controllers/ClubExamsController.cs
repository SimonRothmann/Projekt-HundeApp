using Dogity.Application.Community;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Prüfungstage des Vereins (Paket V4): Meldeliste, Prüfungsreife, Ergebnisse. Die Rechte prüft der Dienst
/// (Ausbildungswart:in, Verwaltung des eigenen Vereins); wer sie nicht hat, bekommt 404.
/// </summary>
public class ClubExamsController(IClubExamService service) : ApiControllerBase
{
    [HttpGet("api/clubs/{clubId:guid}/exams")]
    public async Task<ActionResult<IReadOnlyList<ClubExamListItemDto>>> GetList(Guid clubId, CancellationToken ct) =>
        FromResult(await service.GetListAsync(CurrentUserId, clubId, ct));

    [HttpPost("api/clubs/{clubId:guid}/exams")]
    public async Task<ActionResult<ClubExamDto>> Create(Guid clubId, SaveClubExamRequest request, CancellationToken ct) =>
        FromResult(await service.CreateAsync(CurrentUserId, clubId, request, ct));

    /// <summary>Die Mitglieder mit ihren Hunden - die Auswahl beim Eintragen eines Teams.</summary>
    [HttpGet("api/clubs/{clubId:guid}/exams/candidates")]
    public async Task<ActionResult<IReadOnlyList<ClubExamCandidateDto>>> GetCandidates(Guid clubId, CancellationToken ct) =>
        FromResult(await service.GetCandidatesAsync(CurrentUserId, clubId, ct));

    [HttpGet("api/clubs/{clubId:guid}/exams/{examId:guid}")]
    public async Task<ActionResult<ClubExamDto>> Get(Guid clubId, Guid examId, CancellationToken ct) =>
        FromResult(await service.GetAsync(CurrentUserId, clubId, examId, ct));

    [HttpPut("api/clubs/{clubId:guid}/exams/{examId:guid}")]
    public async Task<ActionResult<ClubExamDto>> Update(Guid clubId, Guid examId, SaveClubExamRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(CurrentUserId, clubId, examId, request, ct));

    [HttpPost("api/clubs/{clubId:guid}/exams/{examId:guid}/cancel")]
    public async Task<ActionResult> Cancel(Guid clubId, Guid examId, CancellationToken ct) =>
        FromResult(await service.CancelAsync(CurrentUserId, clubId, examId, ct));

    [HttpDelete("api/clubs/{clubId:guid}/exams/{examId:guid}")]
    public async Task<ActionResult> Delete(Guid clubId, Guid examId, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(CurrentUserId, clubId, examId, ct));

    [HttpPost("api/clubs/{clubId:guid}/exams/{examId:guid}/entries")]
    public async Task<ActionResult> AddEntry(Guid clubId, Guid examId, AddClubExamEntryRequest request, CancellationToken ct) =>
        FromResult(await service.AddEntryAsync(CurrentUserId, clubId, examId, request, ct));

    [HttpDelete("api/clubs/{clubId:guid}/exams/{examId:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult> RemoveEntry(Guid clubId, Guid examId, Guid entryId, CancellationToken ct) =>
        FromResult(await service.RemoveEntryAsync(CurrentUserId, clubId, examId, entryId, ct));

    [HttpPost("api/clubs/{clubId:guid}/exams/{examId:guid}/entries/{entryId:guid}/move")]
    public async Task<ActionResult> MoveEntry(Guid clubId, Guid examId, Guid entryId, MoveClubExamEntryRequest request, CancellationToken ct) =>
        FromResult(await service.MoveEntryAsync(CurrentUserId, clubId, examId, entryId, request.Direction, ct));

    [HttpPut("api/clubs/{clubId:guid}/exams/{examId:guid}/entries/{entryId:guid}/result")]
    public async Task<ActionResult> SetResult(Guid clubId, Guid examId, Guid entryId, SetClubExamResultRequest request, CancellationToken ct) =>
        FromResult(await service.SetResultAsync(CurrentUserId, clubId, examId, entryId, request, ct));

    /// <summary>Wo der Hund zu einem anstehenden Prüfungstag gemeldet ist - für die Hundeseite der Familie.</summary>
    [HttpGet("api/dogs/{dogId:guid}/club-exams")]
    public async Task<ActionResult<IReadOnlyList<DogExamRegistrationDto>>> GetForDog(Guid dogId, CancellationToken ct) =>
        FromResult(await service.GetForDogAsync(CurrentUserId, dogId, ct));
}
