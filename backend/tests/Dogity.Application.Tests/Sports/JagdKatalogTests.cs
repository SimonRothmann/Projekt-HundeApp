using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Paket JK1: Jagd (JGHV) - VJP und HZP (VZPO 2018), VGP (VGPO 2018), VSwP und VFsP je 20 und 40 Stunden
/// (VSwPO/VFsPO 2026). Die Fachwertziffern und Höchstpunkte stehen hier noch einmal aus den Ordnungen,
/// nicht aus dem Seeder abgeleitet.
/// </summary>
public class JagdKatalogTests
{
    private const string Code = "JAGD";

    private static readonly string[] Schweiss = ["JGHV VSwP 20 h", "JGHV VSwP 40 h", "JGHV VFsP 20 h", "JGHV VFsP 40 h"];

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
    public async Task SiebenOrdnungen_ImLandDeutschlandMitQuelleUndFassung()
    {
        var (db, _) = await KatalogAsync();

        var ordnungen = await db.Regulations.Include(r => r.Versions).Include(r => r.Sport)
            .Where(r => r.Sport!.Code == Code).ToListAsync();

        Assert.Equal(
            new[] { "JGHV VJP", "JGHV HZP", "JGHV VGP", "JGHV VSwP 20 h", "JGHV VSwP 40 h", "JGHV VFsP 20 h", "JGHV VFsP 40 h" }.Order(),
            ordnungen.Select(o => o.Name).Order());
        Assert.All(ordnungen, o =>
        {
            Assert.Equal("DE", o.CountryCode);
            Assert.Contains("jghv.de", o.SourceUrl);
            Assert.Single(o.Versions);
            Assert.Null(o.DescriptionEn);
            Assert.Contains("Zulassung", o.Description);
        });
        Assert.All(ordnungen.Where(o => Schweiss.Contains(o.Name)), o =>
            Assert.Equal(new DateOnly(2026, 12, 1), o.Versions.Single().ValidFrom));
        Assert.All(ordnungen.Where(o => !Schweiss.Contains(o.Name)), o =>
            Assert.Equal(new DateOnly(2018, 1, 1), o.Versions.Single().ValidFrom));
        var sport = await db.Sports.SingleAsync(s => s.Code == Code && s.ClubId == null);
        Assert.Equal("Jagd (JGHV)", sport.Name);
        Assert.DoesNotContain(Code, FciKatalog.InternationaleSportarten);
    }

    [Fact]
    public async Task JedeUebung_HatEineBeschreibungEineKategorieUndEineOrdnung()
    {
        var (db, _) = await KatalogAsync();

        var uebungen = await db.Exercises.Where(e => e.Sport!.Code == Code).ToListAsync();

        Assert.NotEmpty(uebungen);
        Assert.All(uebungen, e => Assert.False(string.IsNullOrWhiteSpace(e.ScoringCriteria), e.Name));
        Assert.All(uebungen, e => Assert.Contains(e.Category, new[] { "Feld", "Wald", "Wasser", "Gehorsam", "Schweiß" }));
        // Jede Übung gehört zu mindestens einer Ordnung, und ein gemeinsames Fach ist dieselbe Übung.
        var verknuepft = await db.RegulationExercises.Select(re => re.ExerciseId).Distinct().ToListAsync();
        Assert.All(uebungen, e => Assert.Contains(e.Id, verknuepft));
        Assert.Equal(1, uebungen.Count(e => e.Name == "Nasengebrauch"));
    }

    [Fact]
    public async Task Vgp_HatDieVierFachgruppenMitDenHoechstpunktenDerOrdnung()
    {
        var (db, _) = await KatalogAsync();

        var z = (await ZeilenAsync(db, "JGHV VGP")).ToDictionary(x => x.Exercise!.Name);
        int Summe(params string[] namen) => namen.Sum(n => z[n].MaxPoints);

        Assert.Equal(124, Summe("Riemenarbeit auf der Übernachtfährte", "Bringen von Fuchs über Hindernis", "Fuchsschleppe",
            "Bringen von Fuchs auf der Schleppe", "Hasen- oder Kaninchenschleppe", "Bringen von Hase oder Kaninchen", "Stöbern", "Buschieren"));
        Assert.Equal(44, Summe("Stöbern ohne Ente im deckungsreichen Gewässer", "Verlorensuchen im deckungsreichen Gewässer",
            "Stöbern mit Ente im deckungsreichen Gewässer", "Bringen von Ente"));
        Assert.Equal(104, Summe("Nasengebrauch", "Suche", "Vorstehen", "Manieren am Wild und Nachziehen", "Arbeit am geflügelten Federwild",
            "Freies Verlorensuchen von Federwild", "Bringen von Federwild"));
        Assert.Equal(68, Summe("Allgemeines Verhalten - Gehorsam", "Verhalten auf dem Stand", "Leinenführigkeit", "Folgen frei bei Fuß", "Ablegen",
            "Benehmen vor eräugtem Federwild", "Benehmen vor eräugtem Haarnutzwild", "Schussruhe"));
        Assert.Equal(340, z.Values.Sum(x => x.MaxPoints));
    }

