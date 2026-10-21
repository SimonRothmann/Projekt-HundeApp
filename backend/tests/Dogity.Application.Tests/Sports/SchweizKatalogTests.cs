using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Preferences;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Paket L3: die SKG-Ordnungen (Begleithund 1-3, Agility, Jumping) im echten
/// Katalog (Seeder) und der Sportarten-Filter nach Land.
/// </summary>
public class SchweizKatalogTests
{
    private static readonly string[] Erwartet =
    [
        "SKG BH 1", "SKG BH 2", "SKG BH 3",
        "SKG Agility Klasse 1", "SKG Agility Klasse 2", "SKG Agility Klasse 3", "SKG Jumping (Klasse 1-3)",
        "SKG Obedience Beginners",
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
        var ordnungen = await db.Regulations.Include(r => r.Sport).Where(r => r.CountryCode == "CH").ToListAsync();
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
    public async Task AlleSkgOrdnungenSindDaMitLandChQuelleUndVersion()
    {
        await using var db = await KatalogAsync();

        var ordnungen = await db.Regulations.Include(r => r.Sport).Include(r => r.Versions)
            .Where(r => r.CountryCode == "CH").ToListAsync();

        Assert.Equal(Erwartet.Order(), ordnungen.Select(o => o.Name).Order());
        Assert.All(ordnungen, o =>
        {
            Assert.StartsWith("https://", o.SourceUrl);
            Assert.Single(o.Versions);
            // Jede Ordnung belegt ihre Angaben mit Seiten des Dokuments.
            Assert.Contains("S. ", o.Description);
            // Nationale Ordnungen bleiben deutsch.
            Assert.Null(o.NameEn);
        });
        var sportarten = ordnungen.GroupBy(o => o.Sport!.Code).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, sportarten["BHCH"]);
        Assert.Equal(4, sportarten["AGILITY"]);
        // Obedience (Paket O1): die Klasse Beginners unter der Sportart OBEDIENCE.
        Assert.Equal(1, sportarten["OBEDIENCE"]);
        // Gültigkeitsbeginn laut Dokument.
        Assert.All(ordnungen.Where(o => o.Sport!.Code == "BHCH"), o =>
            Assert.Equal(new DateOnly(2022, 3, 1), o.Versions.Single().ValidFrom));
        Assert.All(ordnungen.Where(o => o.Sport!.Code == "AGILITY"), o =>
            Assert.Equal(new DateOnly(2025, 1, 1), o.Versions.Single().ValidFrom));
        Assert.Contains("scog-schaffhausen.ch", ordnungen.First(o => o.Name == "SKG BH 1").SourceUrl);
        Assert.Contains("tkamo.ch", ordnungen.First(o => o.Name == "SKG Agility Klasse 1").SourceUrl);
    }

