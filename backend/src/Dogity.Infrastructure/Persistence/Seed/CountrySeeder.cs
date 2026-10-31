using Dogity.Domain.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Legt die wählbaren Geltungsbereiche an. Welche Prüfungsordnung in welchem
/// Land gilt, setzt der SportCatalogSeeder je Ordnung.
///
/// Die Liste hier ist ein Startbestand, keine abschließende Aufzählung -
/// weitere Länder kommen später als Datenzeile dazu, ohne Deploy. Bewusst
/// klein gehalten: Eine Auswahl aus 195 Ländern, von denen 194 leer sind,
/// wäre keine Hilfe, sondern eine Suchaufgabe.
/// </summary>
public static class CountrySeeder
{
    /// <summary>
    /// Startbestand. Reihenfolge ist Absicht: erst der deutschsprachige Raum,
    /// aus dem die Nutzer kommen, dann der Rest.
    /// </summary>
    private static readonly (string Code, int SortOrder)[] Startbestand =
    [
        ("DE", 10),
        ("AT", 20),
        ("CH", 30),
        ("LU", 40),
        ("NL", 50),
        ("BE", 60),
        ("FR", 70),
        ("IT", 80),
        ("DK", 90),
        ("SE", 100),
        ("PL", 110),
        ("CZ", 120),
        ("GB", 130),
        ("US", 140),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        var vorhanden = await db.Countries.Select(c => c.Code).ToListAsync(ct);
        var fehlend = Startbestand.Where(l => !vorhanden.Contains(l.Code)).ToList();

        // Nur Fehlende anlegen. Eine bereits vorhandene Zeile bleibt, wie sie
        // ist - auch ihre Sortierung, die jemand von Hand geändert haben mag.
        foreach (var (code, reihenfolge) in fehlend)
            db.Countries.Add(new Country { Code = code, SortOrder = reihenfolge });

        if (fehlend.Count > 0) await db.SaveChangesAsync(ct);
    }
}
