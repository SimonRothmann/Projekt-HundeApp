using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Preferences;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Paket L2: die ÖKV-Ordnungen im echten Katalog (Seeder) und der
/// Sportarten-Filter nach Land.
/// </summary>
public class OesterreichKatalogTests
{
    private static readonly string[] Erwartet =
    [
        "ÖKV BH/VT (Mensch-Hund-Team-Prüfung)",
        "ÖKV Agility LK 1 (A 1)", "ÖKV Agility LK 2 (A 2)", "ÖKV Agility LK 3 (A 3)", "ÖKV Jumping (J 1 - J 3)",
        "ÖKV Hoopers 1 (H1)", "ÖKV Hoopers 2 (H2)", "ÖKV Hoopers 3 (H3)",
        "ÖKV Breitensport VK 1", "ÖKV Breitensport VK 2", "ÖKV Breitensport Dreikampf",
        "ÖKV Slalomlauf", "ÖKV Hürdenlauf", "ÖKV Hindernislauf", "ÖKV CSC", "ÖKV Shorty",
    ];

    private static IServiceProvider Dienste(ApplicationDbContext db) =>
        new ServiceCollection().AddSingleton(db).BuildServiceProvider();

    private static async Task<ApplicationDbContext> KatalogAsync()
    {
        var db = InMemoryDbContext.Create();
        var dienste = Dienste(db);
        await SportCatalogSeeder.SeedAsync(dienste);
        await CountrySeeder.SeedAsync(dienste);
        return db;
    }

    private static async Task<List<(string Sport, string Name, int Summe, List<(string Uebung, int Punkte, bool Pflicht)> Uebungen)>> OrdnungenAsync(ApplicationDbContext db)
    {
        var ordnungen = await db.Regulations.Include(r => r.Sport).Where(r => r.CountryCode == "AT").ToListAsync();
        var ergebnis = new List<(string, string, int, List<(string, int, bool)>)>();
        foreach (var o in ordnungen)
        {
            var version = await db.RegulationVersions.SingleAsync(v => v.RegulationId == o.Id);
            var uebungen = await db.RegulationExercises
                .Where(re => re.RegulationVersionId == version.Id)
                .OrderBy(re => re.SortOrder)
                .Select(re => new { re.Exercise!.Name, re.MaxPoints, re.IsMandatory })
                .ToListAsync();
            ergebnis.Add((o.Sport!.Code, o.Name, uebungen.Sum(u => u.MaxPoints),
                uebungen.Select(u => (u.Name, u.MaxPoints, u.IsMandatory)).ToList()));
        }
        return ergebnis;
    }

    [Fact]
    public async Task AlleOekvOrdnungenSindDaMitLandAtQuelleUndVersion()
    {
        await using var db = await KatalogAsync();

        var ordnungen = await db.Regulations.Include(r => r.Sport).Include(r => r.Versions)
            .Where(r => r.CountryCode == "AT").ToListAsync();

        Assert.Equal(Erwartet.Order(), ordnungen.Select(o => o.Name).Order());
        Assert.All(ordnungen, o =>
        {
            Assert.StartsWith("https://", o.SourceUrl);
            Assert.Single(o.Versions);
            Assert.Contains("S. ", o.Description);
            // Nationale Ordnungen bleiben deutsch.
            Assert.Null(o.NameEn);
        });
        var sportarten = ordnungen.GroupBy(o => o.Sport!.Code).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(1, sportarten["BH"]);
        Assert.Equal(4, sportarten["AGILITY"]);
        Assert.Equal(3, sportarten["HOOPERS"]);
        Assert.Equal(8, sportarten["BSP"]);
        // Gültigkeitsbeginn laut Dokument.
        Assert.Equal(new DateOnly(2022, 1, 1), ordnungen.Single(o => o.Name.StartsWith("ÖKV BH")).Versions.Single().ValidFrom);
        Assert.Equal(new DateOnly(2026, 1, 1), ordnungen.Single(o => o.Name == "ÖKV Agility LK 1 (A 1)").Versions.Single().ValidFrom);
        Assert.Equal(new DateOnly(2024, 1, 1), ordnungen.Single(o => o.Name == "ÖKV Hoopers 1 (H1)").Versions.Single().ValidFrom);
        Assert.Equal(new DateOnly(2025, 1, 1), ordnungen.Single(o => o.Name == "ÖKV Breitensport VK 1").Versions.Single().ValidFrom);
    }