    [Fact]
    public async Task Punktsummen_StimmenMitDenGesamtpunktenDerQuelle()
    {
        await using var db = await KatalogAsync();
        var ordnungen = await OrdnungenAsync(db);

        // BH 1 bis 3: je 300 Punkte (Tabellen "Prüfungsklassen mit Prüfungsfächern", S. 4-6).
        foreach (var name in new[] { "SKG BH 1", "SKG BH 2", "SKG BH 3" })
            Assert.Equal(300, ordnungen.Single(o => o.Name == name).Summe);

        // BH 1: 80 + 20 | 5 x 20 | 5 x 20.
        Assert.Equal(
            new[] { ("Halten der Fährte", 80), ("Gegenstände auf der Fährte", 20),
                    ("Leinenführigkeit", 20), ("Freifolge", 20), ("Stellungen in der Fussposition", 20), ("Apportieren", 20), ("Hochsprung", 20),
                    ("Durchgehen einer Personengruppe", 20), ("Abruf unter Ablenkung", 20), ("Sitz aus der Bewegung", 20), ("Stellungen in Frontposition", 20), ("Abrufen über die Hürde", 20) },
            ordnungen.Single(o => o.Name == "SKG BH 1").Uebungen.Select(u => (u.Uebung, u.Punkte)).ToArray());

        // BH 2: A1 35 + 15, A2 35 + 15 | 5 x 20 | 20/10/20/10/20/20.
        Assert.Equal(
            new[] { ("Halten der Fährte", 35), ("Gegenstände auf der Fährte", 15), ("Revierarbeit", 35), ("Gegenstände im Revier", 15),
                    ("Freifolge", 20), ("Stellungen in der Fussposition", 20), ("Stellungen in der Frontposition", 20), ("Apportieren", 20), ("Hochsprung", 20),
                    ("Abrufen in Frontposition", 20), ("Sitz aus der Bewegung", 10), ("Stellungen auf Distanz", 20), ("Steh aus dem Normalschritt", 10), ("Voran mit Stellung", 20), ("Abrufen über die Hürde", 20) },
            ordnungen.Single(o => o.Name == "SKG BH 2").Uebungen.Select(u => (u.Uebung, u.Punkte)).ToArray());

        // BH 3: wie BH 2 in A und B, C 20/10/20/10/20/20.
        Assert.Equal(
            new[] { ("Halten der Fährte", 35), ("Gegenstände auf der Fährte", 15), ("Revierarbeit", 35), ("Gegenstände im Revier", 15),
                    ("Freifolge", 20), ("Stellungen in der Fussposition", 20), ("Stellungen in der Frontposition", 20), ("Apportieren", 20), ("Hochsprung", 20),
                    ("Apportieren aus der Bewegung", 20), ("Sitz aus der Bewegung", 10), ("Stellungen auf Distanz", 20), ("Steh aus dem Laufschritt", 10), ("Voran mit Steh und Weitersenden", 20), ("Voran mit Verschieben", 20) },
            ordnungen.Single(o => o.Name == "SKG BH 3").Uebungen.Select(u => (u.Uebung, u.Punkte)).ToArray());

        // Alle Prüfungsfächer sind Pflicht - es gibt keine freiwilligen Übungen.
        Assert.All(ordnungen.Where(o => o.Sport == "BHCH"), o => Assert.All(o.Uebungen, u => Assert.True(u.Pflicht)));

        // Agility und Jumping werden über Fehler und Zeit gewertet: keine Übungspunkte.
        Assert.All(ordnungen.Where(o => o.Sport == "AGILITY"), o => Assert.Equal(0, o.Summe));
    }

    [Fact]
    public async Task Abteilungen_ErreichenJeHundertPunkte()
    {
        await using var db = await KatalogAsync();
        var ordnungen = await OrdnungenAsync(db);
        var kategorien = await db.Exercises.Where(e => e.Sport!.Code == "BHCH").ToDictionaryAsync(e => e.Name, e => e.Category);

        foreach (var ordnung in ordnungen.Where(o => o.Sport == "BHCH"))
        {
            int Summe(params string[] kategorie) => ordnung.Uebungen.Where(u => kategorie.Contains(kategorien[u.Uebung])).Sum(u => u.Punkte);
            // Nasenarbeit: Fährte (bei BH 2/3 zusammen mit dem Revier), Unterordnung, Führigkeit.
            Assert.Equal(100, Summe("Fährte", "Revier"));
            Assert.Equal(100, Summe("Unterordnung"));
            Assert.Equal(100, Summe("Führigkeit"));
        }

        // Die Fährte allein wiegt in BH 2 und 3 die Hälfte, das Revier die andere.
        var bh2 = ordnungen.Single(o => o.Name == "SKG BH 2");
        Assert.Equal(50, bh2.Uebungen.Where(u => kategorien[u.Uebung] == "Fährte").Sum(u => u.Punkte));
        Assert.Equal(50, bh2.Uebungen.Where(u => kategorien[u.Uebung] == "Revier").Sum(u => u.Punkte));
        Assert.DoesNotContain(ordnungen.Single(o => o.Name == "SKG BH 1").Uebungen, u => kategorien[u.Uebung] == "Revier");
    }

