using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Paket M1: Mantrailing - die Prüfungsordnungen des DVG (MT-I bis MT-III, ab 05/2024) und des SV
/// (SV-MT 1 bis 3, Fassung 2026) in einer Sportart. Die Zahlen stehen hier noch einmal aus den
/// Dokumenten, nicht aus dem Seeder abgeleitet: Jede Stufe hat 100 Punkte, je Verband mit denselben
/// Bereichen.
/// </summary>
public class MantrailingKatalogTests
{
    private const string Code = "MANTRAILING";

    private static readonly string[] DvgNamen = ["DVG MT-I", "DVG MT-II", "DVG MT-III"];
    private static readonly string[] SvNamen = ["SV-MT 1", "SV-MT 2", "SV-MT 3"];

    /// <summary>DVG-PO Mantrailing, Abschnitt "Bewertungen": 5, 5, 30, 10, 10, 30, 10.</summary>
    private static readonly Dictionary<string, int> DvgPunkte = new()
    {
        ["Aufnahme vom Geruch"] = 5,
        ["Aufnahme der Geruchsspur"] = 5,
        ["Verfolgen und Halten der Geruchsspur"] = 30,
        ["Arbeitsbereitschaft"] = 10,
        ["Teamwork"] = 10,
        ["Auffinden der Versteckperson"] = 30,
        ["Anzeigeverhalten bei der Versteckperson"] = 10,
    };

    /// <summary>SV-PO Mantrailing, Bewertungskriterien: 10, 10, 10, 20, 30, 20.</summary>
    private static readonly Dictionary<string, int> SvPunkte = new()
    {
        ["Lageerfassung und Aufnahme der Geruchsspur"] = 10,
        ["Zusammenarbeit mit dem Hund und Lesen des Hundes"] = 10,
        ["Selbständigkeit, Kontrollierbarkeit und soziale Verträglichkeit"] = 10,
        ["Halten der Geruchsspur, Arbeitsbereitschaft und Suchmotivation"] = 20,
        ["Verfolgen der Geruchsspur und Umgang mit Schwierigkeiten"] = 30,
        ["Anzeige der Person"] = 20,
    };

    private static async Task<(ApplicationDbContext Db, IServiceProvider Dienste)> KatalogAsync()
    {
        var db = InMemoryDbContext.Create();
        var dienste = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        await SportCatalogSeeder.SeedAsync(dienste);
        return (db, dienste);
    }

    private static Task<List<RegulationExercise>> ZeilenAsync(ApplicationDbContext db, string ordnung) =>
        db.RegulationExercises
            .Include(re => re.Exercise)
            .Where(re => re.RegulationVersion!.Regulation!.Sport!.Code == Code && re.RegulationVersion.Regulation.Name == ordnung)
            .OrderBy(re => re.SortOrder)
            .ToListAsync();

    [Fact]
    public async Task SechsOrdnungen_ImLandDeutschlandMitQuelleVersionUndGueltigkeit()
    {
        var (db, _) = await KatalogAsync();

        var ordnungen = await db.Regulations.Include(r => r.Versions).Include(r => r.Sport)
            .Where(r => r.Sport!.Code == Code).ToListAsync();

        Assert.Equal(DvgNamen.Concat(SvNamen).Order(), ordnungen.Select(o => o.Name).Order());
        Assert.All(ordnungen, o =>
        {
            Assert.Equal("DE", o.CountryCode);
            Assert.StartsWith("https://", o.SourceUrl);
            Assert.Single(o.Versions);
            // Weder englische Beschreibung noch englischer Name: Die Kürzel gelten in beiden Sprachen.
            Assert.Null(o.DescriptionEn);
            Assert.Contains("100 Punkte", o.Description);
            Assert.Contains("Zulassung", o.Description);
        });
        Assert.All(ordnungen.Where(o => DvgNamen.Contains(o.Name)), o =>
        {
            Assert.Contains("dvg-hundesport.de", o.SourceUrl);
            Assert.Equal("05/2024", o.Versions.Single().VersionLabel);
            Assert.Equal(new DateOnly(2024, 5, 6), o.Versions.Single().ValidFrom);
        });
        Assert.All(ordnungen.Where(o => SvNamen.Contains(o.Name)), o =>
        {
            Assert.Contains("schaeferhunde.de", o.SourceUrl);
            Assert.Equal("2026", o.Versions.Single().VersionLabel);
        });
    }

