using System.Net;
using Dogity.Api.Contracts;
using Dogity.Application.Abstractions;
using Dogity.Application.Notifications;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

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
    IEmailSender emailSender,
    INotificationService notifications,
    IUserLookupService userLookup,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { errors = new[] { "E-Mail und Passwort sind erforderlich." } });

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Conflict(new { errors = new[] { "E-Mail wird bereits verwendet." } });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await userManager.AddToRoleAsync(user, Roles.User);

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
            return Unauthorized(new { errors = new[] { "Dieses Konto wurde gesperrt." } });
        if (!result.Succeeded)
            return Unauthorized(new { errors = new[] { "E-Mail oder Passwort ist falsch." } });

        var roles = await userManager.GetRolesAsync(user);
        return Ok(await BuildAuthResponseAsync(user, roles));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request)
    {
        // Rotation: der vorgelegte Refresh-Token wird entwertet und ein neuer
        // ausgestellt. Bei Wiedervorlage eines bereits verbrauchten Tokens
        // widerruft der Service alle Tokens des Nutzers (Reuse-Erkennung).
        var rotation = await refreshTokens.RotateAsync(request.RefreshToken);
        if (!rotation.Succeeded)
            return Unauthorized(new { errors = new[] { "Sitzung abgelaufen. Bitte neu anmelden." } });

        var user = await userManager.FindByIdAsync(rotation.UserId.ToString());
        // Nutzer inzwischen gesperrt/gelöscht: kein neuer Access-Token.
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            await refreshTokens.RevokeAllForUserAsync(rotation.UserId);
            return Unauthorized(new { errors = new[] { "Sitzung abgelaufen. Bitte neu anmelden." } });
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenGenerator.GenerateToken(user.Id, user.Email!, roles.ToArray());
        return Ok(new AuthResponse(accessToken, rotation.NewRawToken!, user.Id, user.Email!, user.FirstName, user.LastName, roles.ToArray()));
    }

    [HttpPost("logout")]
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
            var baseUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/');
            var link = $"{baseUrl}/reset-password?email={WebUtility.UrlEncode(user.Email)}&token={WebUtility.UrlEncode(token)}";

            await emailSender.SendAsync(
                user.Email!,
                "Dogity: Passwort zurücksetzen",
                $"Hallo {user.FirstName},\n\nüber diesen Link kannst du dein Passwort zurücksetzen:\n{link}\n\nWenn du das nicht angefordert hast, kannst du diese Mail ignorieren.");

            // Solange der E-Mail-Versand noch manuell läuft (kein SMTP-Relay),
            // bekommen zusätzlich alle Admins eine In-App-Benachrichtigung,
            // damit sie das Passwort direkt in der Nutzerverwaltung neu setzen
            // können. LinkPath zeigt auf die Admin-Seite mit dem betroffenen
            // Nutzer vorselektiert.
            var adminIds = await userLookup.ListUserIdsInRoleAsync(Roles.Admin);
            foreach (var adminId in adminIds)
            {
                await notifications.CreateAsync(
                    adminId,
                    $"{user.FirstName} {user.LastName} ({user.Email}) möchte das Passwort zurücksetzen.",
                    $"/admin?resetUser={user.Id}");
            }
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
