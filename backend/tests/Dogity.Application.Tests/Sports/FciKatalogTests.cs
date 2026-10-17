using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Paket L1 im echten Katalog: Die FCI-Ordnungen gelten überall und tragen
/// englische Namen, die nationalen VDH-Ordnungen (Turnierhundsport, Agility,
/// Hoopers) bleiben Deutschland vorbehalten und deutsch. Seit Paket L2 kommen
/// die ÖKV-Ordnungen dazu (Land "AT", ebenfalls deutsch) - die Prüfungen hier
/// betrachten deshalb jeweils die Ordnungen OHNE Österreich. Dasselbe gilt für
/// die SKG-Ordnungen der Schweiz (Paket L3, Land "CH", ebenfalls deutsch).
/// </summary>
public class FciKatalogTests
{
    private static readonly string[] Vdh = ["THS", "AGILITY", "HOOPERS"];

    private static IServiceProvider Dienste(ApplicationDbContext db) =>
        new ServiceCollection().AddSingleton(db).BuildServiceProvider();

    /// <summary>Frische Datenbank mit der Seeder-Reihenfolge aus Program.cs.</summary>
    private static async Task<ApplicationDbContext> KatalogAsync()
    {
        var db = InMemoryDbContext.Create();
        var dienste = Dienste(db);
        await SportCatalogSeeder.SeedAsync(dienste);
        await CountrySeeder.SeedAsync(dienste);
        return db;
    }

    [Fact]
    public async Task FrischeDatenbank_FciOrdnungenGeltenUeberall_VdhNurInDeutschland()
    {
        await using var db = await KatalogAsync();

        var alle = await db.Regulations.Select(r => new { r.Sport!.Code, r.Name, r.CountryCode }).ToListAsync();
        // Die österreichischen Ordnungen (Paket L2) haben ihre eigenen Tests.
        var oesterreich = alle.Where(o => o.CountryCode == "AT").ToList();
        Assert.NotEmpty(oesterreich);
        Assert.All(oesterreich, o => Assert.StartsWith("ÖKV", o.Name));
        // Ebenso die schweizerischen (Paket L3).
        var schweiz = alle.Where(o => o.CountryCode == "CH").ToList();
        Assert.NotEmpty(schweiz);
        Assert.All(schweiz, o => Assert.StartsWith("SKG", o.Name));
        var ordnungen = alle.Except(oesterreich).Except(schweiz).ToList();

        Assert.All(ordnungen.Where(o => Vdh.Contains(o.Code)), o => Assert.Equal("DE", o.CountryCode));
        Assert.All(ordnungen.Where(o => !Vdh.Contains(o.Code)), o => Assert.Null(o.CountryCode));
        // Alle drei VDH-Sportarten sind tatsächlich im Katalog (der Test prüft sonst nichts).
        Assert.Equal(Vdh.Order(), ordnungen.Where(o => Vdh.Contains(o.Code)).Select(o => o.Code).Distinct().Order());
    }

    [Fact]
    public async Task InternationaleSportarten_SindGenauDieFciSportartenDesKatalogs()
    {
        await using var db = await KatalogAsync();

        var codes = await db.Sports.Where(s => s.ClubId == null).Select(s => s.Code).ToListAsync();

        // Weder fehlt eine FCI-Sportart in der Liste, noch steht Nationales darin -
        // auch nicht der österreichische Breitensport und der Schweizer Begleithund.
        Assert.Equal(
            codes.Except(Vdh).Except([SportCatalogSeeder.BreitensportCode, SportCatalogSeeder.BegleithundSkgCode]).Order(),
            FciKatalog.InternationaleSportarten.Order());
    }

    [Fact]
    public async Task Katalog_FuerOesterreich_LiefertFciUndOekvAberKeineVdhOrdnungen()
    {
        await using var db = await KatalogAsync();
        var dienst = new SportCatalogService(db);

        foreach (var sport in await db.Sports.ToListAsync())
        {
            var inAt = (await dienst.GetRegulationsAsync(sport.Id, "AT")).Value!;
            var inDe = (await dienst.GetRegulationsAsync(sport.Id, "DE")).Value!;
            var ordnungen = await db.Regulations.Where(r => r.SportId == sport.Id).ToListAsync();

            // International (null) plus das eigene Land - nie die des anderen.
            Assert.Equal(ordnungen.Count(r => r.CountryCode is null or "AT"), inAt.Count);
            Assert.Equal(ordnungen.Count(r => r.CountryCode is null or "DE"), inDe.Count);
            Assert.DoesNotContain(inAt, r => r.CountryCode == "DE");
            Assert.DoesNotContain(inDe, r => r.CountryCode == "AT");
        }

        // Turnierhundsport ist rein deutsch (VDH): In Österreich bleibt er leer.
        var ths = await db.Sports.SingleAsync(s => s.Code == "THS");
        Assert.Empty((await dienst.GetRegulationsAsync(ths.Id, "AT")).Value!);
    }

