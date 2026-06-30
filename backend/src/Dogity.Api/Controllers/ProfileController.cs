using Dogity.Api.Contracts;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Konto-Selbstverwaltung (Name/Avatar/E-Mail/Passwort). Nutzt wie
/// AuthController direkt UserManager statt eines Application-Service -
/// Kontoverwaltung ist Identity-Infrastruktur, kein fachlicher Use Case.
/// </summary>
[Route("api/profile")]
public class ProfileController(UserManager<ApplicationUser> userManager) : ApiControllerBase
{
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

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();

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

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
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

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return NoContent();
    }
}
