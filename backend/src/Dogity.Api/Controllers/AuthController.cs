using Dogity.Api.Contracts;
using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenGenerator jwtTokenGenerator) : ControllerBase
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

        var roles = await userManager.GetRolesAsync(user);
        return Ok(BuildAuthResponse(user, roles));
    }

    private AuthResponse BuildAuthResponse(ApplicationUser user, IEnumerable<string> roles)
    {
        var roleArray = roles.ToArray();
        var token = jwtTokenGenerator.GenerateToken(user.Id, user.Email!, roleArray);
        return new AuthResponse(token, user.Id, user.Email!, user.FirstName, user.LastName, roleArray);
    }
}
