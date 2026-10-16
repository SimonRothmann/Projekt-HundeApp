using Dogity.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Api.Controllers;

/// <summary>
/// Abmelden von den E-Mails ohne Anmeldung - über den Link in der Mail. Es gibt
/// bewusst KEIN GET, das etwas ändert: Link-Scanner (Outlook SafeLinks, Virenscanner)
/// rufen Links aus Mails vorab auf und würden sonst alle abmelden.
/// </summary>
[Route("api/email-abmeldung")]
[AllowAnonymous]
[EnableRateLimiting("abmeldung")]
public class EmailAbmeldungController(IEmailAbmeldungService abmeldung) : ApiControllerBase
{
    /// <summary>Die Seite /email-abmelden: abmelden beim Laden, "Wieder anmelden" als Knopf.</summary>
    [HttpPost]
    public async Task<IActionResult> Setze(EmailAbmeldungRequest request, CancellationToken ct) =>
        FromResult(await abmeldung.SetzeAsync(request.Token, request.Aktiv, ct));

    /// <summary>
    /// Ein-Klick-Abmeldung im Mailprogramm (RFC 8058): Es schickt einen POST mit
    /// dem Formular-Body "List-Unsubscribe=One-Click" an die Adresse aus der
    /// Kopfzeile. Der Body wird weder verlangt noch ausgewertet. Die Antwort ist
    /// immer 200 - Mailprogramme werten sie nicht aus, und ein ungültiges Token
    /// soll sich nicht von einem gültigen unterscheiden lassen.
    /// </summary>
    [HttpPost("ein-klick")]
    public async Task<IActionResult> EinKlick([FromQuery(Name = "t")] string? token, CancellationToken ct)
    {
        await abmeldung.SetzeAsync(token, aktiv: false, ct);
        return Ok();
    }
}