    [Fact]
    public async Task BeschreibungenNennenAlterBestehensgrenzeUndBewertungsstufen()
    {
        await using var db = await KatalogAsync();
        string Beschreibung(string name) => db.Regulations.Single(r => r.Name == name).Description!;

        Assert.Contains("12 Monate", Beschreibung("SKG BH 1"));
        Assert.Contains("19 Monate", Beschreibung("SKG BH 2"));
        Assert.Contains("20 Monate", Beschreibung("SKG BH 3"));
        foreach (var name in new[] { "SKG BH 1", "SKG BH 2", "SKG BH 3" })
        {
            var text = Beschreibung(name);
            Assert.Contains("mindestens 70 Punkte", text);
            Assert.Contains("210 von 300", text);
            Assert.Contains("Vorzüglich 100-96 %", text);
            Assert.Contains("Mangelhaft 69-0 %", text);
            Assert.Contains("V 300-286", text);
        }
    }

    [Fact]
    public async Task Agility_Klasse1HatKeinenDoppelsprung_JumpingKeineKontaktzonen()
    {
        await using var db = await KatalogAsync();
        var ordnungen = await OrdnungenAsync(db);
        string[] Namen(string name) => ordnungen.Single(o => o.Name == name).Uebungen.Select(u => u.Uebung).ToArray();

        Assert.DoesNotContain("Doppelsprung (Spread)", Namen("SKG Agility Klasse 1"));
        Assert.Contains("Doppelsprung (Spread)", Namen("SKG Agility Klasse 2"));
        Assert.Contains("Wippe", Namen("SKG Agility Klasse 3"));
        var jumping = Namen("SKG Jumping (Klasse 1-3)");
        Assert.DoesNotContain("Laufsteg", jumping);
        Assert.DoesNotContain("Wippe", jumping);
        Assert.DoesNotContain("Schrägwand (A-Wand)", jumping);

        // Die Geräte sind die vorhandenen der Sportart: keine neuen Agility-Übungen.
        var agilityUebungen = (await db.Exercises.Where(e => e.Sport!.Code == "AGILITY").ToListAsync()).Select(e => e.Name).ToHashSet();
        Assert.Equal(10, agilityUebungen.Count);
        Assert.All(ordnungen.Where(o => o.Sport == "AGILITY"), o => Assert.Subset(agilityUebungen, o.Uebungen.Select(u => u.Uebung).ToHashSet()));
    }

    [Fact]
    public async Task Agility_TeilnehmerkategorienSindHinweiseKeineOrdnungen()
    {
        await using var db = await KatalogAsync();
        var ordnungen = await OrdnungenAsync(db);

        Assert.DoesNotContain(ordnungen, o => o.Name.Contains("Oldies") || o.Name.Contains("Junior") || o.Name.Contains("Senior") || o.Name.Contains("Large"));
        var beschreibung = (await db.Regulations.SingleAsync(r => r.Name == "SKG Agility Klasse 1")).Description!;
        Assert.Contains("Oldies", beschreibung);
        Assert.Contains("Large ab 48 cm", beschreibung);
        // Was das Reglement offenlässt, steht als solches da und ist nicht erfunden.
        Assert.Contains("nicht im Reglement", beschreibung);
    }

