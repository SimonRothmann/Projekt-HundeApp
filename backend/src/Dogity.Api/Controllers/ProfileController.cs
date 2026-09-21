using Dogity.Api.Contracts;
using Dogity.Application.Abstractions;
using Dogity.Application.Account;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Konto-Selbstverwaltung (Name/Avatar/E-Mail/Passwort). Nutzt wie
/// AuthController direkt UserManager statt eines Application-Service -
/// Kontoverwaltung ist Identity-Infrastruktur, kein fachlicher Use Case.
///
/// Auskunft und Löschung (Art. 15, 17, 20 DSGVO) liegen dagegen in
/// <see cref="IAccountDataService"/>: Sie berühren zwei Dutzend fachliche
/// Tabellen und gehören damit nicht neben den Passwortwechsel.
/// </summary>
[Route("api/profile")]
public class ProfileController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAccountDataService accountData,
    IRefreshTokenService refreshTokens) : ApiControllerBase
{
    /// <summary>
    /// Prüft das aktuelle Passwort MIT Fehlversuchszähler - wie beim Anmelden.
    ///
    /// Vorher prüften E-Mail-Wechsel, Passwortwechsel und Kontolöschung ohne
    /// Zähler und ohne Drosselung. Wer einen Access-Token erbeutet hatte,
    /// konnte darüber beliebig viele Passwörter durchprobieren und sich mit dem
    /// gefundenen dauerhaft festsetzen; die Sperre nach fünf Fehlversuchen
    /// griff nur an der Anmeldung.
    /// </summary>
    private async Task<bool> AktuellesPasswortStimmtAsync(ApplicationUser user, string? password) =>
        !string.IsNullOrEmpty(password)
        && (await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded;

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user is null) return NotFound();

        return Ok(new ProfileDto(user.FirstName, user.LastName, user.Email!, user.AvatarUrl));
    }

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest(new { errors = new[] { "Vor- und Nachname sind erforderlich." } });

        // Die Adresse landet als Bildquelle im Browser anderer Seiten der App.
        // Nur https: kein unverschlüsseltes Nachladen (Mixed Content), kein
        // javascript:/data:-Unfug, und eine Länge, die in eine Zeile passt.
        var avatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        if (avatarUrl is not null
            && (avatarUrl.Length > 2048
                || !Uri.TryCreate(avatarUrl, UriKind.Absolute, out var avatarUri)
                || avatarUri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { errors = new[] { "Die Bild-Adresse muss mit https:// beginnen." } });

        if (request.FirstName.Trim().Length > 100 || request.LastName.Trim().Length > 100)
            return BadRequest(new { errors = new[] { "Vor- und Nachname dürfen höchstens 100 Zeichen lang sein." } });

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.AvatarUrl = avatarUrl;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new ProfileDto(user.FirstName, user.LastName, user.Email!, user.AvatarUrl));
    }

    [HttpPut("email")]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user is null) return NotFound();

        if (!await AktuellesPasswortStimmtAsync(user, request.CurrentPassword))
            return BadRequest(new { errors = new[] { "Aktuelles Passwort ist falsch." } });

        var existing = await userManager.FindByEmailAsync(request.NewEmail);
        if (existing is not null && existing.Id != user.Id)
            return Conflict(new { errors = new[] { "E-Mail wird bereits verwendet." } });

        // UserName folgt der Email-Konvention aus AuthController.Register -
        // beide müssen synchron bleiben, sonst schlägt der nächste Login fehl.
        var emailResult = await userManager.SetEmailAsync(user, request.NewEmail);
        if (!emailResult.Succeeded)
            return BadRequest(new { errors = emailResult.Errors.Select(e => e.Description) });

        var userNameResult = await userManager.SetUserNameAsync(user, request.NewEmail);
        if (!userNameResult.Succeeded)
            return BadRequest(new { errors = userNameResult.Errors.Select(e => e.Description) });

        return NoContent();
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user is null) return NotFound();

        if (!await AktuellesPasswortStimmtAsync(user, request.CurrentPassword))
            return BadRequest(new { errors = new[] { "Aktuelles Passwort ist falsch." } });

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Alle anderen Geräte abmelden. Wer das Passwort wechselt, weil es
        // jemand mitbekommen hat, will genau das - und sonst schadet es nicht.
        await refreshTokens.RevokeAllExceptAsync(user.Id, request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>
    /// Auskunft und Datenübertragbarkeit (Art. 15, 20 DSGVO) - alles, was zu
    /// diesem Konto gespeichert ist, in einer Antwort.
    ///
    /// Bewusst ohne Seitenaufteilung: Eine Auskunft, die man sich in
    /// zwanzig Stücken zusammensuchen muss, ist keine.
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult<AccountExportDto>> Export(CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user is null) return NotFound();

        var result = await accountData.ExportAsync(CurrentUserId, ct);
        if (!result.Succeeded) return FromResult(result);

        // Die Avatar-Adresse hängt am Identity-Konto, das die
        // Application-Schicht bewusst nicht kennt - sie kommt erst hier dazu.
        return Ok(result.Value! with { Konto = result.Value.Konto with { AvatarUrl = user.AvatarUrl } });
    }

    /// <summary>
    /// Löschung des eigenen Kontos (Art. 17 DSGVO).
    ///
    /// Reihenfolge wie bei der Admin-Löschung: erst die Fachdaten, dann das
    /// Konto. Andersherum bliebe kein Weg mehr, die Zeilen zu finden - sie
    /// verweisen nur über die UserId auf das Konto, ohne Fremdschlüssel.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(CurrentUserId.ToString());
        if (user is null) return NotFound();

        if (!await AktuellesPasswortStimmtAsync(user, request.CurrentPassword))
            return BadRequest(new { errors = new[] { "Aktuelles Passwort ist falsch." } });

        var purge = await accountData.PurgeAsync(CurrentUserId, ct);
        if (!purge.Succeeded) return FromResult(purge);

        var deleted = await userManager.DeleteAsync(user);
        if (!deleted.Succeeded)
            return BadRequest(new { errors = deleted.Errors.Select(e => e.Description) });

        await refreshTokens.RevokeAllForUserAsync(CurrentUserId, ct);
        return NoContent();
    }
}
