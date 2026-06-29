using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Legt die Basisrollen aus DATABASE.md beim Start an, falls sie noch
/// nicht existieren. Idempotent, daher unproblematisch bei jedem Start.
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }
}
