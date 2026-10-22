using System.Text.RegularExpressions;
using Dogity.Application.Common;
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
/// Paket RO1: Rally Obedience - die FCI International Class (gilt überall, englisch) und die
/// Klassen RO B bis RO 3 und RO S von VDH und ÖKV (national, deutsch) in einer Sportart mit
/// gemeinsamen Schildgruppen. Die Schildnummern stehen hier noch einmal aus den Katalogen der
/// Quellen (FCI Kapitel 5, VDH Anhang 2, ÖKV Kapitel 8), nicht aus dem Seeder abgeleitet: Jedes
/// Schild muss in genau einer Gruppe stehen.
/// </summary>
public partial class RallyObedienceKatalogTests
{
    private const string Code = "RALLYO";
    private const string Fci = "FCI-Rally Obedience International";

    private static readonly string[] VdhNamen =
    [
        "VDH Rally Obedience Beginner (RO B)", "VDH Rally Obedience 1 (RO 1)", "VDH Rally Obedience 2 (RO 2)",
        "VDH Rally Obedience 3 (RO 3)", "VDH Rally Obedience Senioren (RO S)",
    ];

    private static readonly string[] OekvNamen =
    [
        "ÖKV Rally Obedience Beginner (RO B)", "ÖKV Rally Obedience 1 (RO 1)", "ÖKV Rally Obedience 2 (RO 2)",
        "ÖKV Rally Obedience 3 (RO 3)", "ÖKV Rally Obedience Senior (RO S)",
    ];

    private static readonly string[] NurNational = ["Bogen und Zickzack", "Mehrschildübung"];

    // ---- Schilder laut den Katalogen der Quellen ----

    private static IEnumerable<string> Reihe(string praefix, int von, int bis) =>
        Enumerable.Range(von, bis - von + 1).Select(n => $"{praefix}{n:D3}");

    /// <summary>FCI Kapitel 5 (S. 19-32): 101-122, 201-222, 301-323, 401-422.</summary>
    private static readonly HashSet<string> FciSchilder =
        [.. Reihe("", 101, 122), .. Reihe("", 201, 222), .. Reihe("", 301, 323), .. Reihe("", 401, 422)];

    /// <summary>VDH Anhang 2: B-001 bis B-036, 1-101 bis 1-130, 2-201 bis 2-234, 3-301 bis 3-332 (S. 30-63).</summary>
    private static readonly Dictionary<string, HashSet<string>> VdhSchilder = new()
    {
        ["RO B"] = [.. Reihe("B-", 1, 36)],
        ["RO 1"] = [.. Reihe("1-", 101, 130)],
        ["RO 2"] = [.. Reihe("2-", 201, 234)],
        ["RO 3"] = [.. Reihe("3-", 301, 332)],
    };

    /// <summary>ÖKV Kapitel 8: B-001 bis B-028, 1-101 bis 1-123, 2-201 bis 2-228, 3-301 bis 3-326 (S. 29-63).</summary>
    private static readonly Dictionary<string, HashSet<string>> OekvSchilder = new()
    {
        ["RO B"] = [.. Reihe("B-", 1, 28)],
        ["RO 1"] = [.. Reihe("1-", 101, 123)],
        ["RO 2"] = [.. Reihe("2-", 201, 228)],
        ["RO 3"] = [.. Reihe("3-", 301, 326)],
    };

    /// <summary>VDH Anhang 3 (Senioren, Auswahl Schilder, S. 64-68): als Bilder, von den Seiten abgelesen.</summary>
    private static readonly HashSet<string> VdhSeniorSchilder =
    [
        "B-001", "B-002", "B-004", "B-005", "B-006", "B-007", "B-008", "B-010", "B-011", "B-013",
        "B-014", "B-015", "B-016", "B-017", "B-018", "B-019", "B-020", "B-021", "B-022", "B-023", "B-024", "B-025", "B-026", "B-028",
        "B-030", "B-031", "B-032", "B-033", "B-034", "B-035", "B-036",
        "1-102", "1-103", "1-104", "1-105", "1-107", "1-108", "1-109", "1-110", "1-111", "1-112", "1-116", "1-117", "1-118", "1-119", "1-120",
        "1-123", "1-124", "1-126", "1-127", "1-128", "1-129", "1-130",
        "2-208", "2-209", "2-211", "2-212", "2-213", "2-214", "2-215", "2-216", "2-220", "2-221", "2-223", "2-225", "2-227",
        "2-230", "2-231", "2-232", "2-233", "3-303", "3-307", "3-312", "3-318", "3-329", "3-332",
    ];

