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
/// Paket O1: Obedience - die FCI-Klassen 1 bis 3 (gelten überall, englisch) und
/// die Einsteigerklassen von VDH, ÖKV und SKG (national, deutsch) in einer
/// Sportart mit gemeinsamen Übungen. Die Punkte stehen hier noch einmal aus den
/// Übungstabellen der Quellen (Koeffizient x 10), nicht aus dem Seeder abgeleitet.
/// </summary>
public partial class ObedienceKatalogTests
{
    private const string Code = "OBEDIENCE";

    private static readonly Dictionary<string, (string? Land, (string Uebung, int Punkte)[] Uebungen)> Erwartet = new()
    {
        // FCI-Reglement 2025, Tabellen S. 14 f.
        ["FCI-Obedience 1"] = (null,
        [
            ("Sitzen in der Gruppe", 30), ("Freifolge", 40), ("Positionen aus der Bewegung", 30), ("Abrufen", 40),
            ("In ein Quadrat schicken mit Platz", 40), ("Distanzkontrolle", 40),
            ("Apportieren und Sprung über eine Hürde", 40), ("Um eine Pylonengruppe/Tonne herumschicken", 40), ("Gesamteindruck", 20),
        ]),
        ["FCI-Obedience 2"] = (null,
        [
            ("Liegen in der Gruppe", 30), ("Freifolge", 40), ("Positionen aus der Bewegung", 30), ("Abrufen mit Steh", 30),
            ("In ein Quadrat schicken mit Platz und Abrufen", 40), ("Apportieren mit Richtungsanweisung", 30),
            ("Geruchsidentifizierung", 30), ("Distanzkontrolle", 40),
            ("Um eine Pylonengruppe/Tonne herumschicken, Steh/Platz und springen", 30), ("Gesamteindruck", 20),
        ]),
        ["FCI-Obedience 3"] = (null,
        [
            ("Sitzen in der Gruppe", 20), ("Liegen in der Gruppe mit Abrufen", 20), ("Freifolge", 40), ("Positionen aus der Bewegung", 30),
            ("Abrufen mit Steh/Sitz/Platz", 30), ("In ein Quadrat schicken mit Richtungsanweisung, Platz und Abrufen", 40),
            ("Apportieren mit Richtungsanweisung", 30),
            ("Um eine Pylonengruppe/Tonne herumschicken, Steh/Sitz/Platz, apportieren und springen", 40),
            ("Geruchsidentifizierung", 30), ("Distanzkontrolle", 40),
        ]),
        // VDH-PO Obedience 2025, Tabellen S. 19 f.
        ["VDH Obedience Beginner (O-B)"] = ("DE",
        [
            ("Verhalten gegenüber anderen Hunden", 40), ("Liegen in der Gruppe", 30), ("Freifolge", 30), ("Positionen aus der Bewegung", 20),
            ("In ein Quadrat schicken mit Platz", 30), ("Abrufen", 30), ("Sprung über eine Hürde", 30), ("Bringen auf ebener Erde", 30),
            ("Distanzkontrolle", 30), ("Um eine Pylonengruppe/Tonne herumschicken", 30), ("Gesamteindruck", 20),
        ]),
        ["VDH Obedience Senioren (O-S)"] = ("DE",
        [
            ("Liegen in der Gruppe", 40), ("Freifolge", 30), ("Abrufen", 30), ("In ein Quadrat schicken mit Platz", 30),
            ("Positionen aus der Bewegung", 40), ("Apportieren mit Richtungsanweisung", 30),
            ("Um eine Pylonengruppe/Tonne herumschicken", 20), ("Geruchsidentifizierung", 40), ("Distanzkontrolle", 40), ("Gesamteindruck", 20),
        ]),
        // ÖPO 2022, S. 28 und 33
        ["ÖKV Obedience Beginner"] = ("AT",
        [
            ("Verhalten gegenüber anderen Hunden", 40), ("Liegen in der Gruppe", 30), ("Freifolge", 40), ("Positionen aus der Bewegung", 30),
            ("Abrufen", 40), ("In ein Quadrat schicken mit Platz", 40), ("Bringen auf ebener Erde", 40), ("Distanzkontrolle", 40), ("Gesamteindruck", 20),
        ]),
        ["ÖKV Obedience Senior"] = ("AT",
        [
            ("Liegen in der Gruppe", 30), ("Freifolge", 40), ("Positionen aus der Bewegung", 30), ("Abrufen", 40),
            ("In ein Quadrat schicken mit Platz", 40), ("Bringen auf ebener Erde", 40), ("Distanzkontrolle", 40),
            ("Geruchsidentifizierung", 40), ("Gesamteindruck", 20),
        ]),
        // SKG-Reglement Obedience 2025, Kapitel 6 (S. 14-23)
        ["SKG Obedience Beginners"] = ("CH",
        [
            ("Liegen in der Gruppe", 30), ("Freifolge", 40), ("Positionen aus der Bewegung", 30), ("Abrufen", 30),
            ("Sprung über eine Hürde", 30), ("In ein Quadrat schicken mit Platz", 40), ("Bringen auf ebener Erde", 30),
            ("Distanzkontrolle", 40), ("Um eine Pylonengruppe/Tonne herumschicken", 30), ("Gesamteindruck", 20),
        ]),
    };