    [Fact]
    public async Task Begleithund_HatEigeneUebungenUndNichtDieDerBhOderIgp()
    {
        await using var db = await KatalogAsync();

        var bhch = await db.Sports.SingleAsync(s => s.Code == "BHCH");
        Assert.Equal("Begleithund (SKG)", bhch.Name);
        Assert.Null(bhch.NameEn);
        var eigene = await db.Exercises.Where(e => e.SportId == bhch.Id).ToListAsync();
        Assert.NotEmpty(eigene);
        Assert.All(eigene, e => Assert.Null(e.NameEn));
        var fremdeIds = await db.Exercises.Where(e => e.SportId != bhch.Id).Select(e => e.Id).ToListAsync();
        Assert.Empty(eigene.Select(e => e.Id).Intersect(fremdeIds));
        // Keine Übung der SKG-Ordnungen stammt aus einer anderen Sportart.
        var fremd = await db.RegulationExercises
            .Where(re => re.RegulationVersion!.Regulation!.SportId == bhch.Id && re.Exercise!.SportId != bhch.Id)
            .CountAsync();
        Assert.Equal(0, fremd);
        // Jede Übung des Katalogs kommt in mindestens einer Ordnung vor (keine Waisen).
        var verwendet = await db.RegulationExercises
            .Where(re => re.RegulationVersion!.Regulation!.SportId == bhch.Id).Select(re => re.ExerciseId).Distinct().ToListAsync();
        Assert.Equal(eigene.Select(e => e.Id).Order(), verwendet.Order());
        // Die FCI-BH bleibt unverändert: weder neue Ordnung noch neue Übung.
        Assert.Empty(await db.Regulations.Where(r => r.Sport!.Code == "BH" && r.CountryCode == "CH").ToListAsync());
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
        Assert.All(await db.RegulationExercises.ToListAsync(), re => Assert.True((re.ScoringNotes ?? "").Length <= 1000, $"{re.ScoringNotes!.Length}: {re.ScoringNotes}"));
        Assert.All(await db.Exercises.ToListAsync(), e =>
        {
            Assert.True(e.Name.Length <= 150, e.Name);
            Assert.True((e.ScoringCriteria ?? "").Length <= 1000, $"{e.Name}: {e.ScoringCriteria!.Length}");
        });
    }

    [Fact]
    public async Task JedeUebungUndOrdnungBelegtIhreSeite()
    {
        await using var db = await KatalogAsync();

        var notizen = await db.RegulationExercises
            .Where(re => re.RegulationVersion!.Regulation!.CountryCode == "CH")
            .Select(re => new { Ordnung = re.RegulationVersion!.Regulation!.Name, Uebung = re.Exercise!.Name, re.ScoringNotes })
            .ToListAsync();
        Assert.NotEmpty(notizen);
        Assert.All(notizen, n => Assert.True((n.ScoringNotes ?? "").Contains("S. "), $"{n.Ordnung}/{n.Uebung} ohne Seitenangabe"));
    }

    [Fact]
    public async Task Seeder_IstIdempotent_UndLaesstEineVonHandGepflegteQuelleStehen()
    {
        await using var db = await KatalogAsync();
        var dienste = Dienste(db);
        var bh1 = await db.Regulations.SingleAsync(r => r.Name == "SKG BH 1");
        var agility = await db.Regulations.SingleAsync(r => r.Name == "SKG Agility Klasse 2");
        bh1.SourceUrl = "https://admin.example/quelle";
        agility.SourceUrl = null;
        await db.SaveChangesAsync();

        var vorher = await Zaehlung(db);
        await SportCatalogSeeder.SeedAsync(dienste);
        await SportCatalogSeeder.SeedAsync(dienste);
        db.ChangeTracker.Clear();

        Assert.Equal(vorher, await Zaehlung(db));
        Assert.Equal("https://admin.example/quelle", (await db.Regulations.SingleAsync(r => r.Id == bh1.Id)).SourceUrl);
        Assert.NotNull((await db.Regulations.SingleAsync(r => r.Id == agility.Id)).SourceUrl);
    }

    private static async Task<string> Zaehlung(ApplicationDbContext db) =>
        string.Join("/", await db.Sports.CountAsync(), await db.Regulations.CountAsync(), await db.RegulationVersions.CountAsync(),
            await db.Exercises.CountAsync(), await db.RegulationExercises.CountAsync());

    [Fact]
    public async Task VerteilungNachLand_ZaehltDieSchweizerOrdnungenUnterCh()
    {
        await using var db = await KatalogAsync();

        var verteilung = (await db.Regulations.Select(r => new { r.Sport!.Code, r.CountryCode }).ToListAsync())
            .Where(r => r.CountryCode == "CH")
            .GroupBy(r => r.Code)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(new Dictionary<string, int> { ["BHCH"] = 3, ["AGILITY"] = 4, ["OBEDIENCE"] = 1 }, verteilung);
    }