    /// <summary>ÖKV: die mit „Senior“ gekennzeichneten Schilder der Kapitel 8.2 bis 8.5 (Anhang 3, S. 70 f.).</summary>
    private static readonly HashSet<string> OekvSeniorSchilder =
    [
        "B-001", "B-002", "B-004", "B-006", "B-007", "B-008", "B-009", "B-010", "B-012", "B-013", "B-015", "B-017", "B-018",
        "B-020", "B-021", "B-022", "B-023", "B-024",
        "1-102", "1-105", "1-112", "1-114", "1-118", "1-121",
        "2-201", "2-202", "2-205", "2-214", "2-215", "2-216", "2-217", "2-219", "2-221", "2-222", "2-227", "2-228",
        "3-301", "3-303", "3-307", "3-309", "3-312", "3-313", "3-322",
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

    private static async Task<List<RegulationExercise>> OrdnungsuebungenAsync(ApplicationDbContext db, string? ordnung = null) =>
        await db.RegulationExercises
            .Include(re => re.Exercise)
            .Include(re => re.RegulationVersion!).ThenInclude(v => v.Regulation!).ThenInclude(r => r.Sport)
            .Where(re => re.RegulationVersion!.Regulation!.Sport!.Code == Code && (ordnung == null || re.RegulationVersion.Regulation.Name == ordnung))
            .ToListAsync();

    private static HashSet<string> Zahlen(string text) => ZifferFolge().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\d+")]
    private static partial Regex ZifferFolge();

    /// <summary>Die Schildnummern, die ein VDH- oder ÖKV-Hinweis in seiner Liste nennt: Nummer, Leerzeichen, Bezeichnung.</summary>
    [GeneratedRegex(@"\b((?:B|[123])-\d{3})(?:/((?:B|[123])-\d{3}))? (?=[A-ZÄÖÜ0-9])")]
    private static partial Regex NationaleSchilder();

    /// <summary>Die Schildnummern eines FCI-Hinweises: Nummer, Anführungszeichen mit dem englischen Namen.</summary>
    [GeneratedRegex(@"\b(\d{3})(?:/(\d{3}))? „")]
    private static partial Regex FciSchilderImText();

    /// <summary>Beide Nummern eines Treffers: "B-006/B-007" ist ein Eintrag für zwei Schilder.</summary>
    private static IEnumerable<string> Nummern(Match treffer) =>
        new[] { treffer.Groups[1], treffer.Groups[2] }.Where(g => g.Success).Select(g => g.Value);

    private static List<string> SchilderIn(IEnumerable<string?> hinweise, Regex muster) =>
        hinweise.SelectMany(h => muster.Matches(h ?? "").SelectMany(Nummern)).ToList();

    private static string[] Sortiert(params string[][] teile) => teile.SelectMany(t => t).Order().ToArray();

    // ---- Ordnungen und Gruppen ----

    [Fact]
    public async Task ElfOrdnungen_ProLandMitQuelleVersionUndSeiten()
    {
        await using var db = await KatalogAsync();

        var ordnungen = await db.Regulations.Include(r => r.Versions).Include(r => r.Sport)
            .Where(r => r.Sport!.Code == Code).ToListAsync();

        Assert.Equal(Sortiert([Fci], VdhNamen, OekvNamen), ordnungen.Select(o => o.Name).Order());
        Assert.All(ordnungen, o =>
        {
            Assert.StartsWith("https://", o.SourceUrl);
            Assert.Single(o.Versions);
            // Jede Ordnung belegt ihre Angaben mit Seiten des Dokuments und nennt Startwert, Abzüge und Mindestalter.
            Assert.Contains("S. ", o.Description);
            Assert.Contains("100", o.Description);
            Assert.Contains("Mindestalter", o.Description);
        });
        Assert.Null(ordnungen.Single(o => o.Name == Fci).CountryCode);
        Assert.All(ordnungen.Where(o => VdhNamen.Contains(o.Name)), o => Assert.Equal("DE", o.CountryCode));
        Assert.All(ordnungen.Where(o => OekvNamen.Contains(o.Name)), o => Assert.Equal("AT", o.CountryCode));
        Assert.Contains("fci.be", ordnungen.Single(o => o.Name == Fci).SourceUrl);
        Assert.All(ordnungen.Where(o => VdhNamen.Contains(o.Name)), o => Assert.Contains("VDH_RO_RW_2027", o.SourceUrl));
        Assert.All(ordnungen.Where(o => OekvNamen.Contains(o.Name)), o => Assert.Contains("oekv.at", o.SourceUrl));
        // Gültigkeitsbeginn laut Dokument: FCI-Fassung vom 01.02.2025, VDH ab 01.01.2027, ÖKV ab 01.01.2023.
        Assert.Equal(new DateOnly(2025, 2, 1), ordnungen.Single(o => o.Name == Fci).Versions.Single().ValidFrom);
        Assert.All(ordnungen.Where(o => VdhNamen.Contains(o.Name)), o => Assert.Equal(new DateOnly(2027, 1, 1), o.Versions.Single().ValidFrom));
        Assert.All(ordnungen.Where(o => OekvNamen.Contains(o.Name)), o => Assert.Equal(new DateOnly(2023, 1, 1), o.Versions.Single().ValidFrom));
        // Die künftige VDH-Fassung ist als solche gekennzeichnet, die bisherige nur erwähnt.
        Assert.All(ordnungen.Where(o => VdhNamen.Contains(o.Name)), o =>
        {
            Assert.Contains("01.01.2027", o.Description);
            Assert.Contains("31.12.2026", o.Description);
        });
    }

    [Fact]
    public async Task ParcoursAngabenJeKlasse_StehenInDerBeschreibung()
    {
        await using var db = await KatalogAsync();
        var beschreibung = (await db.Regulations.Where(r => r.Sport!.Code == Code).ToListAsync()).ToDictionary(r => r.Name, r => r.Description!);

        // Anzahl der Schilder je Klasse: FCI S. 12, VDH S. 12 f., ÖKV S. 16.
        Assert.Contains("18 bis 20 Schilder", beschreibung[Fci]);
        Assert.Contains("15 bis 18 Übungsschilder", beschreibung[VdhNamen[0]]);
        Assert.Contains("18 bis 20 Übungsschilder", beschreibung[VdhNamen[1]]);
        Assert.Contains("20 bis 22 Übungsschilder", beschreibung[VdhNamen[2]]);
        Assert.Contains("22 bis 24 Übungsschilder", beschreibung[VdhNamen[3]]);
        Assert.Contains("12 bis 14 Übungsschilder", beschreibung[VdhNamen[4]]);
        Assert.Contains("15 bis 18 Übungsschilder", beschreibung[OekvNamen[0]]);
        Assert.Contains("18 bis 20 Übungsschilder", beschreibung[OekvNamen[1]]);
        Assert.Contains("20 bis 22 Übungsschilder", beschreibung[OekvNamen[2]]);
        Assert.Contains("22 bis 24 Übungsschilder", beschreibung[OekvNamen[3]]);
        Assert.Contains("15 bis 18 Übungsschilder", beschreibung[OekvNamen[4]]);
        // Mindestalter: FCI 18 Monate (S. 5); VDH 15 Monate, Senioren ab 8 Jahren (S. 5); ÖKV 12/15/18/18 Monate, 8 Jahre (S. 6).
        Assert.Contains("18 Monate", beschreibung[Fci]);
        Assert.All(VdhNamen.Take(4), n => Assert.Contains("15 Monate", beschreibung[n]));
        Assert.Contains("ab 8 Jahren", beschreibung[VdhNamen[4]]);
        Assert.Contains("12 Monate (S. 6)", beschreibung[OekvNamen[0]]);
        Assert.Contains("15 Monate (S. 6)", beschreibung[OekvNamen[1]]);
        Assert.Contains("18 Monate (S. 6)", beschreibung[OekvNamen[2]]);
        Assert.Contains("18 Monate (S. 6)", beschreibung[OekvNamen[3]]);
        Assert.Contains("8 Jahre (S. 6)", beschreibung[OekvNamen[4]]);
        // Noten und Aufstieg: FCI 90/80/70 (S. 8); VDH und ÖKV: mindestens 90 Punkte oder dreimal mindestens 70.
        Assert.Contains("Excellent 90 bis 100", beschreibung[Fci]);
        Assert.Contains("Good 70 bis 79,99", beschreibung[Fci]);
        Assert.All(VdhNamen.Take(3).Concat(OekvNamen.Take(4)), n => Assert.Contains("90", beschreibung[n]));
        Assert.Contains("dreimal mindestens 70", beschreibung[VdhNamen[0]]);
        Assert.Contains("dreimal mindestens 70", beschreibung[OekvNamen[0]]);
        Assert.Contains("Zeitlimit 4 Minuten", beschreibung[OekvNamen[0]]);
        Assert.Contains("Zeitlimit keines", beschreibung[OekvNamen[4]]);
    }

    [Fact]
    public async Task ZweiundzwanzigUebungenFuerDieFciOrdnung_ProOrdnungDieGruppenDerKlasse()
    {
        await using var db = await KatalogAsync();

        var anzahl = (await OrdnungsuebungenAsync(db)).GroupBy(re => re.RegulationVersion!.Regulation!.Name)
            .ToDictionary(g => g.Key, g => g.Select(re => re.Exercise!.Name).ToList());

        Assert.Equal(22, anzahl[Fci].Count);
        Assert.Equal(
            [15, 19, 23, 24, 22],
            VdhNamen.Select(n => anzahl[n].Count));
        Assert.Equal(
            [15, 18, 21, 23, 19],
            OekvNamen.Select(n => anzahl[n].Count));
        // Die beiden Gruppen, die es nur national gibt, fehlen in der FCI-Ordnung.
        Assert.DoesNotContain(anzahl[Fci], n => NurNational.Contains(n));
        // Klassenunterschiede: Hürde ab RO 2, Position aus der Bewegung ab RO 3, Mehrschildübung nicht in der Beginner-Klasse des VDH.
        string[] huerde = ["Hund über die Hürde senden", "Abruf über die Hürde"];
        foreach (var klasse in new[] { VdhNamen, OekvNamen })
        {
            Assert.DoesNotContain(anzahl[klasse[0]], n => huerde.Contains(n));
            Assert.DoesNotContain(anzahl[klasse[1]], n => huerde.Contains(n));
            Assert.Contains("Hund über die Hürde senden", anzahl[klasse[2]]);
            Assert.Contains("Abruf über die Hürde", anzahl[klasse[2]]);
            Assert.DoesNotContain("Position aus der Bewegung", anzahl[klasse[2]]);
            Assert.Contains("Position aus der Bewegung", anzahl[klasse[3]]);
            Assert.DoesNotContain(anzahl[klasse[4]], n => huerde.Contains(n));
        }
        Assert.DoesNotContain("Mehrschildübung", anzahl[VdhNamen[0]]);
        Assert.Contains("Mehrschildübung", anzahl[VdhNamen[1]]);
        Assert.Contains("Mehrschildübung", anzahl[OekvNamen[0]]);
        // Bogen und Zickzack kennt nur das VDH-Regelwerk.
        Assert.All(VdhNamen, n => Assert.Contains("Bogen und Zickzack", anzahl[n]));
        Assert.All(OekvNamen, n => Assert.DoesNotContain("Bogen und Zickzack", anzahl[n]));
        // Die Fußarbeit zwischen den Schildern wird in jeder Klasse bewertet.
        Assert.All(anzahl.Values, namen => Assert.Contains("Fußarbeit zwischen den Schildern", namen));
    }

    [Fact]
    public async Task Gruppen_SindGemeinsameUebungenDerSportart_OhnePunkte()
    {
        await using var db = await KatalogAsync();

        var sport = await db.Sports.SingleAsync(s => s.Code == Code);
        var uebungen = await db.Exercises.Where(e => e.SportId == sport.Id).ToListAsync();

        // Keine Übung doppelt, jede hängt an mindestens einer Ordnung.
        Assert.Equal(24, uebungen.Count);
        Assert.Equal(uebungen.Count, uebungen.Select(u => u.Name).Distinct().Count());
        var links = await OrdnungsuebungenAsync(db);
        var verwendet = links.Select(re => re.ExerciseId).ToHashSet();
        Assert.All(uebungen, u => Assert.Contains(u.Id, verwendet));
        Assert.All(uebungen, u =>
        {
            Assert.False(string.IsNullOrWhiteSpace(u.Category), u.Name);
            Assert.False(string.IsNullOrWhiteSpace(u.ScoringCriteria), u.Name);
        });
        Assert.Equal(
            ["Distanzarbeit", "Figuren", "Hürde", "Parcours", "Positionen", "Schritte und Seitenwechsel", "Wendungen und Tempo"],
            uebungen.Select(u => u.Category!).Distinct().Order());
        // Gewertet wird über Abzüge vom Startwert 100: keine Übungspunkte (wie Agility und Hoopers).
        Assert.All(links, re => Assert.Equal(0, re.MaxPoints));
        // Jeder Hinweis nennt die Seite des Dokuments.
        Assert.All(links, re => Assert.Contains("S. ", re.ScoringNotes ?? $"{re.Exercise!.Name}: kein Hinweis") );
        // Die nur nationalen Gruppen gehören nur zu nationalen Ordnungen.
        foreach (var name in NurNational)
            Assert.DoesNotContain(null, links.Where(re => re.Exercise!.Name == name).Select(re => re.RegulationVersion!.Regulation!.CountryCode));
        // Gruppen ohne die Pflicht: die Mehrschildübung (VDH: nicht vorgeschrieben, ÖKV RO B: 0 bis 2).
        Assert.All(links.Where(re => !re.IsMandatory), re => Assert.Equal("Mehrschildübung", re.Exercise!.Name));
        Assert.Contains(links, re => !re.IsMandatory && re.RegulationVersion!.Regulation!.Name == OekvNamen[0]);
        Assert.All(links.Where(re => re.Exercise!.Name == "Mehrschildübung" && re.RegulationVersion!.Regulation!.Name == OekvNamen[1]), re => Assert.True(re.IsMandatory));
    }

    [Fact]
    public async Task JedesSchild_StehtInGenauEinerGruppe_JedeKlasseNenntIhreSchilder()
    {
        await using var db = await KatalogAsync();
        var links = await OrdnungsuebungenAsync(db);
        List<string> Schilder(string ordnung, Regex muster) =>
            SchilderIn(links.Where(re => re.RegulationVersion!.Regulation!.Name == ordnung).Select(re => re.ScoringNotes), muster);

        // FCI: alle 89 Schilder, jedes einmal.
        var fci = Schilder(Fci, FciSchilderImText());
        Assert.Equal(89, fci.Count);
        Assert.Equal(FciSchilder.Order(), fci.Order());

        // VDH und ÖKV: Die Hinweise der Klassen B bis 3 nennen die neuen Schilder der Klasse, zusammen alle, jedes einmal.
        string[] klassen = ["RO B", "RO 1", "RO 2", "RO 3"];
        for (var i = 0; i < 4; i++)
        {
            var vdh = Schilder(VdhNamen[i], NationaleSchilder());
            Assert.Equal(VdhSchilder[klassen[i]].Order(), vdh.Order());
            var oekv = Schilder(OekvNamen[i], NationaleSchilder());
            Assert.Equal(OekvSchilder[klassen[i]].Order(), oekv.Order());
        }
        Assert.Equal(132, klassen.Sum(k => VdhSchilder[k].Count));
        Assert.Equal(105, klassen.Sum(k => OekvSchilder[k].Count));

        // Seniorenklassen: die Auswahl des VDH (Anhang 3) und die mit „Senior“ gekennzeichneten Schilder des ÖKV.
        Assert.Equal(VdhSeniorSchilder.Order(), Schilder(VdhNamen[4], NationaleSchilder()).Order());
        Assert.Equal(OekvSeniorSchilder.Order(), Schilder(OekvNamen[4], NationaleSchilder()).Order());
        Assert.Equal(76, VdhSeniorSchilder.Count);
        Assert.Equal(43, OekvSeniorSchilder.Count);
    }

    [Fact]
    public async Task Textlaengen_PassenInDieSpalten()
    {
        await using var db = await KatalogAsync();

        Assert.All(await db.Regulations.Where(r => r.Sport!.Code == Code).ToListAsync(), r =>
        {
            Assert.True(r.Name.Length <= 100, r.Name);
            Assert.True(r.Description!.Length <= 2000, $"{r.Name}: {r.Description.Length}");
            Assert.True((r.SourceUrl ?? "").Length <= 500, r.Name);
            Assert.True((r.NameEn ?? "").Length <= Textlaengen.OrdnungNameEn, r.Name);
            Assert.True((r.DescriptionEn ?? "").Length <= Textlaengen.OrdnungBeschreibungEn, r.Name);
        });
        Assert.All(await OrdnungsuebungenAsync(db), re =>
        {
            Assert.True(re.ScoringNotes!.Length <= 1000, $"{re.ScoringNotes.Length}: {re.ScoringNotes}");
            Assert.True((re.ScoringNotesEn ?? "").Length <= Textlaengen.OrdnungsuebungHinweisEn);
        });
        Assert.All(await db.Exercises.Where(e => e.Sport!.Code == Code).ToListAsync(), e =>
        {
            Assert.True(e.Name.Length <= 150, e.Name);
            Assert.True(e.ScoringCriteria!.Length <= 1000, $"{e.Name}: {e.ScoringCriteria.Length}");
            Assert.True((e.NameEn ?? "").Length <= Textlaengen.UebungNameEn, e.Name);
            Assert.True((e.ScoringCriteriaEn ?? "").Length <= Textlaengen.UebungBewertungEn, e.Name);
        });
    }

    // ---- Englisch: nur FCI ----

    [Fact]
    public async Task Englisch_NurDieFciOrdnungUndIhreGruppen()
    {
        await using var db = await KatalogAsync();

        var sport = await db.Sports.SingleAsync(s => s.Code == Code);
        Assert.Equal("Rally Obedience", sport.NameEn);
        var ordnungen = await db.Regulations.Where(r => r.SportId == sport.Id).ToListAsync();
        var fci = ordnungen.Single(o => o.CountryCode == null);
        Assert.Equal(Fci, fci.Name);
        Assert.Equal("FCI Rally Obedience International Class", fci.NameEn);
        Assert.False(string.IsNullOrWhiteSpace(fci.DescriptionEn));
        Assert.All(ordnungen.Where(o => o.CountryCode != null), o =>
        {
            Assert.Null(o.NameEn);
            Assert.Null(o.DescriptionEn);
        });

        var hinweise = await OrdnungsuebungenAsync(db);
        Assert.All(hinweise.Where(h => h.RegulationVersion!.Regulation!.CountryCode == null),
            h => Assert.False(string.IsNullOrWhiteSpace(h.ScoringNotesEn), h.Exercise!.Name));
        // Auch die gemeinsamen Gruppen: Die nationale Ordnung zeigt deutsche Hinweise, aber den englischen Namen der Gruppe.
        Assert.All(hinweise.Where(h => h.RegulationVersion!.Regulation!.CountryCode != null), h => Assert.Null(h.ScoringNotesEn));

        var uebungen = await db.Exercises.Where(e => e.SportId == sport.Id).ToListAsync();
        Assert.All(uebungen.Where(u => !NurNational.Contains(u.Name)), u =>
        {
            Assert.False(string.IsNullOrWhiteSpace(u.NameEn), u.Name);
            Assert.False(string.IsNullOrWhiteSpace(u.ScoringCriteriaEn), u.Name);
        });
        Assert.All(uebungen.Where(u => NurNational.Contains(u.Name)), u =>
        {
            Assert.Null(u.NameEn);
            Assert.Null(u.ScoringCriteriaEn);
            Assert.Null(u.DescriptionEn);
        });
        // Zwei Übungen derselben Sportart mit gleichem englischem Namen wären in der Auswahl nicht zu unterscheiden.
        var namenEn = uebungen.Where(u => u.NameEn != null).Select(u => u.NameEn).ToList();
        Assert.Equal(22, namenEn.Count);
        Assert.Equal(namenEn.Count, namenEn.Distinct().Count());
    }

    [Fact]
    public async Task Englisch_ZahlenDesDeutschenTextsStehenImEnglischen_UndKeinDeutschesWort()
    {
        await using var db = await KatalogAsync();
        var fehler = new List<string>();
        void Pruefe(string name, string? de, string? en)
        {
            if (string.IsNullOrWhiteSpace(de) || en is null) return;
            var fehlen = Zahlen(de).Except(Zahlen(en)).ToList();
            if (fehlen.Count > 0) fehler.Add($"{name}: {string.Join(", ", fehlen)} fehlt");
            if (en.Contains("Hund ") || en.Contains("Punkte") || en.Any(c => "äöüÄÖÜß".Contains(c))) fehler.Add($"{name}: deutsch im Englischen");
        }

        foreach (var e in await db.Exercises.Where(e => e.Sport!.Code == Code).ToListAsync())
            Pruefe($"Übung {e.Name}", e.ScoringCriteria, e.ScoringCriteriaEn);
        foreach (var o in await db.Regulations.Where(r => r.Sport!.Code == Code).ToListAsync())
            Pruefe($"Ordnung {o.Name}", o.Description, o.DescriptionEn);
        foreach (var re in await OrdnungsuebungenAsync(db))
            Pruefe($"Hinweis {re.RegulationVersion!.Regulation!.Name}/{re.Exercise!.Name}", re.ScoringNotes, re.ScoringNotesEn);

        Assert.Empty(fehler);
    }

    [Fact]
    public async Task EnglischeHinweise_NennenDieSchilderMitDenselbenNummernWieDasDeutsche()
    {
        await using var db = await KatalogAsync();

        var fci = await OrdnungsuebungenAsync(db, Fci);

        // Im deutschen wie im englischen Hinweis stehen dieselben Schildnummern, mit den englischen Original-Bezeichnungen.
        Assert.All(fci, re =>
        {
            var de = SchilderIn([re.ScoringNotes], FciSchilderImText());
            var en = Regex.Matches(re.ScoringNotesEn!, @"\b(\d{3})(?:/(\d{3}))? “").SelectMany(Nummern).ToList();
            Assert.Equal(de, en);
        });
        Assert.Contains(fci, re => re.ScoringNotes!.Contains("„SPIRAL RIGHT/LEFT“"));
    }

    // ---- Länderfilter ----

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
    public async Task Sportart_IstInJedemLandSichtbar_AuchOhneGespeichertesLand()
    {
        await using var db = await KatalogAsync();

        foreach (var land in new string?[] { null, "DE", "AT", "CH", "IT" })
            Assert.Contains(Code, await SichtbareAsync(db, await PersonAsync(db, land)));
        Assert.Contains(Code, await SichtbareAsync(db, null));
        Assert.Contains(Code, await SichtbareAsync(db, await PersonAsync(db, "DE"), alle: true));
    }

    [Fact]
    public async Task Ordnungen_ProLand_FciUeberallDasNationaleNurImEigenenLand()
    {
        await using var db = await KatalogAsync();
        var dienst = new SportCatalogService(db);
        var sport = await db.Sports.SingleAsync(s => s.Code == Code);

        async Task<string[]> NamenAsync(string? land) =>
            (await dienst.GetRegulationsAsync(sport.Id, land)).Value!.Select(r => r.Name).Order().ToArray();

        Assert.Equal(Sortiert([Fci], VdhNamen), await NamenAsync("DE"));
        Assert.Equal(Sortiert([Fci], OekvNamen), await NamenAsync("AT"));
        // Für die Schweiz gibt es kein eigenes SKG-Reglement: Die FCI-Klasse gilt auch dort.
        Assert.Equal(new[] { Fci }, await NamenAsync("CH"));
        Assert.Equal(new[] { Fci }, await NamenAsync("IT"));
        Assert.Equal(Sortiert([Fci], VdhNamen, OekvNamen), await NamenAsync(null));
    }

    [Fact]
    public async Task Laenderzaehler_ZaehltDieNeuenOrdnungenMit()
    {
        await using var db = await KatalogAsync();
        var fci = await db.Regulations.CountAsync(r => r.CountryCode == null);
        var laender = (await new SportCatalogService(db).GetCountriesAsync()).Value!;

        Assert.Equal(fci + await db.Regulations.CountAsync(r => r.CountryCode == "DE"), laender.Single(l => l.Code == "DE").RegulationCount);
        Assert.Equal(fci + await db.Regulations.CountAsync(r => r.CountryCode == "AT"), laender.Single(l => l.Code == "AT").RegulationCount);
        Assert.Equal(fci + await db.Regulations.CountAsync(r => r.CountryCode == "CH"), laender.Single(l => l.Code == "CH").RegulationCount);
        Assert.Equal(fci, laender.Single(l => l.Code == "IT").RegulationCount);
        // Die Verteilung der neuen Sportart nach Land: 1 FCI, 5 VDH, 5 ÖKV.
        var verteilung = (await db.Regulations.Where(r => r.Sport!.Code == Code).Select(r => r.CountryCode).ToListAsync())
            .GroupBy(c => c ?? "-").ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int> { ["-"] = 1, ["DE"] = 5, ["AT"] = 5 }, verteilung);
    }

