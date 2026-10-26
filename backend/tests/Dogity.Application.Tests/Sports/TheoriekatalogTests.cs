using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Welche Prüfungsordnungen im Katalog einen Sachkunde-Theorieteil haben
/// (<c>Regulation.TheoryCatalogCode</c>): nur die FCI-BH/VT, deren Ablauf den
/// Sachkundenachweis enthält - nicht die Ordnungen, die eine bestandene BH/VT
/// bloß voraussetzen, und nicht Österreich und die Schweiz.
/// </summary>
public class TheoriekatalogTests
{
    private static async Task<ApplicationDbContext> KatalogAsync()
    {
        var db = InMemoryDbContext.Create();
        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        await SportCatalogSeeder.SeedAsync(services);
        return db;
    }

    [Fact]
    public async Task NurDieFciBegleithundpruefung_HatEinenTheorieKatalog()
    {
        await using var db = await KatalogAsync();

        var mitTheorie = await db.Regulations
            .Where(r => r.TheoryCatalogCode != null)
            .Select(r => new { r.Name, r.TheoryCatalogCode, r.CountryCode, SportCode = r.Sport!.Code })
            .ToListAsync();

        var bh = Assert.Single(mitTheorie);
        Assert.Equal("BH", bh.Name);
        Assert.Equal("BH", bh.SportCode);
        Assert.Null(bh.CountryCode); // FCI-Ordnung, gilt überall
        Assert.Equal(SachkundeSeeder.ErwachsenenKatalogCode, bh.TheoryCatalogCode);
        Assert.Equal("SWHV-BHVT-ERW", bh.TheoryCatalogCode);
    }

    [Fact]
    public async Task OesterreichSchweizUndZulassungOrdnungen_HabenKeinenTheorieKatalog()
    {
        await using var db = await KatalogAsync();

        var ohne = await db.Regulations
            .Where(r => r.Name.StartsWith("ÖKV BH") || r.Name.StartsWith("SKG BH") || r.Name.StartsWith("IBGH") || r.Name.StartsWith("IGP"))
            .ToListAsync();

        Assert.NotEmpty(ohne);
        Assert.All(ohne, r => Assert.Null(r.TheoryCatalogCode));
    }

    [Fact]
    public async Task Seeder_ZiehtDenKatalogBeiBestehenderOrdnungNach_UndLaeuftIdempotent()
    {
        await using var db = await KatalogAsync();
        var bh = await db.Regulations.SingleAsync(r => r.Name == "BH");
        bh.TheoryCatalogCode = null; // Bestand von vor der Migration
        await db.SaveChangesAsync();

        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        await SportCatalogSeeder.SeedAsync(services);
        await SportCatalogSeeder.SeedAsync(services);

        Assert.Equal("SWHV-BHVT-ERW", (await db.Regulations.SingleAsync(r => r.Name == "BH")).TheoryCatalogCode);
        Assert.Single(await db.Regulations.Where(r => r.TheoryCatalogCode != null).ToListAsync());
    }
}
