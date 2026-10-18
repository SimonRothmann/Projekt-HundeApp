using System.Text.RegularExpressions;
using Dogity.Application.Common;
using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Paket E4: die englischen Texte der FCI-Ordnungen (Bewertungskriterien der
/// Übungen, Beschreibungen der Ordnungen, Bewertungshinweise der Ordnungs-
/// Übungen) - Vollständigkeit im echten Katalog, Abgrenzung zu den nationalen
/// Ordnungen, Zahlen, Spaltenlängen, Idempotenz, Admin-Eingaben, Antworten
/// und Admin-Editor.
/// </summary>
public partial class KatalogTexteEnTests
{
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

    private static async Task<List<Exercise>> FciUebungenAsync(ApplicationDbContext db) =>
        await db.Exercises.Include(e => e.Sport)
            .Where(e => e.ClubId == null && e.Sport != null && FciKatalog.InternationaleSportarten.Contains(e.Sport.Code))
            .ToListAsync();

    private static async Task<List<RegulationExercise>> OrdnungsuebungenAsync(ApplicationDbContext db) =>
        await db.RegulationExercises
            .Include(re => re.Exercise)
            .Include(re => re.RegulationVersion!).ThenInclude(v => v.Regulation!).ThenInclude(r => r.Sport)
            .ToListAsync();

    private static HashSet<string> Zahlen(string text) =>
        ZifferFolge().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\d+")]
    private static partial Regex ZifferFolge();

    // ---- Vollständigkeit und Abgrenzung ----

    [Fact]
    public async Task JedeFciUebungMitDeutscherBewertung_HatEineEnglische_DieOekvEigenenNicht()
    {
        await using var db = await KatalogAsync();
        var uebungen = await FciUebungenAsync(db);

        var mitText = uebungen.Where(e => !string.IsNullOrWhiteSpace(e.ScoringCriteria)).ToList();
        Assert.True(mitText.Count >= 120, $"nur {mitText.Count} FCI-Übungen mit Bewertung");

        // Alle, die einen englischen Namen tragen (= FCI-Übungen), haben auch den Text ...
        Assert.All(mitText.Where(e => e.NameEn is not null),
            e => Assert.False(string.IsNullOrWhiteSpace(e.ScoringCriteriaEn), $"{e.Sport!.Code}/{e.Name}"));
        // ... und was keinen englischen Namen hat, ist national (die zwei ÖKV-Übungen der BH) und bleibt deutsch.
        Assert.Equal(
            ["BH/Freilaufen mit Heranrufen", "BH/Ortsabhängige Begegnung"],
            mitText.Where(e => e.NameEn is null).Select(e => $"{e.Sport!.Code}/{e.Name}").Order());
        Assert.All(mitText.Where(e => e.NameEn is null), e => Assert.Null(e.ScoringCriteriaEn));
    }

    [Fact]
    public async Task JedeFciOrdnungMitDeutscherBeschreibung_HatEineEnglische()
    {
        await using var db = await KatalogAsync();

        var fci = await db.Regulations.Include(r => r.Sport)
            .Where(r => r.CountryCode == null && r.Description != null)
            .ToListAsync();

        Assert.Equal(27, fci.Count);
        Assert.All(fci, r => Assert.False(string.IsNullOrWhiteSpace(r.DescriptionEn), $"{r.Sport!.Code}/{r.Name}"));
    }

    [Fact]
    public async Task JederHinweisEinerFciOrdnungsuebung_HatEinenEnglischen()
    {
        await using var db = await KatalogAsync();

        var fci = (await OrdnungsuebungenAsync(db))
            .Where(re => re.RegulationVersion!.Regulation!.CountryCode == null && !string.IsNullOrWhiteSpace(re.ScoringNotes))
            .ToList();

        Assert.Equal(204, fci.Count);
        Assert.All(fci, re => Assert.False(
            string.IsNullOrWhiteSpace(re.ScoringNotesEn),
            $"{re.RegulationVersion!.Regulation!.Name}/{re.Exercise!.Name}"));
    }

    [Fact]
    public async Task NationaleOrdnungen_BekommenKeinenEnglischenText()
    {
        await using var db = await KatalogAsync();

        // Nationale Sportarten (VDH, ÖKV-Breitensport, SKG-Begleithund) ...
        var nationaleUebungen = await db.Exercises.Include(e => e.Sport)
            .Where(e => e.Sport != null && !FciKatalog.InternationaleSportarten.Contains(e.Sport.Code))
            .ToListAsync();
        Assert.NotEmpty(nationaleUebungen);
        Assert.All(nationaleUebungen, e =>
        {
            Assert.Null(e.DescriptionEn);
            Assert.Null(e.ScoringCriteriaEn);
        });

        // ... jede Ordnung mit Land (DE, AT, CH) - auch die ÖKV-BH unter der FCI-Sportart ...
        var nationaleOrdnungen = await db.Regulations.Where(r => r.CountryCode != null).ToListAsync();
        Assert.Contains(nationaleOrdnungen, r => r.CountryCode == "AT");
        Assert.Contains(nationaleOrdnungen, r => r.CountryCode == "CH");
        Assert.Contains(nationaleOrdnungen, r => r.CountryCode == "DE");
        Assert.All(nationaleOrdnungen, r => Assert.Null(r.DescriptionEn));

        // ... und deren Hinweise, selbst wenn die Übung eine der FCI ist (die ÖKV-BH führt sie auch).
        var nationaleHinweise = (await OrdnungsuebungenAsync(db))
            .Where(re => re.RegulationVersion!.Regulation!.CountryCode != null)
            .ToList();
        Assert.NotEmpty(nationaleHinweise);
        Assert.All(nationaleHinweise, re => Assert.Null(re.ScoringNotesEn));
    }

    /// <summary>
    /// Übungen, die ein frischer Katalog nicht mehr anlegt, die ältere Datenbanken aber noch führen
    /// (die Ordnung kennt sie nicht mehr, Trainingseinträge können daran hängen) - ihre deutschen
    /// Texte haben ebenfalls eine englische Fassung (Paket E4, "Altübungen"). Die vier
    /// Fährten-Übungen fielen erst auf Prod auf: "Eigenfährte vertiefen" hängt dort an
    /// sieben Trainings und blieb deshalb stehen (2026-10-18).
    /// </summary>
    private static readonly (string Sport, string Name)[] Altuebungen =
    [
        ("BH", "Ablegen mit Abrufen"), ("BH", "Sitz aus der Bewegung"),
        ("FAERTE", "Fährtenaufnahme"), ("FAERTE", "Winkelarbeit"),
        ("FAERTE", "Eigenfährte vertiefen"), ("FAERTE", "Fremde Fährte folgen"),
    ];

    [Fact]
    public async Task Textdatei_HatKeineVerwaisteSchluessel()
    {
        await using var db = await KatalogAsync();
        var uebungen = (await FciUebungenAsync(db)).Select(e => (e.Sport!.Code, e.Name)).ToHashSet();
        var ordnungen = (await db.Regulations.Include(r => r.Sport).ToListAsync()).Select(r => (r.Sport!.Code, r.Name)).ToHashSet();
        var hinweise = (await OrdnungsuebungenAsync(db))
            .Select(re => (re.RegulationVersion!.Regulation!.Sport!.Code, re.RegulationVersion.Regulation.Name, re.Exercise!.Name))
            .ToHashSet();

        // Ein Tippfehler im Schlüssel liefe sonst still ins Leere: Der Text würde nie eingetragen.
        var ohneUebung = FciTexteEn.UebungenListe.Where(t => !uebungen.Contains((t.Sport, t.Name))).Select(t => (t.Sport, t.Name)).Distinct();
        Assert.Equal(Altuebungen.Order(), ohneUebung.Order());
        Assert.All(FciTexteEn.OrdnungenListe, t => Assert.Contains((t.Sport, t.Name), ordnungen));
        Assert.All(FciTexteEn.OrdnungsuebungenListe, t => Assert.Contains((t.Sport, t.Ordnung, t.Uebung), hinweise));
        Assert.All(FciTexteEn.OrdnungenListe, t => Assert.Contains(t.Sport, FciKatalog.InternationaleSportarten));
        Assert.All(FciTexteEn.UebungenListe, t => Assert.Contains(t.Sport, FciKatalog.InternationaleSportarten));
        // Je Übung und deutschem Text höchstens ein Eintrag.
        Assert.Equal(FciTexteEn.UebungenListe.Count, FciTexteEn.UebungenListe.Select(t => (t.Sport, t.Name, t.ScoringCriteria)).Distinct().Count());
    }

    [Fact]
    public async Task FrischerKatalog_HatZuJederFciBewertungEinenPassendenEintragInDerDatei()
    {
        await using var db = await KatalogAsync();

        // Die englische Fassung gehört zum deutschen Text des Seeders - nicht zu irgendeinem.
        Assert.All((await FciUebungenAsync(db)).Where(e => e.ScoringCriteriaEn is not null), e =>
            Assert.Contains(FciTexteEn.Uebungen[(e.Sport!.Code, e.Name)], t => t.ScoringCriteria == e.ScoringCriteria && t.ScoringCriteriaEn == e.ScoringCriteriaEn));
    }

    // ---- Inhalt: Zahlen und Länge ----

    [Fact]
    public async Task Zahlen_DesDeutschenTexts_KommenImEnglischenVor()
    {
        await using var db = await KatalogAsync();

        // Grobe Gegenprobe: Punkte, Distanzen, Zeiten, Gewichte stehen in beiden
        // Fassungen. Ausnahmen gibt es keine - auch Seitenangabe und Datum stimmen.
        var fehler = new List<string>();
        void Pruefe(string name, string? de, string? en)
        {
            if (string.IsNullOrWhiteSpace(de)) return;
            var fehlen = Zahlen(de).Except(Zahlen(en ?? "")).ToList();
            if (fehlen.Count > 0) fehler.Add($"{name}: {string.Join(", ", fehlen)} fehlt");
        }

        foreach (var e in (await FciUebungenAsync(db)).Where(e => e.ScoringCriteriaEn is not null))
        {
            Pruefe($"Übung {e.Sport!.Code}/{e.Name}", e.ScoringCriteria, e.ScoringCriteriaEn);
            Pruefe($"Beschreibung {e.Sport.Code}/{e.Name}", e.Description, e.DescriptionEn);
        }
        foreach (var r in await db.Regulations.Where(r => r.DescriptionEn != null).ToListAsync())
            Pruefe($"Ordnung {r.Name}", r.Description, r.DescriptionEn);
        foreach (var re in (await OrdnungsuebungenAsync(db)).Where(re => re.ScoringNotesEn is not null))
            Pruefe($"Hinweis {re.RegulationVersion!.Regulation!.Name}/{re.Exercise!.Name}", re.ScoringNotes, re.ScoringNotesEn);

        Assert.Empty(fehler);
    }

    [Fact]
    public void Zahlen_JederFassungInDerDatei_KommenImEnglischenVor()
    {
        // Auch für die älteren Fassungen, die ein frischer Katalog gar nicht mehr hat.
        var fehler = FciTexteEn.UebungenListe
            .Where(t => t.ScoringCriteria is not null)
            .Where(t => Zahlen(t.ScoringCriteria!).Except(Zahlen(t.ScoringCriteriaEn ?? "")).Any())
            .Select(t => $"{t.Sport}/{t.Name}")
            .ToList();

        Assert.Empty(fehler);
    }

    [Fact]
    public async Task EnglischeTexte_PassenInDieSpalten()
    {
        await using var db = await KatalogAsync();

        Assert.All(await db.Exercises.Where(e => e.ScoringCriteriaEn != null).ToListAsync(),
            e => Assert.True(e.ScoringCriteriaEn!.Length <= Textlaengen.UebungBewertungEn, e.Name));
        Assert.All(await db.Regulations.Where(r => r.DescriptionEn != null).ToListAsync(),
            r => Assert.True(r.DescriptionEn!.Length <= Textlaengen.OrdnungBeschreibungEn, r.Name));
        Assert.All((await OrdnungsuebungenAsync(db)).Where(re => re.ScoringNotesEn != null),
            re => Assert.True(re.ScoringNotesEn!.Length <= Textlaengen.OrdnungsuebungHinweisEn, re.ScoringNotesEn));
    }

    [Fact]
    public async Task EnglischeTexte_SindKeineKopieDesDeutschen_UndNichtLeer()
    {
        await using var db = await KatalogAsync();

        // Schutz vor einer versehentlich eingefügten deutschen Zeile: Umlaute gibt es im Englischen nicht
        // (außer im Kürzel "StöPr" der Stöberprüfung) - und "Hund " ist ein sicheres Kennzeichen.
        var englisch = (await db.Exercises.Where(e => e.ScoringCriteriaEn != null).Select(e => e.ScoringCriteriaEn!).ToListAsync())
            .Concat(await db.Regulations.Where(r => r.DescriptionEn != null).Select(r => r.DescriptionEn!).ToListAsync())
            .Concat((await OrdnungsuebungenAsync(db)).Where(re => re.ScoringNotesEn != null).Select(re => re.ScoringNotesEn!))
            .ToList();

        Assert.NotEmpty(englisch);
        Assert.All(englisch, t =>
        {
            Assert.Equal(t.Trim(), t);
            Assert.DoesNotContain("Hund ", t);
            Assert.DoesNotContain("Punkte", t);
            Assert.False(t.Replace("StöPr", "").Any(c => "äöüÄÖÜß".Contains(c)), t);
        });
    }

    // ---- Seeder: idempotent, Admin-Eingaben bleiben ----

    private static async Task<List<string?>> AlleEnglischenTexteAsync(ApplicationDbContext db) =>
    [
        .. (await db.Exercises.OrderBy(e => e.Name).ThenBy(e => e.SportId).Select(e => e.ScoringCriteriaEn + "|" + e.DescriptionEn).ToListAsync()),
        .. (await db.Regulations.OrderBy(r => r.Name).ThenBy(r => r.SportId).Select(r => r.DescriptionEn).ToListAsync()),
        .. (await db.RegulationExercises.OrderBy(re => re.Id).Select(re => re.ScoringNotesEn).ToListAsync()),
    ];

    [Fact]
    public async Task ZweiterSeederLauf_AendertNichts()
    {
        await using var db = await KatalogAsync();
        var vorher = await AlleEnglischenTexteAsync(db);
        var zeilen = await db.RegulationExercises.CountAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));

        Assert.Equal(zeilen, await db.RegulationExercises.CountAsync());
        Assert.Equal(vorher, await AlleEnglischenTexteAsync(db));
    }

    [Fact]
    public async Task Seeder_UeberschreibtKeineAdminEingabe()
    {
        await using var db = await KatalogAsync();
        var uebung = await db.Exercises.Include(e => e.Sport).FirstAsync(e => e.Sport!.Code == "IGP1" && e.Name == "Freifolge");
        var ordnung = await db.Regulations.FirstAsync(r => r.Name == "FCI-IGP 1");
        var hinweis = (await OrdnungsuebungenAsync(db)).First(re => re.RegulationVersion!.Regulation!.Name == "FCI-IGP 1" && re.Exercise!.Name == "Freifolge");
        uebung.ScoringCriteriaEn = "Von Hand, Bewertung";
        uebung.DescriptionEn = "By hand, description";
        ordnung.DescriptionEn = "By hand, regulation";
        hinweis.ScoringNotesEn = "By hand, note";
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));

        Assert.Equal("Von Hand, Bewertung", (await db.Exercises.FindAsync(uebung.Id))!.ScoringCriteriaEn);
        Assert.Equal("By hand, description", (await db.Exercises.FindAsync(uebung.Id))!.DescriptionEn);
        Assert.Equal("By hand, regulation", (await db.Regulations.FindAsync(ordnung.Id))!.DescriptionEn);
        Assert.Equal("By hand, note", (await db.RegulationExercises.FindAsync(hinweis.Id))!.ScoringNotesEn);
    }

    [Fact]
    public async Task Seeder_FuelltBestehendeZeilenOhneEnglisch_NachUndLaesstFehlendesDeutsch()
    {
        await using var db = await KatalogAsync();
        // Bestehende Datenbank vor Paket E4: die Spalten sind neu und leer.
        foreach (var e in await db.Exercises.ToListAsync()) { e.ScoringCriteriaEn = null; e.DescriptionEn = null; }
        foreach (var r in await db.Regulations.ToListAsync()) r.DescriptionEn = null;
        foreach (var re in await db.RegulationExercises.ToListAsync()) re.ScoringNotesEn = null;
        await db.SaveChangesAsync();
        // Eine deutsche Bewertung ohne Eintrag in der Datei (z. B. vom Admin ergänzt) bleibt ohne Englisch.
        var fremd = new Exercise
        {
            SportId = (await db.Sports.FirstAsync(s => s.Code == "UPR")).Id,
            Name = "Vom Admin ergänzt",
            ScoringCriteria = "Nur deutsch",
        };
        db.Exercises.Add(fremd);
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));

        Assert.Equal(127, await db.Exercises.CountAsync(e => e.ScoringCriteriaEn != null));
        Assert.Equal(27, await db.Regulations.CountAsync(r => r.DescriptionEn != null));
        Assert.Equal(204, await db.RegulationExercises.CountAsync(re => re.ScoringNotesEn != null));
        Assert.Null((await db.Exercises.FindAsync(fremd.Id))!.ScoringCriteriaEn);
    }

    [Fact]
    public async Task Seeder_AeltereDeutscheFassung_BekommtDasZugehoerigeEnglisch_GeaenderteBleibtDeutsch()
    {
        await using var db = await KatalogAsync();
        // Der Seeder überschreibt die Bewertung einer bestehenden Übung nie: Datenbanken, die vor dem
        // geänderten Seed-Text angelegt wurden, tragen noch die ältere Fassung (z. B. BH/Fußarbeit).
        var aeltere = FciTexteEn.Uebungen[("BH", "Fußarbeit")].Single(t => t.ScoringCriteria!.StartsWith("Hund läuft eng"));
        var fussarbeit = await db.Exercises.Include(e => e.Sport).SingleAsync(e => e.Sport!.Code == "BH" && e.Name == "Fußarbeit");
        fussarbeit.ScoringCriteria = aeltere.ScoringCriteria;
        fussarbeit.ScoringCriteriaEn = null;
        // Ein Admin hat den deutschen Text selbst geändert: Das Englisch zum Original passte nicht dazu.
        var geaendert = await db.Exercises.Include(e => e.Sport).SingleAsync(e => e.Sport!.Code == "BH" && e.Name == "Freifolge");
        geaendert.ScoringCriteria = "Vom Admin umgeschrieben.";
        geaendert.ScoringCriteriaEn = null;
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));

        Assert.Equal(aeltere.ScoringCriteriaEn, (await db.Exercises.FindAsync(fussarbeit.Id))!.ScoringCriteriaEn);
        Assert.Contains("close to and attentively beside the handler", aeltere.ScoringCriteriaEn);
        Assert.Null((await db.Exercises.FindAsync(geaendert.Id))!.ScoringCriteriaEn);
    }

    [Fact]
    public async Task Seeder_GeaenderterDeutscherSeedText_ErsetztDasAlteEnglisch()
    {
        await using var db = await KatalogAsync();
        // Der Seeder schreibt Ordnungsbeschreibung und Hinweis bei jedem Start selbst. Wäre das Englisch
        // einer älteren deutschen Fassung stehen geblieben (z. B. eine korrigierte Punktzahl), sähen
        // Englisch-Nutzer falsche Zahlen: Es muss zum neuen deutschen Text gehören oder fehlen.
        var ordnung = await db.Regulations.FirstAsync(r => r.Name == "FCI-IGP 1");
        var richtigeBeschreibung = ordnung.Description;
        var richtigesEnglisch = ordnung.DescriptionEn;
        ordnung.Description = "Ältere Fassung mit 99 Punkten.";
        ordnung.DescriptionEn = "Older version with 99 points.";
        var hinweis = (await OrdnungsuebungenAsync(db)).First(re => re.RegulationVersion!.Regulation!.Name == "FCI-IGP 1" && re.Exercise!.Name == "Freifolge");
        var richtigerHinweis = hinweis.ScoringNotes;
        var richtigesHinweisEnglisch = hinweis.ScoringNotesEn;
        hinweis.ScoringNotes = "Älterer Hinweis, 99 Schritte.";
        hinweis.ScoringNotesEn = "Older note, 99 paces.";
        await db.SaveChangesAsync();

        await SportCatalogSeeder.SeedAsync(Dienste(db));

        var neueOrdnung = await db.Regulations.FindAsync(ordnung.Id);
        Assert.Equal(richtigeBeschreibung, neueOrdnung!.Description);
        Assert.Equal(richtigesEnglisch, neueOrdnung.DescriptionEn);
        var neuerHinweis = await db.RegulationExercises.FindAsync(hinweis.Id);
        Assert.Equal(richtigerHinweis, neuerHinweis!.ScoringNotes);
        Assert.Equal(richtigesHinweisEnglisch, neuerHinweis.ScoringNotesEn);
    }

    [Fact]
    public async Task Textdatei_KenntZuJedemEnglischenTextDenDeutschenQuelltext()
    {
        // Ohne den deutschen Quelltext ließe sich nicht prüfen, ob das Englisch noch zum Seed-Text passt.
        Assert.All(FciTexteEn.OrdnungenListe, t => Assert.False(string.IsNullOrWhiteSpace(t.Description), $"{t.Sport}/{t.Name}"));
        Assert.All(FciTexteEn.OrdnungsuebungenListe, t => Assert.False(string.IsNullOrWhiteSpace(t.ScoringNotes), $"{t.Sport}/{t.Ordnung}/{t.Uebung}"));

        // Und der Quelltext ist der aktuelle des Seeders: Wer einen deutschen Seed-Text ändert, ohne die
        // Datei nachzuziehen, scheitert hier statt still beim Englisch-Nutzer.
        await using var db = await KatalogAsync();
        var ordnungen = await db.Regulations.Include(r => r.Sport).ToListAsync();
        Assert.All(FciTexteEn.OrdnungenListe, t =>
            Assert.Equal(t.Description, ordnungen.Single(r => r.Sport!.Code == t.Sport && r.Name == t.Name).Description));
        var hinweise = await OrdnungsuebungenAsync(db);
        Assert.All(FciTexteEn.OrdnungsuebungenListe, t =>
            Assert.Contains(hinweise, re => re.RegulationVersion!.Regulation!.Sport!.Code == t.Sport
                && re.RegulationVersion.Regulation.Name == t.Ordnung && re.Exercise!.Name == t.Uebung && re.ScoringNotes == t.ScoringNotes));
    }

    // ---- Antworten ----

    [Fact]
    public async Task Katalog_LiefertEnglischeTexteMit()
    {
        await using var db = await KatalogAsync();
        var dienst = new SportCatalogService(db);
        var sport = await db.Sports.FirstAsync(s => s.Code == "IGP1");
        var ordnung = await db.Regulations.FirstAsync(r => r.SportId == sport.Id && r.Name == "FCI-IGP 1");

        var uebungen = (await dienst.GetExercisesAsync(sport.Id, Guid.NewGuid())).Value!;
        var freifolge = uebungen.Single(e => e.Name == "Freifolge");
        Assert.False(string.IsNullOrWhiteSpace(freifolge.ScoringCriteriaEn));
        Assert.Contains("heels off leash", freifolge.ScoringCriteriaEn);

        var liste = (await dienst.GetRegulationsAsync(sport.Id)).Value!;
        Assert.StartsWith("FCI international utility dog examination level 1", liste.Single(r => r.Name == "FCI-IGP 1").DescriptionEn);

        var detail = (await dienst.GetRegulationDetailAsync(ordnung.Id)).Value!;
        Assert.StartsWith("FCI international utility dog examination level 1", detail.Regulation.DescriptionEn);
        Assert.All(detail.Exercises.Where(e => e.ScoringNotes is not null), e => Assert.False(string.IsNullOrWhiteSpace(e.ScoringNotesEn), e.ExerciseName));
    }

    [Fact]
    public async Task Katalog_NationaleOrdnung_LiefertKeineEnglischenTexte()
    {
        await using var db = await KatalogAsync();
        var dienst = new SportCatalogService(db);
        var vdh = await db.Regulations.Include(r => r.Sport).FirstAsync(r => r.Sport!.Code == "AGILITY");

        var detail = (await dienst.GetRegulationDetailAsync(vdh.Id)).Value!;

        Assert.Null(detail.Regulation.DescriptionEn);
        Assert.All(detail.Exercises, e => Assert.Null(e.ScoringNotesEn));
    }

    // ---- Admin-Editor ----

    [Fact]
    public async Task Uebung_AnlegenUndAendern_MitEnglischenTexten_GrenzeUndRecht()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "UPR", Name = "UPr" };
        db.Sports.Add(sport);
        await db.SaveChangesAsync();
        var dienst = new ExerciseManagementService(db);
        var admin = Guid.NewGuid();

        var angelegt = await dienst.CreateAsync(admin, true,
            new CreateExerciseRequest(sport.Id, "Freifolge", "Beschreibung", ExerciseDifficulty.Beginner, null, "Bewertung", null, null, "  Description ", " Scoring "));
        Assert.Equal("Description", angelegt.Value!.DescriptionEn);
        Assert.Equal("Scoring", angelegt.Value.ScoringCriteriaEn);

        var geaendert = await dienst.UpdateAsync(admin, true, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", "Beschreibung", ExerciseDifficulty.Beginner, null, "Bewertung", null, "Other", "   "));
        Assert.Equal("Other", geaendert.Value!.DescriptionEn);
        Assert.Null(geaendert.Value.ScoringCriteriaEn);

        // 1000 Zeichen sind erlaubt, 1001 nicht - dieselbe Grenze wie bei der deutschen Bewertung.
        var genau = await dienst.UpdateAsync(admin, true, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, null, null, new string('x', 1000)));
        Assert.True(genau.Succeeded);
        var zuLang = await dienst.UpdateAsync(admin, true, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, null, null, new string('x', 1001)));
        Assert.False(zuLang.Succeeded);
        var beschreibungZuLang = await dienst.UpdateAsync(admin, true, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, null, new string('x', 2001), null));
        Assert.False(beschreibungZuLang.Succeeded);
        var zuLangNeu = await dienst.CreateAsync(admin, true,
            new CreateExerciseRequest(sport.Id, "Neu", null, ExerciseDifficulty.Beginner, null, null, null, null, null, new string('x', 1001)));
        Assert.False(zuLangNeu.Succeeded);

        var ohneRecht = await dienst.UpdateAsync(Guid.NewGuid(), false, angelegt.Value.Id,
            new UpdateExerciseRequest("Freifolge", null, ExerciseDifficulty.Beginner, null, null, null, "Hijack", "Hijack"));
        Assert.False(ohneRecht.Succeeded);
        var gespeichert = await db.Exercises.SingleAsync(e => e.Name == "Freifolge");
        // Der Aufruf mit 1000 Zeichen hatte keine Beschreibung mitgeschickt: Leer löscht, wie beim englischen Namen.
        Assert.Null(gespeichert.DescriptionEn);
        Assert.Equal(new string('x', 1000), gespeichert.ScoringCriteriaEn);
    }

    [Fact]
    public async Task Ordnung_EnglischeBeschreibung_SetzenGrenzeUndRecht()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "UPR", Name = "UPr" };
        db.Sports.Add(sport);
        var ordnung = new Regulation { SportId = sport.Id, Name = "FCI-UPr 1", Description = "Beschreibung" };
        db.Regulations.Add(ordnung);
        await db.SaveChangesAsync();
        var dienst = new RegulationManagementService(db);

        var gesetzt = await dienst.UpdateRegulationAsync(Guid.NewGuid(), true, ordnung.Id,
            new UpdateRegulationRequest("FCI-UPr 1", "Beschreibung", null, null, null, " Description "));
        Assert.Equal("Description", gesetzt.Value!.DescriptionEn);

        var genau = await dienst.UpdateRegulationAsync(Guid.NewGuid(), true, ordnung.Id,
            new UpdateRegulationRequest("FCI-UPr 1", "Beschreibung", null, null, null, new string('x', 2000)));
        Assert.True(genau.Succeeded);
        var zuLang = await dienst.UpdateRegulationAsync(Guid.NewGuid(), true, ordnung.Id,
            new UpdateRegulationRequest("FCI-UPr 1", "Beschreibung", null, null, null, new string('x', 2001)));
        Assert.False(zuLang.Succeeded);

        var fremd = await dienst.UpdateRegulationAsync(Guid.NewGuid(), false, ordnung.Id,
            new UpdateRegulationRequest("FCI-UPr 1", "Beschreibung", null, null, null, "Hijack"));
        Assert.False(fremd.Succeeded);
        Assert.Equal(new string('x', 2000), (await db.Regulations.SingleAsync()).DescriptionEn);
    }

    [Fact]
    public async Task OrdnungsUebung_EnglischerHinweis_HinzufuegenAendernGrenzeUndRecht()
    {
        var db = InMemoryDbContext.Create();
        var sport = new Sport { Code = "UPR", Name = "UPr" };
        db.Sports.Add(sport);
        var ordnung = new Regulation { SportId = sport.Id, Name = "FCI-UPr 1" };
        db.Regulations.Add(ordnung);
        db.RegulationVersions.Add(new RegulationVersion { RegulationId = ordnung.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) });
        var uebung = new Exercise { SportId = sport.Id, Name = "Freifolge" };
        db.Exercises.Add(uebung);
        await db.SaveChangesAsync();
        var dienst = new RegulationManagementService(db);
        var admin = Guid.NewGuid();

        var hinzu = await dienst.AddRegulationExerciseAsync(admin, true, ordnung.Id,
            new AddRegulationExerciseRequest(uebung.Id, true, 10, "Hinweis", " Note "));
        Assert.Equal("Note", hinzu.Value!.ScoringNotesEn);

        var neu = await dienst.UpdateRegulationExerciseAsync(admin, true, ordnung.Id, uebung.Id,
            new UpdateRegulationExerciseRequest(true, 10, "Hinweis", "Other note"));
        Assert.True(neu.Succeeded);
        Assert.Equal("Other note", (await db.RegulationExercises.SingleAsync()).ScoringNotesEn);

        var zuLang = await dienst.UpdateRegulationExerciseAsync(admin, true, ordnung.Id, uebung.Id,
            new UpdateRegulationExerciseRequest(true, 10, "Hinweis", new string('x', 1001)));
        Assert.False(zuLang.Succeeded);

        var ohneRecht = await dienst.UpdateRegulationExerciseAsync(Guid.NewGuid(), false, ordnung.Id, uebung.Id,
            new UpdateRegulationExerciseRequest(true, 10, "Hinweis", "Hijack"));
        Assert.False(ohneRecht.Succeeded);
        Assert.Equal("Other note", (await db.RegulationExercises.SingleAsync()).ScoringNotesEn);

        var detail = (await new SportCatalogService(db).GetRegulationDetailAsync(ordnung.Id)).Value!;
        Assert.Equal("Other note", detail.Exercises.Single().ScoringNotesEn);
    }
}