    // ---- Seeder: Bestand, Idempotenz, Auswahl ----

    private static async Task<string> ZaehlungAsync(ApplicationDbContext db) =>
        string.Join("/", await db.Sports.CountAsync(), await db.Regulations.CountAsync(), await db.RegulationVersions.CountAsync(),
            await db.Exercises.CountAsync(), await db.RegulationExercises.CountAsync());

    [Fact]
    public async Task ZweiterSeederLauf_AendertNichts_UndLaesstAdminEingabenStehen()
    {
        await using var db = await KatalogAsync();
        var sport = await db.Sports.SingleAsync(s => s.Code == Code);
        var slalom = await db.Exercises.SingleAsync(e => e.SportId == sport.Id && e.Name == "Slalom");
        var fci = await db.Regulations.SingleAsync(r => r.Name == Fci);
        var vdh = await db.Regulations.SingleAsync(r => r.Name == VdhNamen[0]);
        slalom.NameEn = "Weave";
        slalom.ScoringCriteria = "Vom Admin umgeschrieben.";
        slalom.ScoringCriteriaEn = null;
        fci.NameEn = "International Class (Admin)";
        vdh.SourceUrl = "https://admin.example/quelle";
        await db.SaveChangesAsync();

        var vorher = await ZaehlungAsync(db);
        await SportCatalogSeeder.SeedAsync(Dienste(db));
        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        Assert.Equal(vorher, await ZaehlungAsync(db));
        var danach = await db.Exercises.SingleAsync(e => e.Id == slalom.Id);
        Assert.Equal("Weave", danach.NameEn);
        Assert.Equal("Vom Admin umgeschrieben.", danach.ScoringCriteria);
        // Zu einem geänderten deutschen Text gibt es kein passendes Englisch.
        Assert.Null(danach.ScoringCriteriaEn);
        Assert.Equal("International Class (Admin)", (await db.Regulations.SingleAsync(r => r.Id == fci.Id)).NameEn);
        Assert.Equal("https://admin.example/quelle", (await db.Regulations.SingleAsync(r => r.Id == vdh.Id)).SourceUrl);
    }