    [Fact]
    public async Task Vgp_ZusatzUndErsatzfaecher_ZaehlenNichtMitUndSindKeinePflicht()
    {
        var (db, _) = await KatalogAsync();

        var z = (await ZeilenAsync(db, "JGHV VGP")).ToDictionary(x => x.Exercise!.Name);

        foreach (var name in new[] { "Totverbellen", "Totverweisen", "Riemenarbeit auf der Tagfährte", "Federwildschleppe" })
        {
            Assert.False(z[name].IsMandatory, name);
            Assert.Equal(0, z[name].MaxPoints);
        }
        Assert.True(z["Riemenarbeit auf der Übernachtfährte"].IsMandatory);
        Assert.Equal(32, z["Riemenarbeit auf der Übernachtfährte"].MaxPoints);
        Assert.Equal(0, z["Schussfestigkeit"].MaxPoints);
    }

    [Fact]
    public async Task Vzpo_VjpUndHzp_HabenDieFachwertziffernDerOrdnung()
    {
        var (db, _) = await KatalogAsync();

        var vjp = (await ZeilenAsync(db, "JGHV VJP")).ToDictionary(x => x.Exercise!.Name, x => x.MaxPoints);
        var hzp = (await ZeilenAsync(db, "JGHV HZP")).ToDictionary(x => x.Exercise!.Name, x => x.MaxPoints);

        // Ziffer mal 12 (hervorragend möglich), mal 11 (übrige Anlagefächer), mal 10 (Abrichtefächer der HZP).
        Assert.Equal((24, 24, 11, 11, 11, 0), (vjp["Spurarbeit"], vjp["Nasengebrauch"], vjp["Suche"], vjp["Vorstehen"], vjp["Führigkeit"], vjp["Allgemeines Verhalten - Gehorsam"]));
        Assert.Equal((36, 36, 22, 22, 22, 11, 36), (hzp["Spurarbeit"], hzp["Nasengebrauch"], hzp["Suche"], hzp["Vorstehen"], hzp["Führigkeit"], hzp["Arbeitsfreude"], hzp["Stöbern mit Ente im deckungsreichen Gewässer"]));
        Assert.Equal(5, hzp.Values.Count(p => p == 10));
        Assert.Equal(7, vjp.Count);
        Assert.Equal(13, hzp.Count);
    }

    [Theory]
    [InlineData("JGHV VSwP 20 h")]
    [InlineData("JGHV VSwP 40 h")]
    [InlineData("JGHV VFsP 20 h")]
    [InlineData("JGHV VFsP 40 h")]
    public async Task Schweisspruefungen_HabenSechsPflichtfaecherOhnePunkte(string ordnung)
    {
        var (db, _) = await KatalogAsync();

        var zeilen = await ZeilenAsync(db, ordnung);

        Assert.Equal(6, zeilen.Count);
        Assert.All(zeilen, z => Assert.True(z.IsMandatory));
        Assert.All(zeilen, z => Assert.Equal(0, z.MaxPoints));
        Assert.Contains(zeilen, z => z.Exercise!.Name == "Anschuss suchen und ansprechen");
        Assert.Contains(zeilen, z => z.Exercise!.Name == "Verweisen von Wundbetten und Verweiserpunkten");
        Assert.All(zeilen, z => Assert.Equal("Schweiß", z.Exercise!.Category));
    }

    [Theory]
    [InlineData("JGHV VJP", new[] { "Spurarbeit 2", "Nasengebrauch 2", "Suche 1", "mindestens genügend", "Vorjahr" })]
    [InlineData("JGHV HZP", new[] { "Spurarbeit 3", "Stöbern mit Ente 3", "Gehorsam 1", "mindestens genügend" })]
    [InlineData("JGHV VGP", new[] { "ca. 400 m", "zwei stumpfwinklige Haken", "ein Wundbett", "14 Stunden", "254", "220", "163", "31.12.2027", "340" })]
    [InlineData("JGHV VSwP 20 h", new[] { "24 Monate", "Lautnachweis", "1.000 m", "drei nahezu rechtwinklige Haken", "zwei Wundbetten", "vier Verweiserpunkte", "Viertelliter", "20 Stunden", "6 m", "mehr als zwei Rückrufe", "01.12.2026" })]
    [InlineData("JGHV VSwP 40 h", new[] { "40 Stunden", "Viertelliter", "20-Stunden-VSwP" })]
    [InlineData("JGHV VFsP 20 h", new[] { "vier Tropfbetten", "Zehntelliter", "20 Stunden", "24 Monate" })]
    [InlineData("JGHV VFsP 40 h", new[] { "40 Stunden", "Zehntelliter", "20-Stunden-VFsP" })]
    public async Task Ordnungsfakten_StehenInDerBeschreibung(string ordnung, string[] fakten)
    {
        var (db, _) = await KatalogAsync();

        var text = (await db.Regulations.SingleAsync(r => r.Name == ordnung && r.Sport!.Code == Code)).Description;

        Assert.All(fakten, fakt => Assert.Contains(fakt, text));
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
        Assert.Equal(uebungen, await db.Exercises.CountAsync(e => e.Sport!.Code == Code));
        Assert.Equal(zeilen, await db.RegulationExercises.CountAsync());
        Assert.Equal(1, await db.Sports.CountAsync(s => s.Code == Code));
        Assert.Equal(7, await db.Regulations.CountAsync(r => r.Sport!.Code == Code));
    }
}
