using Dogity.Application.Betrieb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Api.Controllers;

/// <summary>
/// Der öffentliche Abruf des Wartungshinweises. Setzen und Ausschalten liegen im
/// AdminController.
/// </summary>
[Route("api/wartungshinweis")]
public class WartungshinweisController(IWartungshinweisService wartung) : ApiControllerBase
{
    /// <summary>
    /// Der Hinweis, wenn einer anzuzeigen ist - sonst 204. Ohne Anmeldung, weil ihn auch
    /// Startseite und Anmeldung zeigen; jede Seite der App fragt ihn beim Laden ab, darum
    /// kurz zwischengespeichert (Server und Browser) und im weiten Topf der Token-Erneuerung
    /// gedrosselt, der "nur die Last begrenzt".
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<WartungshinweisDto?>> Get(CancellationToken ct)
    {
        Response.Headers.CacheControl = $"public, max-age={(int)WartungshinweisService.Zwischenspeicher.TotalSeconds}";
        var result = await wartung.GetAktuellenAsync(ct);
        return result is { Succeeded: true, Value: null } ? NoContent() : FromResult(result);
    }
}
