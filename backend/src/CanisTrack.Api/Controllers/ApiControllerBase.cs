using System.Security.Claims;
using CanisTrack.Application.Common;
using CanisTrack.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanisTrack.Api.Controllers;

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
    /// </summary>
    protected ActionResult<T> FromResult<T>(Result<T> result) =>
        result.Succeeded ? Ok(result.Value) : NotFound(new { errors = result.Errors });

    protected ActionResult FromResult(Result result) =>
        result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
}
