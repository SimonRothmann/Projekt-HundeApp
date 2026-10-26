using System.Security.Claims;
using Dogity.Application.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
            CurrentUserId, questionId, request.SelectedOptionIds, request.SelfAssessedCorrect,
            request.Assignments, ct));

    /// <summary>
    /// Zieht eine Übungsprüfung - ohne Lösungen. Anonym erlaubt, wie der Rest der
    /// Katalog-Endpunkte, aber gedrosselt: Jeder Aufruf lädt den ganzen Katalog.
    /// </summary>
    [HttpPost("catalogs/{code}/exam")]
    [AllowAnonymous]
    [EnableRateLimiting("pruefung")]
    public async Task<ActionResult<QuizExamDto>> StartExam(string code, CancellationToken ct) =>
        FromResult(await sachkunde.StartExamAsync(code, ct));

    /// <summary>
    /// Wertet die Übungsprüfung aus. Angemeldet wird das Ergebnis gespeichert,
    /// anonym nur gewertet.
    /// </summary>
    [HttpPost("catalogs/{code}/exam/submit")]
    [AllowAnonymous]
    [EnableRateLimiting("pruefung")]
    public async Task<ActionResult<QuizExamResultDto>> SubmitExam(
        string code, SubmitExamRequest request, CancellationToken ct) =>
        FromResult(await sachkunde.SubmitExamAsync(OptionalUserId, code, request.Answers ?? [], request.SubmissionId, ct));

    /// <summary>Die letzten Ergebnisse der Übungsprüfung des angemeldeten Nutzers.</summary>
    [HttpGet("catalogs/{code}/exams")]
    public async Task<ActionResult<QuizExamHistoryDto>> GetExams(string code, CancellationToken ct) =>
        FromResult(await sachkunde.GetExamHistoryAsync(CurrentUserId, code, ct));

    /// <summary>Von vorne anfangen.</summary>
    [HttpPost("catalogs/{code}/reset")]
    public async Task<ActionResult> Reset(string code, CancellationToken ct) =>
        FromResult(await sachkunde.ResetAsync(CurrentUserId, code, ct));
}

/// <param name="SelectedOptionIds">Angekreuzte Antworten bei Auswahlfragen.</param>
/// <param name="SelfAssessedCorrect">
/// Selbsteinschätzung bei Freitextfragen ("gewusst" / "nicht gewusst").
/// </param>
/// <param name="Assignments">
/// Bei Zuordnungsaufgaben: je Begriff der gewählte Schlüssel (Begriffs-Id → Schlüssel).
/// </param>
public record AnswerQuestionRequest(
    IReadOnlyList<Guid>? SelectedOptionIds,
    bool? SelfAssessedCorrect,
    IReadOnlyDictionary<Guid, string>? Assignments);

/// <param name="Answers">Eine Antwort je Frage der Prüfung; leere Auswahl = unbeantwortet.</param>
/// <param name="SubmissionId">Die Abgabe-Kennung aus dem Ziehen; fehlt bei alten Clients (dann wie bisher).</param>
public record SubmitExamRequest(IReadOnlyList<QuizExamAnswerDto>? Answers, Guid? SubmissionId = null);
