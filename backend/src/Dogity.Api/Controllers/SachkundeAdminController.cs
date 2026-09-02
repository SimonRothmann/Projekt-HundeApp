using Dogity.Application.Learning;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Verwaltung der Sachkunde-Fragenkataloge: ansehen und von Hand überarbeiten.
///
/// Getrennt vom <see cref="SachkundeController"/>, weil dort alles Lesende
/// bewusst ohne Anmeldung erreichbar ist - hier ist nichts davon öffentlich.
/// </summary>
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/sachkunde")]
public class SachkundeAdminController(ISachkundeAdminService verwaltung) : ApiControllerBase
{
    /// <param name="onlyFlagged">Nur Fragen mit auffälligen Textstellen.</param>
    [HttpGet("questions")]
    public async Task<ActionResult<IReadOnlyList<AdminQuizQuestionDto>>> GetQuestions(
        [FromQuery] string? catalog,
        [FromQuery] string? section,
        [FromQuery] string? search,
        [FromQuery] bool onlyEdited = false,
        [FromQuery] bool onlyFlagged = false,
        CancellationToken ct = default) =>
        FromResult(await verwaltung.GetQuestionsAsync(catalog, section, search, onlyEdited, onlyFlagged, ct));

    [HttpPut("questions/{id:guid}")]
    public async Task<ActionResult<AdminQuizQuestionDto>> Update(
        Guid id, UpdateQuizQuestionRequest request, CancellationToken ct) =>
        FromResult(await verwaltung.UpdateQuestionAsync(CurrentUserId, id, request, ct));

    /// <summary>
    /// Handbearbeitung zurücknehmen. Die Katalogfassung kommt beim nächsten
    /// Start der Anwendung wieder.
    /// </summary>
    [HttpPost("questions/{id:guid}/revert")]
    public async Task<ActionResult> Revert(Guid id, CancellationToken ct) =>
        FromResult(await verwaltung.RevertQuestionAsync(id, ct));
}