    private static readonly string[] NurNational = ["Verhalten gegenüber anderen Hunden", "Sprung über eine Hürde", "Bringen auf ebener Erde"];

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

    // ---- Ordnungen, Übungen, Punkte ----

    [Fact]
    public async Task AchtOrdnungen_ProLandMitQuelleVersionUndSeiten()
    {
        await using var db = await KatalogAsync();

        var ordnungen = await db.Regulations.Include(r => r.Versions).Include(r => r.Sport)
            .Where(r => r.Sport!.Code == Code).ToListAsync();

        Assert.Equal(Erwartet.Keys.Order(), ordnungen.Select(o => o.Name).Order());
        Assert.All(ordnungen, o =>
        {
            Assert.Equal(Erwartet[o.Name].Land, o.CountryCode);
            Assert.StartsWith("https://", o.SourceUrl);
            Assert.Single(o.Versions);
            // Jede Ordnung belegt ihre Angaben mit Seiten des Dokuments und nennt die Noten.
            Assert.Contains("S. ", o.Description);
            Assert.Contains("256", o.Description);
            Assert.Contains("192", o.Description);
        });
        Assert.Contains("fci.be", ordnungen.Single(o => o.Name == "FCI-Obedience 1").SourceUrl);
        Assert.Contains("vdh.de", ordnungen.Single(o => o.Name == "VDH Obedience Beginner (O-B)").SourceUrl);
        Assert.Contains("tkamo.ch", ordnungen.Single(o => o.Name == "SKG Obedience Beginners").SourceUrl);
        // Gültigkeitsbeginn laut Dokument.
        Assert.Equal(new DateOnly(2025, 1, 1), ordnungen.Single(o => o.Name == "FCI-Obedience 3").Versions.Single().ValidFrom);
        Assert.Equal(new DateOnly(2025, 1, 1), ordnungen.Single(o => o.Name == "VDH Obedience Senioren (O-S)").Versions.Single().ValidFrom);
        Assert.Equal(new DateOnly(2022, 1, 1), ordnungen.Single(o => o.Name == "ÖKV Obedience Senior").Versions.Single().ValidFrom);
        Assert.Equal(new DateOnly(2025, 1, 1), ordnungen.Single(o => o.Name == "SKG Obedience Beginners").Versions.Single().ValidFrom);
    }

    [Fact]
    public async Task JedeOrdnung_HatDieUebungenUndPunkteDerQuelle_UndSumme320()
    {
        await using var db = await KatalogAsync();

        foreach (var (name, (_, uebungen)) in Erwartet)
        {
            var ist = (await OrdnungsuebungenAsync(db, name)).OrderBy(re => re.SortOrder)
                .Select(re => (re.Exercise!.Name, re.MaxPoints)).ToArray();

            Assert.Equal(uebungen, ist);
            Assert.Equal(320, ist.Sum(u => u.MaxPoints));
            // Punkte = Koeffizient x 10, Koeffizient 2 bis 4.
            Assert.All(ist, u => Assert.True(u.MaxPoints is 20 or 30 or 40, $"{name}/{u.Name}"));
        }
        var anzahl = Erwartet.ToDictionary(e => e.Key, e => e.Value.Uebungen.Length);
        Assert.Equal(9, anzahl["FCI-Obedience 1"]);
        Assert.Equal(10, anzahl["FCI-Obedience 2"]);
        Assert.Equal(10, anzahl["FCI-Obedience 3"]);
        Assert.Equal(11, anzahl["VDH Obedience Beginner (O-B)"]);
        Assert.Equal(10, anzahl["VDH Obedience Senioren (O-S)"]);
        Assert.Equal(9, anzahl["ÖKV Obedience Beginner"]);
        Assert.Equal(9, anzahl["ÖKV Obedience Senior"]);
        Assert.Equal(10, anzahl["SKG Obedience Beginners"]);
    }