    [Fact]
    public async Task Sportart_IstNational_AlsoNichtInDerListeDerInternationalen()
    {
        var (db, _) = await KatalogAsync();

        var sport = await db.Sports.SingleAsync(s => s.Code == Code && s.ClubId == null);

        Assert.Equal("Mantrailing", sport.Name);
        Assert.DoesNotContain(Code, FciKatalog.InternationaleSportarten);
    }

    [Theory]
    [InlineData("DVG MT-I")]
    [InlineData("DVG MT-II")]
    [InlineData("DVG MT-III")]
    public async Task DvgStufe_HatDieSiebenBereicheMitZusammen100Punkten(string ordnung)
    {
        var (db, _) = await KatalogAsync();

        var zeilen = await ZeilenAsync(db, ordnung);

        Assert.Equal(DvgPunkte.Keys, zeilen.Select(z => z.Exercise!.Name));
        Assert.All(zeilen, z => Assert.Equal(DvgPunkte[z.Exercise!.Name], z.MaxPoints));
        Assert.All(zeilen, z => Assert.True(z.IsMandatory));
        Assert.Equal(100, zeilen.Sum(z => z.MaxPoints));
    }

    [Theory]
    [InlineData("SV-MT 1")]
    [InlineData("SV-MT 2")]
    [InlineData("SV-MT 3")]
    public async Task SvStufe_HatDieSechsBereicheMitZusammen100Punkten(string ordnung)
    {
        var (db, _) = await KatalogAsync();

        var zeilen = await ZeilenAsync(db, ordnung);

        Assert.Equal(SvPunkte.Keys, zeilen.Select(z => z.Exercise!.Name));
        Assert.All(zeilen, z => Assert.Equal(SvPunkte[z.Exercise!.Name], z.MaxPoints));
        Assert.Equal(100, zeilen.Sum(z => z.MaxPoints));
    }

    [Theory]
    [InlineData("DVG MT-I", new[] { "ca. 300 m", "mindestens 2 Stunden", "mindestens 1 Richtungswechsel", "15 Minuten", "15 Monate", "BH-VT" })]
    [InlineData("DVG MT-II", new[] { "ca. 600 m", "mindestens 4 Stunden", "mindestens 2 Richtungswechsel", "30 Minuten", "Kreuzungen" })]
    [InlineData("DVG MT-III", new[] { "ca. 1000 m", "mindestens 15 Stunden", "mindestens 4 Richtungswechsel", "45 Minuten", "befahrener Straßenbereich" })]
    [InlineData("SV-MT 1", new[] { "ca. 300 m", "mindestens 2 Richtungswechsel", "mindestens 1 Stunde", "25 Minuten", "15 Monate", "FCI BH/VT" })]
    [InlineData("SV-MT 2", new[] { "ca. 600 m", "mindestens 4 Richtungswechsel", "mindestens 3 Stunden", "35 Minuten", "18 Monate", "FCI RH MT V" })]
    [InlineData("SV-MT 3", new[] { "ca. 1000 m", "mindestens 6 Richtungswechsel", "ca. 24 Stunden", "45 Minuten", "24 Monate", "FCI RH MT A" })]
    public async Task Stufenfakten_StehenInDerBeschreibung(string ordnung, string[] fakten)
    {
        var (db, _) = await KatalogAsync();

        var beschreibung = (await db.Regulations.SingleAsync(r => r.Name == ordnung && r.Sport!.Code == Code)).Description;

        Assert.All(fakten, fakt => Assert.Contains(fakt, beschreibung));
    }