    [Fact]
    public async Task Laenderzaehler_ZaehltGiltUeberallMit()
    {
        await using var db = await KatalogAsync();
        var fci = await db.Regulations.CountAsync(r => r.CountryCode == null);
        var vdh = await db.Regulations.CountAsync(r => r.CountryCode == "DE");
        var oekv = await db.Regulations.CountAsync(r => r.CountryCode == "AT");
        var skg = await db.Regulations.CountAsync(r => r.CountryCode == "CH");

        var laender = (await new SportCatalogService(db).GetCountriesAsync()).Value!;

        Assert.True(fci > 0 && vdh > 0 && oekv > 0 && skg > 0);
        Assert.Equal(fci + vdh, laender.Single(l => l.Code == "DE").RegulationCount);
        Assert.Equal(fci + oekv, laender.Single(l => l.Code == "AT").RegulationCount);
        Assert.Equal(fci + skg, laender.Single(l => l.Code == "CH").RegulationCount);
        // Ein Land ohne eigene Ordnungen hat trotzdem die internationalen.
        Assert.Equal(fci, laender.Single(l => l.Code == "IT").RegulationCount);
    }

    [Fact]
    public async Task EnglischeNamen_JedeFciSportartOrdnungUndUebungHatEinen()
    {
        await using var db = await KatalogAsync();
        var codes = FciKatalog.InternationaleSportarten;

        var sportarten = await db.Sports.Where(s => codes.Contains(s.Code)).ToListAsync();
        Assert.Equal(codes.Count, sportarten.Count);
        Assert.All(sportarten, s => Assert.False(string.IsNullOrWhiteSpace(s.NameEn), $"Sportart {s.Code}"));

        // Die FCI-Ordnungen, nicht die ÖKV-Ordnung unter der Sportart BH: Nationales bleibt deutsch.
        var ordnungen = await db.Regulations.Where(r => codes.Contains(r.Sport!.Code) && r.CountryCode == null).ToListAsync();
        Assert.NotEmpty(ordnungen);
        Assert.All(ordnungen, r => Assert.False(string.IsNullOrWhiteSpace(r.NameEn), $"Ordnung {r.Name}"));

        // Übungen, die nur in einer österreichischen Ordnung vorkommen, bleiben deutsch.
        var nurOesterreich = await db.RegulationExercises
            .GroupBy(re => re.ExerciseId)
            .Where(g => g.All(re => re.RegulationVersion!.Regulation!.CountryCode == "AT"))
            .Select(g => g.Key)
            .ToListAsync();
        var uebungen = await db.Exercises
            .Where(e => e.Sport != null && codes.Contains(e.Sport.Code) && !nurOesterreich.Contains(e.Id))
            .ToListAsync();
        Assert.NotEmpty(uebungen);
        Assert.All(uebungen, e => Assert.False(string.IsNullOrWhiteSpace(e.NameEn), $"Übung {e.Sport!.Code}/{e.Name}"));
    }

    [Fact]
    public async Task EnglischeNamen_KeineVdhSportartOrdnungOderUebungBekommtEinen()
    {
        await using var db = await KatalogAsync();

        Assert.All(await db.Sports.Where(s => Vdh.Contains(s.Code)).ToListAsync(), s => Assert.Null(s.NameEn));
        var ordnungen = await db.Regulations.Where(r => Vdh.Contains(r.Sport!.Code)).ToListAsync();
        Assert.NotEmpty(ordnungen);
        Assert.All(ordnungen, r => Assert.Null(r.NameEn));
        var uebungen = await db.Exercises.Where(e => e.Sport != null && Vdh.Contains(e.Sport.Code)).ToListAsync();
        Assert.NotEmpty(uebungen);
        Assert.All(uebungen, e => Assert.Null(e.NameEn));
    }

