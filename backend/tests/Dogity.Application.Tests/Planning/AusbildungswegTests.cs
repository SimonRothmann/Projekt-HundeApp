using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Der Ausbildungsweg führt Prüfungsordnungen über ihre Namen. Diese Tests
/// halten ihn an den Katalog: Wird im SportCatalogSeeder etwas umbenannt,
/// schlagen sie an, statt dass eine Folgestufe stillschweigend verschwindet.
/// </summary>
public class AusbildungswegTests
{
    [Fact]
    public async Task JederNameDesAusbildungswegs_ExistiertImSeederKatalog()
    {
        // Der echte Seeder über eine InMemory-Datenbank - nicht gegen
        // nachgeschriebene Strings, sondern gegen das, was er wirklich anlegt.
        await using var db = InMemoryDbContext.Create();
        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        await SportCatalogSeeder.SeedAsync(services);

        var vorhanden = (await db.Regulations.Select(r => r.Name).ToListAsync()).ToHashSet(StringComparer.Ordinal);
        var fehlend = Ausbildungsweg.AlleNamen.Where(name => !vorhanden.Contains(name)).ToList();

        Assert.True(fehlend.Count == 0, $"Im Seeder nicht (mehr) vorhanden: {string.Join(", ", fehlend)}");
    }

    [Fact]
    public void BhFuehrtZuVierRichtungen()
    {
        Assert.Equal(["FCI-IGP 1", "IBGH1", "FCI-FPr 1", "FCI-UPr 1"], Ausbildungsweg.FolgestufenVon("BH"));
    }

    [Theory]
    [InlineData("FCI-IGP 1", "FCI-IGP 2")]
    [InlineData("FCI-IGP 2", "FCI-IGP 3")]
    [InlineData("IBGH1", "IBGH2")]
    [InlineData("FCI-StöPr 2", "FCI-StöPr 3")]
    [InlineData("FCI-FPr 1", "FCI-FPr 2")]
    [InlineData("Agility 0 (A0)", "Agility 1 (A1)")]
    [InlineData("Agility 2 (A2)", "Agility 3 (A3)")]
    [InlineData("VDH-VK1", "VDH-VK2")]
    public void Stufen_FuehrenZurNaechstenStufe(string von, string nach)
    {
        Assert.Equal([nach], Ausbildungsweg.FolgestufenVon(von));
    }

    [Theory]
    [InlineData("FCI-IGP 3")]
    [InlineData("Agility 3 (A3)")]
    [InlineData("VDH-VK3")]
    [InlineData("Unbekannte PO")]
    [InlineData(null)]
    public void LetzteStufeUndUnbekanntes_HabenKeineFolgestufe(string? name)
    {
        Assert.Empty(Ausbildungsweg.FolgestufenVon(name));
    }
}
