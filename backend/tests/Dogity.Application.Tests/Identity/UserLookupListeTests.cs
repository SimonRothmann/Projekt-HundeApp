using Dogity.Application.Abstractions;
using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Identity;

/// <summary>Die Admin-Nutzerliste holt die Rollen der Seite gesammelt (nicht je Nutzer:in einzeln).</summary>
public class UserLookupListeTests
{
    [Fact]
    public async Task ListPaged_OrdnetRollenJeNutzerZu_ReihenfolgeNachEmail()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<KontoStatus>();
        services.AddScoped<UserLookupService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var rolle in new[] { "Admin", "Trainer" })
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(rolle))).Succeeded);

        async Task Lege(string email, params string[] rollen)
        {
            var konto = new ApplicationUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(konto)).Succeeded);
            if (rollen.Length > 0) Assert.True((await users.AddToRolesAsync(konto, rollen)).Succeeded);
        }
        await Lege("c@test.de");
        await Lege("a@test.de", "Admin", "Trainer");
        await Lege("b@test.de", "Trainer");

        var (liste, gesamt) = await scope.ServiceProvider.GetRequiredService<UserLookupService>().ListPagedAsync(1, 10, null);

        Assert.Equal(3, gesamt);
        Assert.Equal(["a@test.de", "b@test.de", "c@test.de"], liste.Select(e => e.Email));
        Assert.Equal(["Admin", "Trainer"], liste[0].Roles.OrderBy(r => r));
        Assert.Equal(["Trainer"], liste[1].Roles);
        Assert.Empty(liste[2].Roles);
    }
}
