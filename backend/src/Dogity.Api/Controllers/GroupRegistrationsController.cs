using Dogity.Application.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Api.Controllers;

/// <summary>
/// Anmeldungen zu einer Gruppe (z. B. Welpengruppe): öffentliches Formular für
/// Menschen ohne Konto und die Verwaltung durch die Trainer:innen. Die Rechte
/// prüft der Service; wer die Gruppe nicht verwalten darf, bekommt überall 404.
/// </summary>
[Route("api/groups")]
public class GroupRegistrationsController(
    IGroupRegistrationService registrations,
    IGroupRegistrationFormService form) : ApiControllerBase
{
    /// <summary>
    /// Öffentlich: Vereins- und Gruppenname zum Anmeldecode, für die Seite
    /// hinter dem QR-Code. Dieselbe Drosselung je IP wie bei der Vereinseinladung.
    /// </summary>
    [HttpGet("registration/{code}")]
    [AllowAnonymous]
    [EnableRateLimiting("invite")]
    public async Task<ActionResult<GroupRegistrationFormDto>> GetForm(string code, CancellationToken ct) =>
        FromResult(await form.GetFormAsync(code, ct));

    /// <summary>
    /// Öffentlich: legt die Anmeldung an. Eigene, engere Drosselung je IP
    /// ("anmeldung"), damit sich das Formular nicht vollschreiben lässt.
    /// </summary>
    [HttpPost("registration/{code}")]
    [AllowAnonymous]
    [EnableRateLimiting("anmeldung")]
    public async Task<IActionResult> Submit(string code, PublicRegistrationRequest request, CancellationToken ct) =>
        FromResult(await form.SubmitAsync(code, request, ct));

    /// <summary>Der Anmeldecode der Gruppe, oder 204, wenn es keinen gibt.</summary>
    [HttpGet("{id:guid}/registration-link")]
    public async Task<ActionResult<GroupRegistrationLinkDto>> GetLink(Guid id, CancellationToken ct)
    {
        var result = await registrations.GetLinkAsync(CurrentUserId, id, ct);
        if (result.Succeeded && result.Value is null) return NoContent();
        return FromResult(result)!;
    }

    /// <summary>Neuen Code erzeugen. Der alte wird damit ungültig.</summary>
    [HttpPost("{id:guid}/registration-link")]
    public async Task<ActionResult<GroupRegistrationLinkDto>> CreateLink(Guid id, CancellationToken ct) =>
        FromResult(await registrations.RegenerateLinkAsync(CurrentUserId, id, ct));

    /// <summary>Anmeldeformular schließen.</summary>
    [HttpDelete("{id:guid}/registration-link")]
    public async Task<IActionResult> DisableLink(Guid id, CancellationToken ct) =>
        FromResult(await registrations.DisableLinkAsync(CurrentUserId, id, ct));

    [HttpGet("{id:guid}/registrations")]
    public async Task<ActionResult<IReadOnlyList<GroupRegistrationDto>>> List(Guid id, CancellationToken ct) =>
        FromResult(await registrations.ListAsync(CurrentUserId, id, ct));

    [HttpPost("{id:guid}/registrations")]
    public async Task<ActionResult<GroupRegistrationDto>> Create(Guid id, GroupRegistrationRequest request, CancellationToken ct) =>
        FromResult(await registrations.CreateAsync(CurrentUserId, id, request, ct));

    [HttpPut("{id:guid}/registrations/{registrationId:guid}")]
    public async Task<ActionResult<GroupRegistrationDto>> Update(Guid id, Guid registrationId, GroupRegistrationRequest request, CancellationToken ct) =>
        FromResult(await registrations.UpdateAsync(CurrentUserId, id, registrationId, request, ct));

    [HttpDelete("{id:guid}/registrations/{registrationId:guid}")]
    public async Task<IActionResult> Delete(Guid id, Guid registrationId, CancellationToken ct) =>
        FromResult(await registrations.DeleteAsync(CurrentUserId, id, registrationId, ct));

    [HttpPut("{id:guid}/registrations/{registrationId:guid}/paid")]
    public async Task<ActionResult<GroupRegistrationDto>> SetPaid(Guid id, Guid registrationId, SetRegistrationPaidRequest request, CancellationToken ct) =>
        FromResult(await registrations.SetPaidAsync(CurrentUserId, id, registrationId, request.Paid, ct));

    /// <summary>Tage mit Termin der Gruppe (letzte vier bis nächste zwei Wochen) für die Tagesauswahl.</summary>
    [HttpGet("{id:guid}/registrations/days")]
    public async Task<ActionResult<IReadOnlyList<AttendanceDayDto>>> GetDays(Guid id, CancellationToken ct) =>
        FromResult(await registrations.GetAttendanceDaysAsync(CurrentUserId, id, ct));

    /// <summary>Die Anmeldungen, die an einem Tag (yyyy-MM-dd) abgehakt sind.</summary>
    [HttpGet("{id:guid}/registrations/attendance")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetAttendance(Guid id, [FromQuery] DateOnly date, CancellationToken ct) =>
        FromResult(await registrations.GetAttendanceAsync(CurrentUserId, id, date, ct));

    [HttpPut("{id:guid}/registrations/{registrationId:guid}/attendance")]
    public async Task<IActionResult> SetAttendance(Guid id, Guid registrationId, SetRegistrationAttendanceRequest request, CancellationToken ct) =>
        FromResult(await registrations.SetAttendanceAsync(CurrentUserId, id, registrationId, request.Date, request.Present, ct));

    /// <summary>Übernimmt Zeilen aus dem CSV einer Google-Formular-Liste (höchstens 500).</summary>
    [HttpPost("{id:guid}/registrations/import")]
    public async Task<ActionResult<ImportRegistrationsResultDto>> Import(Guid id, ImportRegistrationsRequest request, CancellationToken ct) =>
        FromResult(await registrations.ImportAsync(CurrentUserId, id, request, ct));
}
