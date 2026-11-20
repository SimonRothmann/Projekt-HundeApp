using Dogity.Application.Preferences;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Persönliche Einstellungen: Sprache, ausgeblendete Module, betriebene
/// Sportarten (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
/// </summary>
[ApiController]
[Authorize]
[Route("api/preferences")]
public class PreferencesController(IPreferenceService preferences, UserManager<ApplicationUser> userManager) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserPreferenceDto>> Get(CancellationToken ct) =>
        FromResult(await preferences.GetAsync(CurrentUserId, ct));

    [HttpPut("modules")]
    public async Task<IActionResult> UpdateModules(UpdateModulesRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateModulesAsync(CurrentUserId, request, ct));

    [HttpPut("sports")]
    public async Task<IActionResult> UpdateSports(UpdateSportsRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateSportsAsync(CurrentUserId, request, ct));

    [HttpPut("locale")]
    public async Task<IActionResult> UpdateLocale(UpdateLocaleRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateLocaleAsync(CurrentUserId, request, ct));

    /// <summary>
    /// Geltungsbereich der Prüfungsordnungen - eigener Endpunkt und nicht
    /// Teil der Sprache, weil beides unabhängig voneinander gewählt wird.
    /// </summary>
    [HttpPut("country")]
    public async Task<IActionResult> UpdateCountry(UpdateCountryRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateCountryAsync(CurrentUserId, request, ct));

    /// <summary>
    /// Schriftgröße der Oberfläche. Eigener Endpunkt wie Sprache und Land:
    /// drei unabhängige Einstellungen, die einzeln gespeichert werden.
    /// </summary>
    [HttpPut("font-scale")]
    public async Task<IActionResult> UpdateFontScale(UpdateFontScaleRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateFontScaleAsync(CurrentUserId, request, ct));

    /// <summary>
    /// "Wichtiges auch per E-Mail" (Einladungen, Entscheidungen über
    /// Beitrittsanfragen). Betrifft weder die Willkommens- noch die
    /// Passwort-Mail.
    /// </summary>
    [HttpPut("email-notifications")]
    public async Task<IActionResult> UpdateEmailNotifications(UpdateEmailNotificationsRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateEmailNotificationsAsync(CurrentUserId, request, ct));

    /// <summary>Farbsichere Darstellung (Ampel in Blau, Gelb, Rot) - Einstellung je Konto wie die Schriftgröße.</summary>
    [HttpPut("color-safe")]
    public async Task<IActionResult> UpdateColorSafe(UpdateColorSafeRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateColorSafeAsync(CurrentUserId, request, ct));

    /// <summary>Zeitzone des Geräts, vom Frontend nach der Anmeldung gemeldet - "heute" und Ruhezeiten rechnen danach.</summary>
    [HttpPut("time-zone")]
    public async Task<IActionResult> UpdateTimeZone(UpdateTimeZoneRequest request, CancellationToken ct)
    {
        // Ein Gastkonto bleibt in Vereinszeit: Sein Demo-Verein und die Beispieldaten sind darauf gerechnet.
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user?.GuestUntil is not null) return NoContent();

        return FromResult(await preferences.UpdateTimeZoneAsync(CurrentUserId, request, ct));
    }

    /// <summary>
    /// Schrittlänge für die Umrechnung der Fährtenlänge in Schritte. Eigener Endpunkt wie die übrigen
    /// unabhängigen Einstellungen.
    /// </summary>
    [HttpPut("step-length")]
    public async Task<IActionResult> UpdateStepLength(UpdateStepLengthRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateStepLengthAsync(CurrentUserId, request, ct));

    /// <summary>
    /// Erinnerungen (Termin am Vortag, Fährte, offene Rückmeldungen) und der Rhythmus der
    /// Zusammenfassung für Trainer:innen. Gilt immer für die angemeldete Person.
    /// </summary>
    [HttpPut("reminders")]
    public async Task<IActionResult> UpdateReminders(UpdateRemindersRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateRemindersAsync(CurrentUserId, request, ct));

    /// <summary>
    /// Die Sportarten, die für diesen Hund gelten - eigene Auswahl, sonst die
    /// des Menschen, sonst alle (leere Liste = keine Einschränkung).
    /// </summary>
    [HttpGet("dogs/{dogId:guid}/sports")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetDogSports(Guid dogId, CancellationToken ct) =>
        FromResult(await preferences.GetEffectiveDogSportsAsync(CurrentUserId, dogId, ct));

    [HttpPut("dogs/{dogId:guid}/sports")]
    public async Task<IActionResult> UpdateDogSports(Guid dogId, UpdateDogSportsRequest request, CancellationToken ct) =>
        FromResult(await preferences.UpdateDogSportsAsync(CurrentUserId, dogId, request, ct));
}
