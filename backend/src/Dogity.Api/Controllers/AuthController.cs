using System.Net;
using Dogity.Api.Contracts;
using Dogity.Application.Abstractions;
using Dogity.Application.Account;
using Dogity.Application.Notifications;
using Dogity.Application.Preferences;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Dogity.Api.Controllers;

[ApiController]
[Route("api/auth")]
// IP-Rate-Limit für alle anonymen Auth-Endpoints (siehe Program.cs):
// Identity-Lockout greift nur pro Konto, nicht gegen Passwort-Spraying,
// Massenregistrierung oder forgot-password-E-Mail-Spam.
[EnableRateLimiting("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenGenerator jwtTokenGenerator,
    IRefreshTokenService refreshTokens,
    IAccountMailService accountMails,
    IPreferenceService preferences,
    IFrontendLinks frontendLinks,
    TimeProvider timeProvider,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { errors = new[] { "E-Mail und Passwort sind erforderlich." } });

        // Namen haben keine Längengrenze in der Datenbank - ohne diese Prüfung
        // nähme die anonyme Registrierung Megabytes als "Vorname" an. Dieselbe Regel wie im Profil.
        var namensfehler = Personennamen.Pruefe(request.FirstName, request.LastName);
        if (namensfehler is not null)
            return BadRequest(new { errors = new[] { namensfehler } });

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Conflict(new { errors = new[] { "E-Mail wird bereits verwendet." } });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, Roles.User);

        // Konto und Einstellung entstehen nicht in einer Einheit. Scheitert das
        // Speichern der Abwahl, soll die Registrierung trotzdem gelingen: Ein 500
        // ließe das Konto zurück, der zweite Versuch endete mit 409. Die Abwahl
        // geht dann verloren - die Person stellt es unter Profil -> Funktionen
        // ein (die Willkommensmail unten trägt dafür ihren Abmelde-Link).
        // Nur "abgewählt" muss gespeichert werden: Ohne Einstellungszeile gilt "an".
        if (request.EmailBenachrichtigungen == false)
        {
            try
            {
                await preferences.UpdateEmailNotificationsAsync(user.Id, new UpdateEmailNotificationsRequest(false));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Die Abwahl der E-Mail-Benachrichtigungen konnte bei der Registrierung nicht gespeichert werden.");
            }
        }

        // Die Sprache der Oberfläche beim Registrieren gilt ab jetzt für Mails und Push.
        // VOR der Willkommensmail gespeichert, damit schon sie in dieser Sprache kommt.
        // Wie bei der Abwahl oben: Scheitert das Speichern, gelingt die Registrierung trotzdem -
        // die Person stellt die Sprache dann im Profil ein. Unbekanntes wird gar nicht erst gespeichert.
        var sprache = Sprachen.Normalisiere(request.Locale);
        try
        {
            sprache = await preferences.SetRegistrationLocaleAsync(user.Id, request.Locale) ?? sprache;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Die Sprache konnte bei der Registrierung nicht gespeichert werden.");
        }

        // Eingereiht, nicht gesendet: Der Versand läuft im Hintergrund, die
        // Registrierung wartet nie auf den Mailserver und scheitert nie an ihm.
        // Das gilt auch für das Erzeugen des Abmelde-Tokens (Schlüsselring).
        try
        {
            accountMails.SendeWillkommen(user.Id, user.Email!, sprache);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Die Willkommensmail konnte nicht eingereiht werden.");
        }

        return Ok(await BuildAuthResponseAsync(user, [Roles.User]));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { errors = new[] { "E-Mail oder Passwort ist falsch." } });

        // CheckPasswordSignInAsync (statt CheckPasswordAsync) statt: prüft
        // Lockout VOR dem Passwort, zählt bei falschem Passwort
        // AccessFailedCount hoch und sperrt automatisch nach
        // Lockout:MaxFailedAccessAttempts (siehe DependencyInjection.cs) -
        // ohne das blieb der eingebaute Brute-Force-Schutz von ASP.NET
        // Identity bisher wirkungslos, weil CheckPasswordAsync ihn nicht
        // anstößt.
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            // Zwei Sperren, zwei Meldungen: "gesperrt" klang auch nach fünf
            // falschen Passwörtern nach einer Entscheidung des Betreibers.
            return Unauthorized(new
            {
                errors = new[]
                {
                    Sperren.Bestimme(user.LockoutEnd, timeProvider.GetUtcNow()) == Sperrart.Dauerhaft
                        ? "Dieses Konto wurde gesperrt."
                        : "Zu viele Fehlversuche. Bitte versuche es in ein paar Minuten erneut.",
                },
            });
        if (!result.Succeeded)
            return Unauthorized(new { errors = new[] { "E-Mail oder Passwort ist falsch." } });

        var roles = await userManager.GetRolesAsync(user);
        return Ok(await BuildAuthResponseAsync(user, roles));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request)
    {
        // Rotation: der vorgelegte Refresh-Token wird entwertet und ein neuer
        // ausgestellt. Bei Wiedervorlage eines bereits verbrauchten Tokens
        // widerruft der Service alle Tokens des Nutzers (Reuse-Erkennung); bei
        // gelöschtem oder gesperrtem Konto verweigert er die Erneuerung.
        var rotation = await refreshTokens.RefreshAsync(request.RefreshToken);
        var user = rotation.Succeeded ? await userManager.FindByIdAsync(rotation.UserId.ToString()) : null;
        if (user is null)
            return Unauthorized(new { errors = new[] { "Sitzung abgelaufen. Bitte neu anmelden." } });
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenGenerator.GenerateToken(user.Id, user.Email!, roles.ToArray());
        return Ok(new AuthResponse(accessToken, rotation.NewRawToken!, user.Id, user.Email!, user.FirstName, user.LastName, roles.ToArray()));
    }

    [HttpPost("logout")]
    [EnableRateLimiting("refresh")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        // Nur den Refresh-Token dieses Geräts widerrufen - andere Sitzungen
        // (weitere Geräte) bleiben bestehen. Kein Auth nötig: der Besitz des
        // Tokens genügt, und ein ungültiger Token ist einfach ein No-Op.
        await refreshTokens.RevokeAsync(request.RefreshToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        // Antwort ist bewusst immer gleich (200, generische Meldung),
        // unabhängig davon ob die E-Mail existiert - verhindert, dass sich
        // über diesen Endpoint registrierte E-Mail-Adressen erraten lassen.
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = frontendLinks.Absolut($"/reset-password?email={WebUtility.UrlEncode(user.Email)}&token={WebUtility.UrlEncode(token)}");

            // Die Mail mit dem Link geht über die Warteschlange (Vorlage und
            // Versand siehe AccountMailService). Ist kein Mailversand eingerichtet,
            // bekommen stattdessen alle Admins eine In-App-Benachrichtigung und
            // setzen das Passwort in der Nutzerverwaltung selbst zurück - mit
            // Mailversand entfällt sie.
            await accountMails.SendePasswortZuruecksetzenAsync(
                new PasswortResetAnfrage(user.Id, user.Email!, user.FirstName, user.LastName, link),
                Roles.Admin);
        }

        return Ok(new { message = "Falls diese E-Mail-Adresse registriert ist, wurde ein Link zum Zurücksetzen verschickt." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return BadRequest(new { errors = new[] { "Link ist ungültig oder abgelaufen." } });

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Überall abmelden: Wer sein Passwort zurücksetzt, hat es vergessen -
        // oder jemand anderes kennt es. Sitzungen mit dem alten Passwort
        // sollen dann nicht weiterlaufen.
        await refreshTokens.RevokeAllForUserAsync(user.Id);

        return Ok(new { message = "Passwort wurde geändert." });
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(ApplicationUser user, IEnumerable<string> roles)
    {
        var roleArray = roles.ToArray();
        var token = jwtTokenGenerator.GenerateToken(user.Id, user.Email!, roleArray);
        var refreshToken = await refreshTokens.IssueAsync(user.Id);
        return new AuthResponse(token, refreshToken, user.Id, user.Email!, user.FirstName, user.LastName, roleArray);
    }
}
