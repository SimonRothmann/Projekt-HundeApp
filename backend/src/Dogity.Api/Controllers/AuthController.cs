using System.Net;
using Dogity.Api.Contracts;
using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Dogity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenGenerator jwtTokenGenerator,
    IEmailSender emailSender,
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

        return Ok(BuildAuthResponse(user, [Roles.User]));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { errors = new[] { "E-Mail oder Passwort ist falsch." } });

        if (await userManager.IsLockedOutAsync(user))
            return Unauthorized(new { errors = new[] { "Dieses Konto wurde gesperrt." } });

        var roles = await userManager.GetRolesAsync(user);
        return Ok(BuildAuthResponse(user, roles));
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

    private AuthResponse BuildAuthResponse(ApplicationUser user, IEnumerable<string> roles)
    {
        var roleArray = roles.ToArray();
        var token = jwtTokenGenerator.GenerateToken(user.Id, user.Email!, roleArray);
        return new AuthResponse(token, user.Id, user.Email!, user.FirstName, user.LastName, roleArray);
    }
}
