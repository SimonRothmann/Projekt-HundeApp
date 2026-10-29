using Dogity.Infrastructure.Identity;
using Dogity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Identity;

/// <summary>
/// KontoStatus entscheidet bei JEDER angemeldeten Anfrage (JwtBearerEvents.
/// OnTokenValidated), ob ein gültiger Token noch arbeiten darf. Festgehalten:
/// gelöscht und vom Admin gesperrt heißt nein, die kurze Sperre nach falschen
/// Passwörtern heißt ja (die kann jede:r mit der E-Mail-Adresse auslösen), und
/// nach Vergessen gilt eine Sperre sofort statt erst nach der Merkdauer.
/// </summary>
public class KontoStatusTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    private static ServiceProvider BaueDienste()
    {
        var datenbank = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(datenbank));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddMemoryCache();
        services.AddSingleton<TimeProvider>(new FesteUhr(Jetzt));
        services.AddScoped<KontoStatus>();
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> LegeKontoAnAsync(IServiceProvider provider, DateTimeOffset? sperreBis = null)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var konto = new ApplicationUser { UserName = "a@dogity.test", Email = "a@dogity.test", LockoutEnd = sperreBis };
        Assert.True((await users.CreateAsync(konto)).Succeeded);
        return konto.Id;
    }

    private static async Task<bool> IstNutzbarAsync(IServiceProvider provider, Guid id)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<KontoStatus>().IstNutzbarAsync(id);
    }

    private static async Task SperreAsync(IServiceProvider provider, Guid id, DateTimeOffset bis)
    {
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var konto = (await users.FindByIdAsync(id.ToString()))!;
        konto.LockoutEnd = bis;
        await users.UpdateAsync(konto);
    }

    [Fact]
    public async Task AktivesKonto_IstNutzbar()
    {
        using var provider = BaueDienste();
        var id = await LegeKontoAnAsync(provider);

        Assert.True(await IstNutzbarAsync(provider, id));
    }

    [Fact]
    public async Task UnbekanntesOderGeloeschtesKonto_IstNichtNutzbar()
    {
        using var provider = BaueDienste();

        Assert.False(await IstNutzbarAsync(provider, Guid.NewGuid()));
    }

    [Fact]
    public async Task AdminSperre_IstNichtNutzbar()
    {
        using var provider = BaueDienste();
        // So sperrt die Verwaltung (UserLookupService.LockUserAsync).
        var id = await LegeKontoAnAsync(provider, DateTimeOffset.MaxValue);

        Assert.False(await IstNutzbarAsync(provider, id));
    }

    [Fact]
    public async Task SperreNachFalschenPasswoertern_BeendetLaufendeSitzungNicht()
    {
        using var provider = BaueDienste();
        var id = await LegeKontoAnAsync(provider, Jetzt.AddMinutes(5));

        Assert.True(await IstNutzbarAsync(provider, id));
    }

    [Fact]
    public async Task AbgelaufeneAdminSperre_IstWiederNutzbar()
    {
        using var provider = BaueDienste();
        var id = await LegeKontoAnAsync(provider, Jetzt.AddMinutes(-1));

        Assert.True(await IstNutzbarAsync(provider, id));
    }

    [Fact]
    public async Task SperreWaehrendDerMerkdauer_GreiftErstNachVergessen()
    {
        using var provider = BaueDienste();
        var id = await LegeKontoAnAsync(provider);
        Assert.True(await IstNutzbarAsync(provider, id));

        await SperreAsync(provider, id, DateTimeOffset.MaxValue);
        // Gemerkt: eine Abfrage je Konto und halber Minute, nicht je Anfrage.
        Assert.True(await IstNutzbarAsync(provider, id));

        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<KontoStatus>().Vergessen(id);

        Assert.False(await IstNutzbarAsync(provider, id));
    }
}