    [Fact]
    public async Task Punktsummen_StimmenMitDenGesamtpunktenDerQuelle()
    {
        await using var db = await KatalogAsync();
        var ordnungen = await OrdnungenAsync(db);

        var bh = ordnungen.Single(o => o.Sport == "BH");
        Assert.Equal(100, bh.Summe);
        Assert.Equal(
            new[] { ("Fußarbeit", 20), ("Freifolge", 10), ("Sitzübung", 10), ("Ablegen in Verbindung mit Herankommen", 20), ("Ablegen unter Ablenkung", 20), ("Freilaufen mit Heranrufen", 20) },
            bh.Uebungen.Where(u => u.Punkte > 0).Select(u => (u.Uebung, u.Punkte)).ToArray());
        // Freifolge und Freilaufen sind freiwillig, der Verkehrsteil hat keine Einzelpunkte.
        Assert.False(bh.Uebungen.Single(u => u.Uebung == "Freifolge").Pflicht);
        Assert.False(bh.Uebungen.Single(u => u.Uebung == "Freilaufen mit Heranrufen").Pflicht);
        Assert.Contains(bh.Uebungen, u => u.Uebung == "Verhalten im Verkehr" && u.Punkte == 0 && u.Pflicht);

        Assert.Equal(60, ordnungen.Single(o => o.Name == "ÖKV Breitensport VK 1").Summe);
        Assert.Equal(60, ordnungen.Single(o => o.Name == "ÖKV Breitensport VK 2").Summe);
        var vk1 = ordnungen.Single(o => o.Name == "ÖKV Breitensport VK 1").Uebungen;
        Assert.Equal(new[] { 15, 20, 10, 15 }, vk1.Where(u => u.Punkte > 0).Select(u => u.Punkte).ToArray());
        var vk2 = ordnungen.Single(o => o.Name == "ÖKV Breitensport VK 2").Uebungen;
        Assert.Equal(new[] { 25, 10, 15, 10 }, vk2.Where(u => u.Punkte > 0).Select(u => u.Punkte).ToArray());
        Assert.DoesNotContain(vk2, u => u.Uebung == "Leinenführigkeit");

        // Alle übrigen werden über Fehler und Zeit gewertet: keine Übungspunkte.
        var uebrige = ordnungen.Where(o => o.Sport != "BH" && !o.Name.StartsWith("ÖKV Breitensport VK"));
        Assert.All(uebrige, o => Assert.Equal(0, o.Summe));
    }

    [Fact]
    public async Task Agility_LkEinHatKeineDoppelhuerde_JumpingKeineKontaktzonen()
    {
        await using var db = await KatalogAsync();
        var ordnungen = await OrdnungenAsync(db);
        string[] Namen(string name) => ordnungen.Single(o => o.Name == name).Uebungen.Select(u => u.Uebung).ToArray();

        Assert.DoesNotContain("Doppelsprung (Spread)", Namen("ÖKV Agility LK 1 (A 1)"));
        Assert.Contains("Doppelsprung (Spread)", Namen("ÖKV Agility LK 2 (A 2)"));
        Assert.Contains("Wippe", Namen("ÖKV Agility LK 3 (A 3)"));
        var jumping = Namen("ÖKV Jumping (J 1 - J 3)");
        Assert.DoesNotContain("Laufsteg", jumping);
        Assert.DoesNotContain("Wippe", jumping);
        Assert.DoesNotContain("Schrägwand (A-Wand)", jumping);
        // Jugend, Oldies und ParAgility sind Hinweise, keine Ordnungen.
        Assert.DoesNotContain(ordnungen, o => o.Name.Contains("Jugend") || o.Name.Contains("Oldies") || o.Name.Contains("ParA"));
        Assert.Contains("Oldies", (await db.Regulations.SingleAsync(r => r.Name == "ÖKV Agility LK 1 (A 1)")).Description);
    }

    [Fact]
    public async Task Hoopers_WiederverwendetDieVorhandenenUebungen()
    {
        await using var db = await KatalogAsync();
        var ordnungen = await OrdnungenAsync(db);
        var hoopers = await db.Exercises.CountAsync(e => e.Sport!.Code == "HOOPERS");

        Assert.Equal(10, hoopers);
        Assert.All(ordnungen.Where(o => o.Sport == "HOOPERS"), o => Assert.Equal(hoopers, o.Uebungen.Count));
    }