    [Fact]
    public async Task EnglischeNamen_SindProSportartUndOrdnungEindeutigUndPassenInDieSpalten()
    {
        await using var db = await KatalogAsync();

        var uebungen = await db.Exercises.Where(e => e.NameEn != null).ToListAsync();
        // Zwei Übungen derselben Sportart mit gleichem englischem Namen wären
        // in der Übungsauswahl nicht zu unterscheiden.
        Assert.All(uebungen.GroupBy(e => e.SportId), g => Assert.Equal(g.Count(), g.Select(e => e.NameEn).Distinct().Count()));
        Assert.All(uebungen, e => Assert.True(e.NameEn!.Length <= 150));
        Assert.All(await db.Regulations.Where(r => r.NameEn != null).ToListAsync(), r => Assert.True(r.NameEn!.Length <= 100));
        Assert.All(await db.Sports.Where(s => s.NameEn != null).ToListAsync(), s => Assert.True(s.NameEn!.Length <= 100));
    }

    [Fact]
    public async Task Seeder_IstIdempotent_UndUeberschreibtKeineAdminEingabe()
    {
        await using var db = await KatalogAsync();
        var dienste = Dienste(db);
        var uebung = await db.Exercises.FirstAsync(e => e.Sport!.Code == "IGP1" && e.Name == "Freifolge");
        var ordnung = await db.Regulations.FirstAsync(r => r.Name == "FCI-IGP 1");
        var sport = await db.Sports.FirstAsync(s => s.Code == "IGP1");
        uebung.NameEn = "Heeling, off leash";
        ordnung.NameEn = "IGP 1 (Admin)";
        sport.NameEn = null;
        await db.SaveChangesAsync();

        var vorher = await Momentaufnahme(db);
        await SportCatalogSeeder.SeedAsync(dienste);
        await CountrySeeder.SeedAsync(dienste);
        db.ChangeTracker.Clear();

        Assert.Equal("Heeling, off leash", (await db.Exercises.SingleAsync(e => e.Id == uebung.Id)).NameEn);
        Assert.Equal("IGP 1 (Admin)", (await db.Regulations.SingleAsync(r => r.Id == ordnung.Id)).NameEn);
        // Eine leere Sportart bekommt ihren Namen nachgetragen - das war die einzige Lücke.
        Assert.Equal(FciKatalog.Sportarten["IGP1"], (await db.Sports.SingleAsync(s => s.Id == sport.Id)).NameEn);

        // Ein weiterer Lauf ändert nichts mehr.
        var nachLauf1 = await Momentaufnahme(db);
        await SportCatalogSeeder.SeedAsync(dienste);
        db.ChangeTracker.Clear();
        Assert.Equal(nachLauf1, await Momentaufnahme(db));
        Assert.NotEqual(vorher, nachLauf1);
    }

    [Fact]
    public async Task Seeder_RueckenKeinenVonHandGesetztenGeltungsbereichZurecht()
    {
        await using var db = await KatalogAsync();
        // Ein Admin hat die Agility-Ordnung bewusst international gestellt und
        // eine FCI-Ordnung auf Österreich eingegrenzt.
        var agility = await db.Regulations.FirstAsync(r => r.Name == "Agility 1 (A1)");
        var fci = await db.Regulations.FirstAsync(r => r.Name == "FCI-UPr 1");
        agility.CountryCode = null;
        fci.CountryCode = "AT";
        await db.SaveChangesAsync();

        var dienste = Dienste(db);
        await SportCatalogSeeder.SeedAsync(dienste);
        await CountrySeeder.SeedAsync(dienste);
        db.ChangeTracker.Clear();

        Assert.Null((await db.Regulations.SingleAsync(r => r.Id == agility.Id)).CountryCode);
        Assert.Equal("AT", (await db.Regulations.SingleAsync(r => r.Id == fci.Id)).CountryCode);
    }

    private static async Task<string> Momentaufnahme(ApplicationDbContext db)
    {
        var sportarten = await db.Sports.OrderBy(s => s.Code).Select(s => s.Code + "=" + s.NameEn).ToListAsync();
        var ordnungen = await db.Regulations.OrderBy(r => r.Name).Select(r => r.Name + "=" + r.NameEn + "/" + r.CountryCode).ToListAsync();
        var uebungen = await db.Exercises.Select(e => e.Sport!.Code + "/" + e.Name + "=" + e.NameEn).ToListAsync();
        return string.Join("\n", sportarten.Concat(ordnungen).Concat(uebungen.Order()));
    }
}