    [Fact]
    public async Task JedeUebungDerOrdnungenIstPflichtUndTraegtHinweisMitSeite()
    {
        await using var db = await KatalogAsync();

        var alle = await OrdnungsuebungenAsync(db);

        Assert.Equal(Erwartet.Values.Sum(e => e.Uebungen.Length), alle.Count);
        Assert.All(alle, re =>
        {
            Assert.True(re.IsMandatory, $"{re.RegulationVersion!.Regulation!.Name}/{re.Exercise!.Name}");
            Assert.Contains("S. ", re.ScoringNotes ?? "");
        });
    }

    [Fact]
    public async Task GemeinsameUebungen_SindEineEinzigeUebungDerSportart()
    {
        await using var db = await KatalogAsync();

        var sport = await db.Sports.SingleAsync(s => s.Code == Code);
        var uebungen = await db.Exercises.Where(e => e.SportId == sport.Id).ToListAsync();

        // Keine Übung doppelt, jede hängt an mindestens einer Ordnung.
        Assert.Equal(uebungen.Count, uebungen.Select(u => u.Name).Distinct().Count());
        Assert.Equal(22, uebungen.Count);
        var verwendet = (await OrdnungsuebungenAsync(db)).Select(re => re.ExerciseId).ToHashSet();
        Assert.All(uebungen, u => Assert.Contains(u.Id, verwendet));
        // "Freifolge" und "Gesamteindruck" laufen durch (Gesamteindruck nur in FCI 3 nicht).
        var freifolge = uebungen.Single(u => u.Name == "Freifolge");
        var freifolgeLinks = (await OrdnungsuebungenAsync(db)).Where(re => re.ExerciseId == freifolge.Id).ToList();
        Assert.Equal(8, freifolgeLinks.Select(re => re.RegulationVersion!.Regulation!.Name).Distinct().Count());
        Assert.Equal(7, (await OrdnungsuebungenAsync(db)).Count(re => re.Exercise!.Name == "Gesamteindruck"));
        // Die drei Übungen ohne FCI-Gegenstück sind national.
        foreach (var name in NurNational)
        {
            var namen = (await OrdnungsuebungenAsync(db)).Where(re => re.Exercise!.Name == name)
                .Select(re => re.RegulationVersion!.Regulation!.CountryCode).Distinct().ToList();
            Assert.DoesNotContain(null, namen);
        }
        Assert.All(uebungen, u => Assert.False(string.IsNullOrWhiteSpace(u.Category)));
        Assert.Equal(
            ["Apportieren und Suchen", "Distanzarbeit", "Freifolge und Positionen", "Gesamteindruck", "Gruppenübung"],
            uebungen.Select(u => u.Category!).Distinct().Order());
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
    public async Task Englisch_NurDieFciOrdnungenUndIhreUebungen()
    {
        await using var db = await KatalogAsync();

        var sport = await db.Sports.SingleAsync(s => s.Code == Code);
        Assert.Equal("Obedience", sport.NameEn);
        var ordnungen = await db.Regulations.Where(r => r.SportId == sport.Id).ToListAsync();
        var fci = ordnungen.Where(o => o.CountryCode == null).ToList();
        var national = ordnungen.Where(o => o.CountryCode != null).ToList();
        Assert.Equal(3, fci.Count);
        Assert.Equal(5, national.Count);
        Assert.Equal(["FCI Obedience Class 1", "FCI Obedience Class 2", "FCI Obedience Class 3"], fci.Select(o => o.NameEn!).Order());
        Assert.All(fci, o => Assert.False(string.IsNullOrWhiteSpace(o.DescriptionEn), o.Name));
        Assert.All(national, o =>
        {
            Assert.Null(o.NameEn);
            Assert.Null(o.DescriptionEn);
        });

        var hinweise = await OrdnungsuebungenAsync(db);
        Assert.All(hinweise.Where(h => h.RegulationVersion!.Regulation!.CountryCode == null),
            h => Assert.False(string.IsNullOrWhiteSpace(h.ScoringNotesEn), $"{h.RegulationVersion!.Regulation!.Name}/{h.Exercise!.Name}"));
        // Auch die gemeinsamen Übungen: Die nationale Ordnung zeigt deutsche Hinweise, aber den englischen Namen der Übung.
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

        string[] fci = ["FCI-Obedience 1", "FCI-Obedience 2", "FCI-Obedience 3"];
        Assert.Equal(fci.Concat(["VDH Obedience Beginner (O-B)", "VDH Obedience Senioren (O-S)"]).Order(), await NamenAsync("DE"));
        Assert.Equal(fci.Concat(["ÖKV Obedience Beginner", "ÖKV Obedience Senior"]).Order(), await NamenAsync("AT"));
        Assert.Equal(fci.Concat(["SKG Obedience Beginners"]).Order(), await NamenAsync("CH"));
        Assert.Equal(fci, await NamenAsync("IT"));
        Assert.Equal(Erwartet.Keys.Order(), await NamenAsync(null));
    }

    [Fact]
    public async Task Laenderzaehler_ZaehltDieNeuenOrdnungenMit()
    {
        await using var db = await KatalogAsync();
        var fci = await db.Regulations.CountAsync(r => r.CountryCode == null);
        var laender = (await new SportCatalogService(db).GetCountriesAsync()).Value!;

        Assert.Equal(fci + await db.Regulations.CountAsync(r => r.CountryCode == "DE"), laender.Single(l => l.Code == "DE").RegulationCount);
        Assert.Equal(fci + await db.Regulations.CountAsync(r => r.CountryCode == "CH"), laender.Single(l => l.Code == "CH").RegulationCount);
        Assert.Equal(fci, laender.Single(l => l.Code == "IT").RegulationCount);
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
        var abrufen = await db.Exercises.SingleAsync(e => e.SportId == sport.Id && e.Name == "Abrufen");
        var fci1 = await db.Regulations.SingleAsync(r => r.Name == "FCI-Obedience 1");
        var skg = await db.Regulations.SingleAsync(r => r.Name == "SKG Obedience Beginners");
        abrufen.NameEn = "Come";
        abrufen.ScoringCriteria = "Vom Admin umgeschrieben.";
        abrufen.ScoringCriteriaEn = null;
        fci1.NameEn = "Class 1 (Admin)";
        skg.SourceUrl = "https://admin.example/quelle";
        await db.SaveChangesAsync();

        var vorher = await ZaehlungAsync(db);
        await SportCatalogSeeder.SeedAsync(Dienste(db));
        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        Assert.Equal(vorher, await ZaehlungAsync(db));
        var danach = await db.Exercises.SingleAsync(e => e.Id == abrufen.Id);
        Assert.Equal("Come", danach.NameEn);
        Assert.Equal("Vom Admin umgeschrieben.", danach.ScoringCriteria);
        // Zu einem geänderten deutschen Text gibt es kein passendes Englisch.
        Assert.Null(danach.ScoringCriteriaEn);
        Assert.Equal("Class 1 (Admin)", (await db.Regulations.SingleAsync(r => r.Id == fci1.Id)).NameEn);
        Assert.Equal("https://admin.example/quelle", (await db.Regulations.SingleAsync(r => r.Id == skg.Id)).SourceUrl);
    }

    [Fact]
    public async Task BestehendeSportartenUndUebungen_BleibenUnveraendert()
    {
        // Ein Bestand ohne Obedience: nach dem Seeder ist alles andere Zeile für Zeile gleich.
        var db = InMemoryDbContext.Create();
        var igp = new Sport { Code = "IGP1", Name = "Mein IGP 1" };
        var eigene = new Exercise { SportId = igp.Id, Name = "Freifolge", ScoringCriteria = "Eigener Text" };
        db.Sports.Add(igp);
        db.Exercises.Add(eigene);
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        Assert.Equal("Mein IGP 1", (await db.Sports.SingleAsync(s => s.Id == igp.Id)).Name);
        var freifolge = await db.Exercises.SingleAsync(e => e.Id == eigene.Id);
        Assert.Equal("Eigener Text", freifolge.ScoringCriteria);
        Assert.Equal(igp.Id, freifolge.SportId);
        // Die Obedience-Übung gleichen Namens ist eine andere Zeile einer anderen Sportart.
        var obedienceSport = await db.Sports.SingleAsync(s => s.Code == Code);
        Assert.NotEqual(freifolge.Id, (await db.Exercises.SingleAsync(e => e.SportId == obedienceSport.Id && e.Name == "Freifolge")).Id);
    }

    [Fact]
    public async Task BestehendeSportartenwahl_BekommtObedienceNichtDazu()
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
        // Jemand hat von Hand eine globale Sportart "Obedience" unter anderem Code angelegt, samt eigener Übung.
        var db = InMemoryDbContext.Create();
        var vorhanden = new Sport { Code = "OB", Name = "Obedience" };
        db.Sports.Add(vorhanden);
        var eigene = new Exercise { SportId = vorhanden.Id, Name = "Freifolge", ScoringCriteria = "Eigener Text" };
        var fremd = new Exercise { SportId = vorhanden.Id, Name = "Meine Übung", ScoringCriteria = "Bleibt" };
        db.Exercises.AddRange(eigene, fremd);
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        var sportarten = await db.Sports.Where(s => s.Name == "Obedience").ToListAsync();
        Assert.Equal(vorhanden.Id, Assert.Single(sportarten).Id);
        Assert.Equal("OB", sportarten[0].Code);
        Assert.False(await db.Sports.AnyAsync(s => s.Code == Code));
        // Eigene Übungen bleiben unverändert, die Ordnungen hängen an der vorhandenen Sportart.
        Assert.Equal("Eigener Text", (await db.Exercises.SingleAsync(e => e.Id == eigene.Id)).ScoringCriteria);
        Assert.Equal("Bleibt", (await db.Exercises.SingleAsync(e => e.Id == fremd.Id)).ScoringCriteria);
        Assert.Equal(8, await db.Regulations.CountAsync(r => r.SportId == vorhanden.Id));
        Assert.Equal(1, await db.Exercises.CountAsync(e => e.SportId == vorhanden.Id && e.Name == "Freifolge"));
        // Die FCI-Ordnungen gelten trotz des anderen Codes überall und tragen ihren englischen Namen.
        var fci = await db.Regulations.Where(r => r.SportId == vorhanden.Id && r.Name.StartsWith("FCI")).ToListAsync();
        Assert.Equal(3, fci.Count);
        Assert.All(fci, r =>
        {
            Assert.Null(r.CountryCode);
            Assert.NotNull(r.NameEn);
        });
        Assert.Equal("Obedience", sportarten[0].NameEn);
        Assert.Equal("Heelwork", (await db.Exercises.SingleAsync(e => e.Id == eigene.Id)).NameEn);
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
        var vereinssport = new Sport { Code = Code, Name = "Obedience", ClubId = verein.Id };
        db.Sports.Add(vereinssport);
        var vereinsUebung = new Exercise { SportId = vereinssport.Id, ClubId = verein.Id, Name = "Freifolge", ScoringCriteria = "Vereinstext" };
        db.Exercises.Add(vereinsUebung);
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));
        db.ChangeTracker.Clear();

        // Die globale Sportart entsteht daneben; die des Vereins hat weder Ordnungen noch zusätzliche Übungen.
        var global = await db.Sports.SingleAsync(s => s.Code == Code && s.ClubId == null);
        Assert.NotEqual(vereinssport.Id, global.Id);
        Assert.Equal(8, await db.Regulations.CountAsync(r => r.SportId == global.Id));
        Assert.Equal(0, await db.Regulations.CountAsync(r => r.SportId == vereinssport.Id));
        Assert.Equal([vereinsUebung.Id], await db.Exercises.Where(e => e.SportId == vereinssport.Id).Select(e => e.Id).ToListAsync());
        var unveraendert = await db.Exercises.SingleAsync(e => e.Id == vereinsUebung.Id);
        Assert.Equal("Vereinstext", unveraendert.ScoringCriteria);
        Assert.Null(unveraendert.NameEn);
        Assert.Null((await db.Sports.SingleAsync(s => s.Id == vereinssport.Id)).NameEn);
        Assert.Equal("Obedience", global.NameEn);
    }
}
