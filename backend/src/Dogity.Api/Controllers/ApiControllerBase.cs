using System.Security.Claims;
using Dogity.Application.Common;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new InvalidOperationException("Kein authentifizierter Benutzer im Kontext."));

    protected bool IsAdmin => User.IsInRole(Roles.Admin);

    /// <summary>
    /// Mappt ein Application-<see cref="Result{T}"/> auf eine passende HTTP-Antwort
    /// (siehe CODING_GUIDELINES.md "Fehlerbehandlung: immer strukturierte Fehler").
    ///
    /// 404 nur, wenn der Use Case wirklich "gibt es nicht" meint
    /// (<see cref="Result.IsNotFound"/>), sonst 400. Vorher wurde JEDER
    /// Fehlschlag eines Result&lt;T&gt; zu 404 - auch eine Eingabeprüfung wie
    /// "Bewertung muss zwischen 1 und 5 liegen", was für jeden Aufrufer
    /// (auch für uns selbst beim Debuggen) irreführend war.
    /// </summary>
    protected ActionResult<T> FromResult<T>(Result<T> result) =>
        result.Succeeded
            ? Ok(result.Value)
            : result.IsNotFound
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });

    protected ActionResult FromResult(Result result) =>
        result.Succeeded
            ? NoContent()
            : result.IsNotFound
                ? NotFound(new { errors = result.Errors })
                : BadRequest(new { errors = result.Errors });
}