    [Fact]
    public async Task Breitensport_HatEigeneUebungenUndNichtDieDesTurnierhundsports()
    {
        await using var db = await KatalogAsync();

        var bsp = await db.Sports.SingleAsync(s => s.Code == "BSP");
        Assert.Equal("Breitensport", bsp.Name);
        Assert.Null(bsp.NameEn);
        var ths = await db.Sports.SingleAsync(s => s.Code == "THS");
        var bspIds = await db.Exercises.Where(e => e.SportId == bsp.Id).Select(e => e.Id).ToListAsync();
        var thsIds = await db.Exercises.Where(e => e.SportId == ths.Id).Select(e => e.Id).ToListAsync();
        Assert.NotEmpty(bspIds);
        Assert.Empty(bspIds.Intersect(thsIds));
        // Keine Übung der Breitensport-Ordnungen stammt aus einer anderen Sportart.
        var fremd = await db.RegulationExercises
            .Where(re => re.RegulationVersion!.Regulation!.SportId == bsp.Id && re.Exercise!.SportId != bsp.Id)
            .CountAsync();
        Assert.Equal(0, fremd);
    }

    [Fact]
    public async Task OesterreichischeUebungenTragenKeineEnglischenNamen()
    {
        await using var db = await KatalogAsync();

        var neuBh = await db.Exercises.Where(e => e.Sport!.Code == "BH"
            && (e.Name == "Freilaufen mit Heranrufen" || e.Name == "Ortsabhängige Begegnung")).ToListAsync();
        Assert.Equal(2, neuBh.Count);
        Assert.All(neuBh, e => Assert.Null(e.NameEn));
        Assert.All(await db.Exercises.Where(e => e.Sport!.Code == "BSP").ToListAsync(), e => Assert.Null(e.NameEn));
        // Wiederverwendete FCI-Übungen behalten ihren englischen Namen.
        Assert.Equal("Heeling on leash", (await db.Exercises.SingleAsync(e => e.Sport!.Code == "BH" && e.Name == "Fußarbeit")).NameEn);
    }

    [Fact]
    public async Task Textlaengen_PassenInDieSpaltenDerDatenbank()
    {
        await using var db = await KatalogAsync();

        Assert.All(await db.Regulations.ToListAsync(), r =>
        {
            Assert.True(r.Name.Length <= 100, r.Name);
            Assert.True((r.Description ?? "").Length <= 2000, $"{r.Name}: {r.Description!.Length}");
            Assert.True((r.SourceUrl ?? "").Length <= 500, r.Name);
        });
        Assert.All(await db.RegulationExercises.ToListAsync(), re => Assert.True((re.ScoringNotes ?? "").Length <= 1000, re.ScoringNotes));
        Assert.All(await db.Exercises.ToListAsync(), e =>
        {
            Assert.True(e.Name.Length <= 150, e.Name);
            Assert.True((e.ScoringCriteria ?? "").Length <= 1000, e.Name);
        });
    }

    [Fact]
    public async Task Seeder_IstIdempotent_UndLaesstEineVonHandGepflegteQuelleStehen()
    {
        await using var db = await KatalogAsync();
        var dienste = Dienste(db);
        var bh = await db.Regulations.SingleAsync(r => r.Name == "ÖKV BH/VT (Mensch-Hund-Team-Prüfung)");
        var shorty = await db.Regulations.SingleAsync(r => r.Name == "ÖKV Shorty");
        bh.SourceUrl = "https://admin.example/quelle";
        shorty.SourceUrl = null;
        await db.SaveChangesAsync();

        var vorher = await Zaehlung(db);
        await SportCatalogSeeder.SeedAsync(dienste);
        await SportCatalogSeeder.SeedAsync(dienste);
        db.ChangeTracker.Clear();

        Assert.Equal(vorher, await Zaehlung(db));
        Assert.Equal("https://admin.example/quelle", (await db.Regulations.SingleAsync(r => r.Id == bh.Id)).SourceUrl);
        Assert.NotNull((await db.Regulations.SingleAsync(r => r.Id == shorty.Id)).SourceUrl);
    }

    private static async Task<string> Zaehlung(ApplicationDbContext db) =>
        string.Join("/", await db.Sports.CountAsync(), await db.Regulations.CountAsync(), await db.RegulationVersions.CountAsync(),
            await db.Exercises.CountAsync(), await db.RegulationExercises.CountAsync());

    // ---- Sportarten nach Land (Teil A) ----

    private static async Task<HashSet<string>> SichtbareAsync(ApplicationDbContext db, Guid? nutzer, bool alle = false) =>
        (await new SportCatalogService(db).GetSportsAsync(nutzer, alle)).Value!.Select(s => s.Code).ToHashSet();

    private static async Task<Guid> PersonAsync(ApplicationDbContext db, string? land)
    {
        var id = Guid.NewGuid();
        if (land is not null)
        {
            db.UserPreferences.Add(new UserPreference { UserId = id, Country = land });
            await db.SaveChangesAsync();
        }
        return id;
    }

