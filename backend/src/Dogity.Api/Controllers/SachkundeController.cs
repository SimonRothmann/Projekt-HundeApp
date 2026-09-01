using System.Security.Claims;
using Dogity.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Fragentrainer zur Sachkundeprüfung.
///
/// Katalog und Fragen sind ohne Anmeldung lesbar - es ist veröffentlichtes
/// Lernmaterial, und wer sich auf die Begleithundeprüfung vorbereitet, hat oft
/// noch gar keinen Zugang zur App. Alles, was einen Lernstand führt, verlangt
/// dagegen einen angemeldeten Nutzer.
/// </summary>
[Route("api/sachkunde")]
public class SachkundeController(ISachkundeService sachkunde) : ApiControllerBase
{
    private Guid? OptionalUserId =>
        User.Identity?.IsAuthenticated == true
            ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;

    [HttpGet("catalogs")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<QuizCatalogDto>>> GetCatalogs(CancellationToken ct) =>
        FromResult(await sachkunde.GetCatalogsAsync(ct));

    [HttpGet("catalogs/{code}/questions")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<QuizQuestionDto>>> GetQuestions(
        string code, [FromQuery] string? section, CancellationToken ct) =>
        FromResult(await sachkunde.GetQuestionsAsync(code, section, OptionalUserId, ct));

    /// <param name="mode">learn (Voreinstellung), mistakes oder all.</param>
    [HttpGet("catalogs/{code}/session")]
    public async Task<ActionResult<QuizSessionDto>> GetSession(
        string code, [FromQuery] string mode = "learn", [FromQuery] int limit = 20, CancellationToken ct = default) =>
        FromResult(await sachkunde.GetSessionAsync(CurrentUserId, code, mode, limit, ct));

    [HttpGet("catalogs/{code}/progress")]
    public async Task<ActionResult<QuizProgressDto>> GetProgress(string code, CancellationToken ct) =>
        FromResult(await sachkunde.GetProgressAsync(CurrentUserId, code, ct));

    [HttpPost("questions/{questionId:guid}/answer")]
    public async Task<ActionResult<QuizAnswerResultDto>> Answer(
        Guid questionId, AnswerQuestionRequest request, CancellationToken ct) =>
        FromResult(await sachkunde.SubmitAnswerAsync(
            CurrentUserId, questionId, request.SelectedOptionIds, request.SelfAssessedCorrect, ct));

    /// <summary>Von vorne anfangen.</summary>
    [HttpPost("catalogs/{code}/reset")]
    public async Task<ActionResult> Reset(string code, CancellationToken ct) =>
        FromResult(await sachkunde.ResetAsync(CurrentUserId, code, ct));
}

/// <param name="SelectedOptionIds">Angekreuzte Antworten bei Auswahlfragen.</param>
/// <param name="SelfAssessedCorrect">
/// Selbsteinschätzung bei Zuordnungs- und Freitextfragen ("gewusst" / "nicht gewusst").
/// </param>
public record AnswerQuestionRequest(IReadOnlyList<Guid>? SelectedOptionIds, bool? SelfAssessedCorrect);