    [Fact]
    public async Task ObedienceUndRallyObedience_SindZweiSportartenMitEigenenUebungen()
    {
        await using var db = await KatalogAsync();

        var obedience = await db.Sports.SingleAsync(s => s.Code == "OBEDIENCE");
        var rally = await db.Sports.SingleAsync(s => s.Code == Code);

        Assert.NotEqual(obedience.Id, rally.Id);
        Assert.Equal("Obedience", obedience.Name);
        Assert.Equal("Rally Obedience", rally.Name);
        Assert.Equal(22, await db.Exercises.CountAsync(e => e.SportId == obedience.Id));
        Assert.Equal(24, await db.Exercises.CountAsync(e => e.SportId == rally.Id));
        Assert.Equal(8, await db.Regulations.CountAsync(r => r.SportId == obedience.Id));
        Assert.Equal(11, await db.Regulations.CountAsync(r => r.SportId == rally.Id));
        // "Slalom" gibt es nur im Rally Obedience, "Freifolge" nur im Obedience.
        Assert.False(await db.Exercises.AnyAsync(e => e.SportId == obedience.Id && e.Name == "Slalom"));
        Assert.False(await db.Exercises.AnyAsync(e => e.SportId == rally.Id && e.Name == "Freifolge"));
    }

    [Fact]
    public async Task BestehendeSportartenUndUebungen_BleibenUnveraendert()
    {
        // Ein Bestand ohne Rally Obedience: nach dem Seeder ist alles andere Zeile für Zeile gleich.
        var db = InMemoryDbContext.Create();
        var eigeneSportart = new Sport { Code = "MEIN", Name = "Mein Sport" };
        var eigene = new Exercise { SportId = eigeneSportart.Id, Name = "Slalom", ScoringCriteria = "Eigener Text" };
        db.Sports.Add(eigeneSportart);
        db.Exercises.Add(eigene);
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        Assert.Equal("Mein Sport", (await db.Sports.SingleAsync(s => s.Id == eigeneSportart.Id)).Name);
        var slalom = await db.Exercises.SingleAsync(e => e.Id == eigene.Id);
        Assert.Equal("Eigener Text", slalom.ScoringCriteria);
        Assert.Equal(eigeneSportart.Id, slalom.SportId);
        // Die Rally-Obedience-Gruppe gleichen Namens ist eine andere Zeile einer anderen Sportart.
        var rally = await db.Sports.SingleAsync(s => s.Code == Code);
        Assert.NotEqual(slalom.Id, (await db.Exercises.SingleAsync(e => e.SportId == rally.Id && e.Name == "Slalom")).Id);
    }