    // ---- Sportarten und Ordnungen nach Land ----

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
    public async Task Sportarten_SchweizSiehtDenSchweizerBegleithundAgilityUndFci_AberNichtsAnderesNationales()
    {
        await using var db = await KatalogAsync();
        var codes = await SichtbareAsync(db, await PersonAsync(db, "CH"));

        Assert.Contains("BHCH", codes);
        Assert.Contains("AGILITY", codes);
        Assert.Subset(codes, FciKatalog.InternationaleSportarten.ToHashSet());
        Assert.DoesNotContain("THS", codes);
        Assert.DoesNotContain("BSP", codes);
        // Hoopers gibt es nur als VDH- und ÖKV-Ordnung.
        Assert.DoesNotContain("HOOPERS", codes);
    }

    [Fact]
    public async Task Sportarten_DeutschlandUndOesterreichSehenDenSchweizerBegleithundNicht()
    {
        await using var db = await KatalogAsync();

        foreach (var nutzer in new[] { await PersonAsync(db, null), await PersonAsync(db, "DE"), await PersonAsync(db, "AT"), await PersonAsync(db, "IT") })
            Assert.DoesNotContain("BHCH", await SichtbareAsync(db, nutzer));

        // Agility bleibt für Deutschland und Österreich sichtbar - mit deren Ordnungen.
        Assert.Contains("AGILITY", await SichtbareAsync(db, await PersonAsync(db, "DE")));
        Assert.Contains("AGILITY", await SichtbareAsync(db, await PersonAsync(db, "AT")));
    }

    [Fact]
    public async Task Sportarten_AlleUndAnonymSehenDenSchweizerBegleithund()
    {
        await using var db = await KatalogAsync();

        Assert.Contains("BHCH", await SichtbareAsync(db, await PersonAsync(db, "DE"), alle: true));
        Assert.Contains("BHCH", await SichtbareAsync(db, null));
    }

    [Fact]
    public async Task Ordnungen_ProLandTrennenDieAgilityOrdnungenSauber()
    {
        await using var db = await KatalogAsync();
        var dienst = new SportCatalogService(db);
        var agility = await db.Sports.SingleAsync(s => s.Code == "AGILITY");
        var bhch = await db.Sports.SingleAsync(s => s.Code == "BHCH");

        var inCh = (await dienst.GetRegulationsAsync(agility.Id, "CH")).Value!;
        var inDe = (await dienst.GetRegulationsAsync(agility.Id, "DE")).Value!;
        var inAt = (await dienst.GetRegulationsAsync(agility.Id, "AT")).Value!;

        Assert.Equal(4, inCh.Count);
        Assert.All(inCh, r => Assert.StartsWith("SKG", r.Name));
        Assert.DoesNotContain(inDe, r => r.CountryCode == "CH");
        Assert.DoesNotContain(inAt, r => r.CountryCode == "CH");
        // Der Schweizer Begleithund hat nur Ordnungen für die Schweiz.
        Assert.Equal(3, (await dienst.GetRegulationsAsync(bhch.Id, "CH")).Value!.Count);
        Assert.Empty((await dienst.GetRegulationsAsync(bhch.Id, "DE")).Value!);
        Assert.Empty((await dienst.GetRegulationsAsync(bhch.Id, "AT")).Value!);
    }

    [Fact]
    public async Task UnsichtbareSportart_BleibtPerIdLesbar_FuerBestehendeZiele()
    {
        await using var db = await KatalogAsync();
        var nutzer = await PersonAsync(db, "DE");
        var dienst = new SportCatalogService(db);
        var bhch = await db.Sports.SingleAsync(s => s.Code == "BHCH");
        var ordnung = await db.Regulations.FirstAsync(r => r.SportId == bhch.Id);

        Assert.DoesNotContain("BHCH", await SichtbareAsync(db, nutzer));
        Assert.NotEmpty((await dienst.GetExercisesAsync(bhch.Id, nutzer)).Value!);
        Assert.NotEmpty((await dienst.GetRegulationsAsync(bhch.Id, null)).Value!);
        Assert.True((await dienst.GetRegulationDetailAsync(ordnung.Id)).Succeeded);
    }
}
