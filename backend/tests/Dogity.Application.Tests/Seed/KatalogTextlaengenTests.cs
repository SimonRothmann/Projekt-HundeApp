using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Seed;

/// <summary>
/// Die Seeder laufen bei jedem Start, auch in Production; ein Text, der länger
/// ist als seine Spalte, ließe sie dort am Datenbankfehler scheitern und den
/// Start verhindern. In-Memory merkt das nicht, deshalb prüft dieser Test jede
/// geseedete Zeichenkette gegen die MaxLength im EF-Modell - für alle Spalten,
/// auch die englischen, und für künftige Kataloge automatisch.
/// </summary>
public class KatalogTextlaengenTests
{
    [Fact]
    public async Task GeseedeteTexte_PassenInDieSpaltenDesModells()
    {
        await using var db = InMemoryDbContext.Create();
        var dienste = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        await SportCatalogSeeder.SeedAsync(dienste);
        await CountrySeeder.SeedAsync(dienste);
        await SachkundeSeeder.SeedAsync(dienste);

        var eintraege = db.ChangeTracker.Entries().ToList();
        Assert.NotEmpty(eintraege);

        var zuLang = new List<string>();
        foreach (var eintrag in eintraege)
        {
            foreach (var eigenschaft in eintrag.Properties)
            {
                var grenze = eigenschaft.Metadata.GetMaxLength();
                if (grenze is null || eigenschaft.CurrentValue is not string text || text.Length <= grenze) continue;

                var anfang = text.Length > 60 ? text[..60] + "…" : text;
                zuLang.Add($"{eintrag.Metadata.ShortName()}.{eigenschaft.Metadata.Name}: {text.Length} Zeichen, Grenze {grenze}: \"{anfang}\"");
            }
        }

        Assert.True(zuLang.Count == 0, "Seed-Texte länger als ihre Spalte:\n" + string.Join("\n", zuLang));
    }
}
