using Dogity.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Was dieser Server kann - für Seiten, die sich danach richten, auch ohne
/// Anmeldung (z. B. "Passwort vergessen": Mail oder Betreiber).
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController(INotificationChannels channels) : ControllerBase
{
    /// <summary>
    /// Nur Wahrheitswerte und der ÖFFENTLICHE VAPID-Schlüssel - keine
    /// Geheimnisse, keine Zugangsdaten, keine Adressen. Anonym, weil die Seite
    /// "Passwort vergessen" vor der Anmeldung danach fragt.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("features")]
    public ActionResult<SystemFeaturesDto> Features() =>
        Ok(new SystemFeaturesDto(channels.EmailAktiv, channels.PushPublicKey));
}