    [Fact]
    public async Task BestehendeSportartenwahl_BekommtRallyObedienceNichtDazu()
    {
        await using var db = await KatalogAsync();
        var nutzer = Guid.NewGuid();
        var igp = await db.Sports.SingleAsync(s => s.Code == "IGP1");
        var einstellung = new UserPreference { UserId = nutzer, Country = "DE" };
        db.UserPreferences.Add(einstellung);
        db.UserSportSelections.Add(new UserSportSelection { UserPreferenceId = einstellung.Id, SportId = igp.Id });
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        var gewaehlt = await db.UserSportSelections.Where(s => s.UserPreferenceId == einstellung.Id).Select(s => s.SportId).ToListAsync();
        Assert.Equal([igp.Id], gewaehlt);
    }

    [Fact]
    public async Task GleichnamigeGlobaleSportart_WirdMitgenutzt_NichtDoppeltAngelegt()
    {
        // Jemand hat von Hand eine globale Sportart "Rally Obedience" unter anderem Code angelegt, samt eigener Übung.
        var db = InMemoryDbContext.Create();
        var vorhanden = new Sport { Code = "RO", Name = "Rally Obedience" };
        db.Sports.Add(vorhanden);
        var eigene = new Exercise { SportId = vorhanden.Id, Name = "Slalom", ScoringCriteria = "Eigener Text" };
        var fremd = new Exercise { SportId = vorhanden.Id, Name = "Meine Übung", ScoringCriteria = "Bleibt" };
        db.Exercises.AddRange(eigene, fremd);
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        var sportarten = await db.Sports.Where(s => s.Name == "Rally Obedience").ToListAsync();
        Assert.Equal(vorhanden.Id, Assert.Single(sportarten).Id);
        Assert.Equal("RO", sportarten[0].Code);
        Assert.False(await db.Sports.AnyAsync(s => s.Code == Code));
        // Eigene Übungen bleiben unverändert, die Ordnungen hängen an der vorhandenen Sportart.
        Assert.Equal("Eigener Text", (await db.Exercises.SingleAsync(e => e.Id == eigene.Id)).ScoringCriteria);
        Assert.Equal("Bleibt", (await db.Exercises.SingleAsync(e => e.Id == fremd.Id)).ScoringCriteria);
        Assert.Equal(11, await db.Regulations.CountAsync(r => r.SportId == vorhanden.Id));
        Assert.Equal(1, await db.Exercises.CountAsync(e => e.SportId == vorhanden.Id && e.Name == "Slalom"));
        // Die FCI-Ordnung gilt trotz des anderen Codes überall und trägt ihren englischen Namen.
        var fci = await db.Regulations.SingleAsync(r => r.SportId == vorhanden.Id && r.Name == Fci);
        Assert.Null(fci.CountryCode);
        Assert.Equal("FCI Rally Obedience International Class", fci.NameEn);
        Assert.Equal("Rally Obedience", sportarten[0].NameEn);
        Assert.Equal("Slalom", (await db.Exercises.SingleAsync(e => e.Id == eigene.Id)).NameEn);
        // Ein zweiter Lauf legt auch hier nichts neu an.
        var vorher = await ZaehlungAsync(db);
        await SportCatalogSeeder.SeedAsync(Dienste(db));
        Assert.Equal(vorher, await ZaehlungAsync(db));
    }

