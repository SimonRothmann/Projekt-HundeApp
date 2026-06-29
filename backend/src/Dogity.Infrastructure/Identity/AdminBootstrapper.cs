using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Vergibt die ADMIN-Rolle einmalig an die in der Konfiguration
/// ("AdminBootstrap:Email") hinterlegte Adresse, falls der Benutzer
/// existiert und die Rolle noch nicht hat. Es gibt bewusst keine
/// UI zur Rollenvergabe (siehe Scope-Entscheidung Plattform-Admin) -
/// der erste Admin-Zugang wird über Konfiguration bootstrapped.
/// </summary>
public static class AdminBootstrapper
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["AdminBootstrap:Email"];
        if (string.IsNullOrWhiteSpace(email))
            return;

        var userManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return;

        if (!await userManager.IsInRoleAsync(user, Roles.Admin))
            await userManager.AddToRoleAsync(user, Roles.Admin);
    }
}