    [Fact]
    public async Task Werturteile_DvgMitGrenzen_SvOhneErfundenePunktegrenze()
    {
        var (db, _) = await KatalogAsync();

        var dvg = (await db.Regulations.SingleAsync(r => r.Name == "DVG MT-I")).Description!;
        var sv = (await db.Regulations.SingleAsync(r => r.Name == "SV-MT 1")).Description!;

        Assert.Contains("Vorzüglich ab 96", dvg);
        Assert.Contains("bestanden ab 70 Punkten", dvg);
        Assert.Contains("keine Punktegrenze", sv);
        Assert.DoesNotContain("bestanden ab 70", sv);
    }

    [Fact]
    public async Task Ausbildungsweg_FuehrtJeVerbandVonStufeZuStufe()
    {
        var (db, _) = await KatalogAsync();

        Assert.Equal(["DVG MT-II"], Ausbildungsweg.FolgestufenVon("DVG MT-I"));
        Assert.Equal(["DVG MT-III"], Ausbildungsweg.FolgestufenVon("DVG MT-II"));
        Assert.Empty(Ausbildungsweg.FolgestufenVon("DVG MT-III"));
        Assert.Equal(["SV-MT 2"], Ausbildungsweg.FolgestufenVon("SV-MT 1"));
        Assert.Equal(["SV-MT 3"], Ausbildungsweg.FolgestufenVon("SV-MT 2"));
        Assert.Empty(Ausbildungsweg.FolgestufenVon("SV-MT 3"));
        var schluessel = await db.Regulations.Select(r => r.SeedKey).ToListAsync();
        Assert.All(["DVG MT-I", "DVG MT-II", "DVG MT-III", "SV-MT 1", "SV-MT 2", "SV-MT 3"], n => Assert.Contains(n, schluessel));
    }

    [Fact]
    public async Task ZweiterLauf_LegtNichtsDoppeltAn()
    {
        var (db, dienste) = await KatalogAsync();
        var ordnungen = await db.Regulations.CountAsync();
        var uebungen = await db.Exercises.CountAsync(e => e.Sport!.Code == Code);
        var zeilen = await db.RegulationExercises.CountAsync();

        await SportCatalogSeeder.SeedAsync(dienste);

        Assert.Equal(ordnungen, await db.Regulations.CountAsync());
        Assert.Equal(13, uebungen);
        Assert.Equal(uebungen, await db.Exercises.CountAsync(e => e.Sport!.Code == Code));
        Assert.Equal(zeilen, await db.RegulationExercises.CountAsync());
        Assert.Equal(1, await db.Sports.CountAsync(s => s.Code == Code));
    }

    [Fact]
    public async Task Handaenderung_AnOrdnungUndPunkten_BleibtBeimNaechstenLaufStehen()
    {
        var (db, dienste) = await KatalogAsync();
        var ordnung = await db.Regulations.SingleAsync(r => r.SeedKey == "SV-MT 1" && r.Sport!.Code == Code);
        ordnung.Description = "Meine Beschreibung";
        ordnung.EditedAt = DateTimeOffset.UtcNow;
        var zeile = (await ZeilenAsync(db, "SV-MT 1")).First(z => z.Exercise!.Name == "Anzeige der Person");
        zeile.MaxPoints = 25;
        zeile.EditedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(dienste);

        Assert.Equal("Meine Beschreibung", (await db.Regulations.SingleAsync(r => r.Id == ordnung.Id)).Description);
        Assert.Equal(25, (await db.RegulationExercises.SingleAsync(re => re.Id == zeile.Id)).MaxPoints);
    }

    [Fact]
    public async Task OhneHandaenderung_KommtEineKorrekturAusDemSeed()
    {
        var (db, dienste) = await KatalogAsync();
        var zeile = (await ZeilenAsync(db, "DVG MT-II")).First(z => z.Exercise!.Name == "Teamwork");
        zeile.MaxPoints = 99;
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(dienste);

        Assert.Equal(10, (await db.RegulationExercises.SingleAsync(re => re.Id == zeile.Id)).MaxPoints);
    }
}