    [Fact]
    public async Task Vereinssportart_MitGleichemCodeUndNamen_BleibtUnberuehrt()
    {
        var db = InMemoryDbContext.Create();
        var verein = new Club { Name = "Verein" };
        db.Clubs.Add(verein);
        var vereinssport = new Sport { Code = Code, Name = "Rally Obedience", ClubId = verein.Id };
        db.Sports.Add(vereinssport);
        var vereinsUebung = new Exercise { SportId = vereinssport.Id, ClubId = verein.Id, Name = "Slalom", ScoringCriteria = "Vereinstext" };
        db.Exercises.Add(vereinsUebung);
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        // Die globale Sportart entsteht daneben; die des Vereins hat weder Ordnungen noch zusätzliche Übungen.
        var global = await db.Sports.SingleAsync(s => s.Code == Code && s.ClubId == null);
        Assert.NotEqual(vereinssport.Id, global.Id);
        Assert.Equal(11, await db.Regulations.CountAsync(r => r.SportId == global.Id));
        Assert.Equal(0, await db.Regulations.CountAsync(r => r.SportId == vereinssport.Id));
        Assert.Equal([vereinsUebung.Id], await db.Exercises.Where(e => e.SportId == vereinssport.Id).Select(e => e.Id).ToListAsync());
        var unveraendert = await db.Exercises.SingleAsync(e => e.Id == vereinsUebung.Id);
        Assert.Equal("Vereinstext", unveraendert.ScoringCriteria);
        Assert.Null(unveraendert.NameEn);
        Assert.Null((await db.Sports.SingleAsync(s => s.Id == vereinssport.Id)).NameEn);
        Assert.Equal("Rally Obedience", global.NameEn);
    }
}