    [Fact]
    public async Task Sportarten_DeutschlandSiehtKeinenBreitensport_AuchOhneGespeichertesLand()
    {
        await using var db = await KatalogAsync();

        foreach (var nutzer in new[] { await PersonAsync(db, null), await PersonAsync(db, "DE") })
        {
            var codes = await SichtbareAsync(db, nutzer);
            Assert.DoesNotContain("BSP", codes);
            Assert.Contains("THS", codes);
            Assert.Contains("AGILITY", codes);
            Assert.Contains("HOOPERS", codes);
            Assert.Subset(codes, FciKatalog.InternationaleSportarten.ToHashSet());
        }
    }

    [Fact]
    public async Task Sportarten_OesterreichSiehtBreitensportAgilityHoopersUndFci_AberKeinenThs()
    {
        await using var db = await KatalogAsync();
        var codes = await SichtbareAsync(db, await PersonAsync(db, "AT"));

        Assert.Contains("BSP", codes);
        Assert.Contains("AGILITY", codes);
        Assert.Contains("HOOPERS", codes);
        Assert.Subset(codes, FciKatalog.InternationaleSportarten.ToHashSet());
        Assert.DoesNotContain("THS", codes);
    }

    [Fact]
    public async Task Sportarten_AndereLaenderSehenNurDieInternationalen()
    {
        await using var db = await KatalogAsync();
        var codes = await SichtbareAsync(db, await PersonAsync(db, "CH"));

        Assert.Equal(FciKatalog.InternationaleSportarten.Order(), codes.Order());
    }

    [Fact]
    public async Task Sportarten_AlleUndAnonymSehenEverything()
    {
        await using var db = await KatalogAsync();
        var gesamt = (await db.Sports.Select(s => s.Code).ToListAsync()).ToHashSet();

        Assert.Equal(gesamt, await SichtbareAsync(db, await PersonAsync(db, "AT"), alle: true));
        Assert.Equal(gesamt, await SichtbareAsync(db, await PersonAsync(db, "DE"), alle: true));
        Assert.Equal(gesamt, await SichtbareAsync(db, null));
    }

    [Fact]
    public async Task Sportarten_OhneOrdnungBleibtUeberallSichtbar_AuchVereinssportarten()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var verein = new Club { Name = "Verein" };
        db.Clubs.Add(verein);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = nutzer });
        var frei = new Sport { Code = "FREI", Name = "Freie Sportart" };
        var eigene = new Sport { Code = "EIGEN", Name = "Vereinssport", ClubId = verein.Id };
        var nurAt = new Sport { Code = "NURAT", Name = "Nur AT" };
        db.Sports.AddRange(frei, eigene, nurAt);
        db.Regulations.Add(new Regulation { SportId = nurAt.Id, Name = "AT-Ordnung", CountryCode = "AT" });
        db.UserPreferences.Add(new UserPreference { UserId = nutzer, Country = "DE" });
        await db.SaveChangesAsync();

        var codes = await SichtbareAsync(db, nutzer);

        Assert.Contains("FREI", codes);
        Assert.Contains("EIGEN", codes);
        Assert.DoesNotContain("NURAT", codes);
    }

    [Fact]
    public async Task Sportarten_EineGeloeschteOrdnungZaehltNicht()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = await PersonAsync(db, "DE");
        var sport = new Sport { Code = "X", Name = "X" };
        db.Sports.Add(sport);
        db.Regulations.AddRange(
            new Regulation { SportId = sport.Id, Name = "Nur AT", CountryCode = "AT" },
            new Regulation { SportId = sport.Id, Name = "Gelöscht DE", CountryCode = "DE", DeletedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        // Die einzige in Deutschland geltende Ordnung ist gelöscht: nicht sichtbar.
        Assert.DoesNotContain("X", await SichtbareAsync(db, nutzer));
    }

    [Fact]
    public async Task UnsichtbareSportart_BleibtPerIdLesbar_FuerBestehendeZiele()
    {
        await using var db = await KatalogAsync();
        var nutzer = await PersonAsync(db, "DE");
        var dienst = new SportCatalogService(db);
        var bsp = await db.Sports.SingleAsync(s => s.Code == "BSP");
        var ordnung = await db.Regulations.FirstAsync(r => r.SportId == bsp.Id);

        Assert.DoesNotContain("BSP", await SichtbareAsync(db, nutzer));
        Assert.NotEmpty((await dienst.GetExercisesAsync(bsp.Id, nutzer)).Value!);
        Assert.NotEmpty((await dienst.GetRegulationsAsync(bsp.Id, null)).Value!);
        Assert.True((await dienst.GetRegulationDetailAsync(ordnung.Id)).Succeeded);
    }
}
