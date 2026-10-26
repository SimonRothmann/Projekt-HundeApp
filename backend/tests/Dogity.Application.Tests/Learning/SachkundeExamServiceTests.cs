using System.Data.Common;
using System.Text.Json;
using Dogity.Application.Learning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Learning;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Learning;

/// <summary>
/// Die Übungsprüfung im Dienst: ziehen ohne Lösungen, abgeben mit Prüfung der
/// Form, speichern nur für Angemeldete, und was die Prüfung am Lernstand ändert
/// (falsche in den Fehlerspeicher, richtige nicht).
/// </summary>
public class SachkundeExamServiceTests
{
    private const string Code = "TEST";

    private sealed record Aufbau(
        SachkundeService Dienst, ApplicationDbContext Db, QuizCatalog Katalog, List<QuizQuestion> Fragen);

    /// <summary>
    /// Ein Katalog aus Abschnitten, je Frage "richtig" und drei falsche Antworten.
    /// <paramref name="pruefungsgroesse"/> null = keine Übungsprüfung.
    /// </summary>
    private static Aufbau Katalog(int? pruefungsgroesse, params (string Abschnitt, int Anzahl)[] abschnitte) =>
        Katalog(InMemoryDbContext.Create(), pruefungsgroesse, abschnitte);

    private static Aufbau Katalog(ApplicationDbContext db, int? pruefungsgroesse, params (string Abschnitt, int Anzahl)[] abschnitte)
    {
        var katalog = new QuizCatalog { Code = Code, Name = "Test", Publisher = "Test", ExamQuestionCount = pruefungsgroesse };
        db.QuizCatalogs.Add(katalog);

        var fragen = new List<QuizQuestion>();
        var nr = 0;
        foreach (var (abschnitt, anzahl) in abschnitte)
        {
            for (var i = 0; i < anzahl; i++)
            {
                nr++;
                var frage = new QuizQuestion
                {
                    CatalogId = katalog.Id, Catalog = katalog, Number = $"{abschnitt}{nr}", Section = abschnitt,
                    SectionName = $"Komplex {abschnitt}", SortOrder = nr, Text = $"Frage {nr}?",
                    Kind = QuizQuestionKind.SingleChoice,
                };
                frage.Options.Add(new QuizOption { Question = frage, Kind = QuizOptionKind.Answer, Text = "richtig", IsCorrect = true, SortOrder = 1 });
                for (var f = 2; f <= 4; f++)
                    frage.Options.Add(new QuizOption { Question = frage, Kind = QuizOptionKind.Answer, Text = $"falsch {f}", SortOrder = f });
                db.QuizQuestions.Add(frage);
                db.QuizOptions.AddRange(frage.Options);
                fragen.Add(frage);
            }
        }

        db.SaveChanges();
        return new Aufbau(new SachkundeService(db, TimeProvider.System), db, katalog, fragen);
    }

    private static Guid Richtige(QuizQuestion f) => f.Options.Single(o => o.IsCorrect).Id;
    private static Guid Falsche(QuizQuestion f) => f.Options.First(o => !o.IsCorrect).Id;

    /// <summary>Die ersten <paramref name="richtig"/> Fragen der Prüfung richtig, den Rest falsch beantwortet.</summary>
    private static List<QuizExamAnswerDto> Antworten(Aufbau a, QuizExamDto pruefung, int richtig) =>
        pruefung.Questions.Select((q, i) =>
        {
            var frage = a.Fragen.Single(f => f.Id == q.Id);
            return new QuizExamAnswerDto(q.Id, [i < richtig ? Richtige(frage) : Falsche(frage)], null);
        }).ToList();

    private static async Task<QuizExamDto> ZiehenAsync(Aufbau a) =>
        (await a.Dienst.StartExamAsync(Code)).Value!;

    // ---- Katalog-Info ----

    [Fact]
    public async Task Katalog_TraegtDieInfoZurUebungspruefung()
    {
        var a = Katalog(25, ("A", 30));

        var info = (await a.Dienst.GetCatalogsAsync()).Value!.Single().Exam;

        Assert.NotNull(info);
        Assert.Equal(25, info!.QuestionCount);
        Assert.Equal(50, info.MaxPoints);
        Assert.Equal(35, info.PassPoints);
        Assert.Equal(70, info.PassPercent);
    }

    [Fact]
    public async Task Katalog_OhnePruefungsgroesse_HatKeineInfo()
    {
        var a = Katalog(null, ("A", 30));

        Assert.Null((await a.Dienst.GetCatalogsAsync()).Value!.Single().Exam);
    }

    [Fact]
    public async Task Katalog_Info_ZaehltNurWertbareFragen()
    {
        var a = Katalog(40, ("J", 10));
        // Freitext zählt nicht mit: 10 wertbar + 1 Freitext, verlangt 40 -> 10 Fragen.
        a.Db.QuizQuestions.Add(new QuizQuestion
        {
            CatalogId = a.Katalog.Id, Number = "J99", Section = "J", Text = "Frei?", Kind = QuizQuestionKind.FreeText,
            SampleSolution = "Lösung",
        });
        a.Db.SaveChanges();

        var info = (await a.Dienst.GetCatalogsAsync()).Value!.Single().Exam!;

        Assert.Equal(10, info.QuestionCount);
        Assert.Equal(20, info.MaxPoints);
        Assert.Equal(14, info.PassPoints);
    }

    // ---- Ziehen ----

    [Fact]
    public async Task Start_ZiehtDieVerlangteAnzahl_AnteiligNachKomplex()
    {
        var a = Katalog(6, ("A", 6), ("B", 4), ("C", 2));

        var pruefung = await ZiehenAsync(a);

        Assert.Equal(6, pruefung.QuestionCount);
        Assert.Equal(12, pruefung.MaxPoints);
        Assert.Equal(9, pruefung.PassPoints);
        Assert.Equal(70, pruefung.PassPercent);
        var jeKomplex = pruefung.Questions.GroupBy(q => q.Section).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(3, jeKomplex["A"]);
        Assert.Equal(2, jeKomplex["B"]);
        Assert.Equal(1, jeKomplex["C"]);
    }

    [Fact]
    public async Task Start_VerraetKeineLoesung()
    {
        var a = Katalog(5, ("A", 10));
        a.Fragen[0].SampleSolution = "GEHEIME-MUSTERLOESUNG";
        a.Db.SaveChanges();

        var pruefung = await ZiehenAsync(a);
        var json = JsonSerializer.Serialize(pruefung);

        // Weder ein Kennzeichen an den Antworten noch eine Musterlösung noch
        // ein Zuordnungs-Schlüssel gehören ins Prüfungsblatt.
        Assert.DoesNotContain("IsCorrect", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SampleSolution", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SolutionKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GEHEIME-MUSTERLOESUNG", json);
    }

    [Fact]
    public async Task Start_ZuordnungBringtNurDieWaehlbarenSchluessel()
    {
        var a = Katalog(1, ("A", 0));
        var frage = new QuizQuestion
        {
            CatalogId = a.Katalog.Id, Number = "A1", Section = "A", SectionName = "A", SortOrder = 1,
            Text = "Ordnen Sie zu:", Kind = QuizQuestionKind.Assignment,
        };
        frage.Options.Add(new QuizOption { Question = frage, Kind = QuizOptionKind.Term, Text = "Boxer", MatchKey = "E", SortOrder = 101 });
        frage.Options.Add(new QuizOption { Question = frage, Kind = QuizOptionKind.Term, Text = "Basset", MatchKey = "C", SortOrder = 102 });
        a.Db.QuizQuestions.Add(frage);
        a.Db.QuizOptions.AddRange(frage.Options);
        a.Db.SaveChanges();

        var pruefung = await ZiehenAsync(a);

        var gezogen = Assert.Single(pruefung.Questions);
        Assert.Equal(2, gezogen.Terms.Count);
        Assert.Equal(["C", "E"], gezogen.Keys.Select(k => k.Key));
        Assert.DoesNotContain("SolutionKey", JsonSerializer.Serialize(pruefung), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Start_LaesstAntwortenInKatalogreihenfolge()
    {
        var a = Katalog(3, ("A", 3));

        var pruefung = await ZiehenAsync(a);

        foreach (var q in pruefung.Questions)
            Assert.Equal(["richtig", "falsch 2", "falsch 3", "falsch 4"], q.Options.Select(o => o.Text));
    }

    [Fact]
    public async Task Start_KatalogOhnePruefung_IstNichtGefunden()
    {
        var a = Katalog(null, ("A", 5));

        var ergebnis = await a.Dienst.StartExamAsync(Code);

        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Start_UnbekannterKatalog_IstNichtGefunden()
    {
        var a = Katalog(5, ("A", 5));

        var ergebnis = await a.Dienst.StartExamAsync("GIBT-ES-NICHT");

        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Start_KatalogcodeIstGrossKleinUnempfindlich()
    {
        var a = Katalog(2, ("A", 5));

        Assert.True((await a.Dienst.StartExamAsync("test")).Succeeded);
    }

    // ---- Abgeben: Prüfung der Form ----

    [Fact]
    public async Task Abgabe_MitFalscherAnzahl_WirdAbgewiesen()
    {
        var a = Katalog(5, ("A", 10));
        var pruefung = await ZiehenAsync(a);

        var zuWenig = await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 5).Take(4).ToList());
        var zuViel = await a.Dienst.SubmitExamAsync(null, Code,
            [.. Antworten(a, pruefung, 5), new QuizExamAnswerDto(a.Fragen.First(f => pruefung.Questions.All(q => q.Id != f.Id)).Id, [], null)]);

        Assert.False(zuWenig.Succeeded);
        Assert.False(zuViel.Succeeded);
        Assert.False(zuWenig.IsNotFound); // 400, nicht 404
        Assert.Contains("5 Fragen", zuWenig.Errors.Single());
    }

    [Fact]
    public async Task Abgabe_MitDoppelterFrage_WirdAbgewiesen()
    {
        var a = Katalog(3, ("A", 10));
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 3);
        antworten[2] = antworten[1];

        var ergebnis = await a.Dienst.SubmitExamAsync(null, Code, antworten);

        Assert.False(ergebnis.Succeeded);
        Assert.Contains("doppelt", ergebnis.Errors.Single());
    }

    [Fact]
    public async Task Abgabe_MitFrageAusAnderemKatalog_WirdAbgewiesen()
    {
        var a = Katalog(3, ("A", 10));
        var fremderKatalog = new QuizCatalog { Code = "ANDERS", Name = "Anders", Publisher = "Test" };
        var fremdeFrage = new QuizQuestion
        {
            CatalogId = fremderKatalog.Id, Catalog = fremderKatalog, Number = "X1", Section = "X", Text = "?",
            Kind = QuizQuestionKind.SingleChoice,
        };
        fremdeFrage.Options.Add(new QuizOption { Question = fremdeFrage, Text = "ja", IsCorrect = true });
        a.Db.QuizCatalogs.Add(fremderKatalog);
        a.Db.QuizQuestions.Add(fremdeFrage);
        a.Db.QuizOptions.AddRange(fremdeFrage.Options);
        a.Db.SaveChanges();

        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 3);
        antworten[0] = new QuizExamAnswerDto(fremdeFrage.Id, [fremdeFrage.Options.Single().Id], null);

        var ergebnis = await a.Dienst.SubmitExamAsync(null, Code, antworten);

        Assert.False(ergebnis.Succeeded);
        Assert.Contains("gehört nicht zu dieser Übungsprüfung", ergebnis.Errors.Single());
    }

    [Fact]
    public async Task Abgabe_MitFreitextfrage_WirdAbgewiesen()
    {
        var a = Katalog(3, ("A", 10));
        var freitext = new QuizQuestion
        {
            CatalogId = a.Katalog.Id, Number = "D5", Section = "D", Text = "Nennen Sie ...", Kind = QuizQuestionKind.FreeText,
            SampleSolution = "Lösung",
        };
        a.Db.QuizQuestions.Add(freitext);
        a.Db.SaveChanges();

        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 3);
        antworten[0] = new QuizExamAnswerDto(freitext.Id, [], null);

        var ergebnis = await a.Dienst.SubmitExamAsync(null, Code, antworten);

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Abgabe_MitAntwortAusAndererFrage_WirdAbgewiesen()
    {
        var a = Katalog(3, ("A", 10));
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 3);
        var andere = a.Fragen.First(f => f.Id != antworten[0].QuestionId && f.Id != antworten[1].QuestionId && f.Id != antworten[2].QuestionId);
        antworten[0] = antworten[0] with { SelectedOptionIds = [Richtige(andere)] };

        var ergebnis = await a.Dienst.SubmitExamAsync(null, Code, antworten);

        Assert.False(ergebnis.Succeeded);
        Assert.Contains("Antwort gehört nicht zu dieser Frage", ergebnis.Errors.Single());
    }

    [Fact]
    public async Task Abgabe_ZuordnungMitFremdemBegriff_WirdAbgewiesen()
    {
        var a = Katalog(1, ("A", 0));
        var frage = ZuordnungsFrage(a);

        var ergebnis = await a.Dienst.SubmitExamAsync(null, Code,
            [new QuizExamAnswerDto(frage.Id, null, new Dictionary<Guid, string> { [Guid.NewGuid()] = "E" })]);

        Assert.False(ergebnis.Succeeded);
        Assert.Contains("Begriff gehört nicht zu dieser Frage", ergebnis.Errors.Single());
    }

    [Fact]
    public async Task Abgabe_KatalogOhnePruefung_IstNichtGefunden()
    {
        var a = Katalog(null, ("A", 5));

        var ergebnis = await a.Dienst.SubmitExamAsync(null, Code, []);

        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Abgabe_HatKatalogWenigerWertbareAlsVerlangt_GenuegenAlle()
    {
        var a = Katalog(40, ("J", 6));
        var pruefung = await ZiehenAsync(a);

        Assert.Equal(6, pruefung.QuestionCount);
        Assert.True((await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 6))).Succeeded);
    }

    private static QuizQuestion ZuordnungsFrage(Aufbau a)
    {
        var frage = new QuizQuestion
        {
            CatalogId = a.Katalog.Id, Number = "A18", Section = "A", SectionName = "A", SortOrder = 1,
            Text = "Ordnen Sie zu:", Kind = QuizQuestionKind.Assignment,
        };
        frage.Options.Add(new QuizOption { Question = frage, Kind = QuizOptionKind.Term, Text = "Boxer", MatchKey = "E", SortOrder = 101 });
        frage.Options.Add(new QuizOption { Question = frage, Kind = QuizOptionKind.Term, Text = "Basset", MatchKey = "C", SortOrder = 102 });
        a.Db.QuizQuestions.Add(frage);
        a.Db.QuizOptions.AddRange(frage.Options);
        a.Db.SaveChanges();
        return frage;
    }

    // ---- Abgeben: Wertung ----

    [Fact]
    public async Task Wertung_PunkteUndBestehensgrenze_ErwachsenenGroesse()
    {
        // 25 Fragen: 18 richtig = 36 von 50 (bestanden), 17 richtig = 34 (nicht bestanden).
        var a = Katalog(25, ("A", 30));
        var pruefung = await ZiehenAsync(a);

        var knappBestanden = (await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 18))).Value!;
        var knappDurch = (await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 17))).Value!;

        Assert.Equal(36, knappBestanden.Points);
        Assert.Equal(50, knappBestanden.MaxPoints);
        Assert.Equal(35, knappBestanden.PassPoints);
        Assert.True(knappBestanden.Passed);
        Assert.Equal(72, knappBestanden.Percent);
        Assert.Equal(34, knappDurch.Points);
        Assert.False(knappDurch.Passed);
        Assert.Equal(68, knappDurch.Percent);
    }

    [Fact]
    public async Task Wertung_Jugend15Von40Fragen_AbEinundzwanzigPunkten()
    {
        // 15 von 40: 30 Punkte, Grenze 21 (70 % von 30) - bei 2 Punkten je Frage also
        // 11 richtige (22 Punkte) bestanden, 10 richtige (20 Punkte) nicht.
        var a = Katalog(15, ("J", 40));
        var pruefung = await ZiehenAsync(a);

        var ergebnis11 = (await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 11))).Value!;
        var ergebnis10 = (await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 10))).Value!;

        Assert.Equal(15, pruefung.QuestionCount);
        Assert.Equal(30, ergebnis11.MaxPoints);
        Assert.Equal(21, ergebnis11.PassPoints);
        Assert.Equal(22, ergebnis11.Points);
        Assert.True(ergebnis11.Passed);
        Assert.Equal(20, ergebnis10.Points);
        Assert.False(ergebnis10.Passed);
    }

    [Fact]
    public async Task Jugend_ZiehtFuenfzehnVerschiedeneAusVierzig_JedeZiehungAnders()
    {
        var a = Katalog(15, ("J", 40));

        var ziehungen = new List<QuizExamDto>();
        for (var i = 0; i < 3; i++)
            ziehungen.Add(await ZiehenAsync(a));

        Assert.All(ziehungen, z =>
        {
            Assert.Equal(15, z.Questions.Count);
            Assert.Equal(15, z.Questions.Select(q => q.Id).Distinct().Count());
            Assert.All(z.Questions, q => Assert.Contains(a.Fragen, f => f.Id == q.Id));
        });
        // Zufall: nicht alle drei Ziehungen dieselbe Menge (Gleichheit: 1 : 10^18).
        Assert.True(ziehungen.Select(z => string.Join(',', z.Questions.Select(q => q.Id).Order())).Distinct().Count() > 1);

        // Die Abgabe erwartet genau 15 (Prüfungsgröße), nicht die 40 des Katalogs.
        Assert.True((await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, ziehungen[0], 15))).Succeeded);
        var alle = a.Fragen.Select(f => new QuizExamAnswerDto(f.Id, [], null)).ToList();
        Assert.False((await a.Dienst.SubmitExamAsync(null, Code, alle)).Succeeded);
    }

    [Fact]
    public async Task Abgabe_LeerOderOhneAntworten_WirdAbgewiesen()
    {
        var a = Katalog(5, ("A", 10));

        var leer = await a.Dienst.SubmitExamAsync(null, Code, []);
        var keine = await a.Dienst.SubmitExamAsync(null, Code, null!);

        Assert.False(leer.Succeeded);
        Assert.False(leer.IsNotFound);
        Assert.False(keine.Succeeded);
    }

    [Fact]
    public async Task Wertung_EineFalscheAntwortKostetNichtsExtra_UnbeantwortetAuchNicht()
    {
        var a = Katalog(4, ("A", 4));
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 2);       // 2 richtig, 2 falsch
        antworten[3] = antworten[3] with { SelectedOptionIds = [] };   // eine davon unbeantwortet

        var ergebnis = (await a.Dienst.SubmitExamAsync(null, Code, antworten)).Value!;

        Assert.Equal(4, ergebnis.Points);   // kein Minus je Frage
        Assert.Equal(8, ergebnis.MaxPoints);
        Assert.Equal(2, ergebnis.CorrectCount);
    }

    [Fact]
    public async Task Wertung_AllesAngekreuzt_BringtNullPunkte()
    {
        var a = Katalog(1, ("A", 1));
        var pruefung = await ZiehenAsync(a);
        var frage = pruefung.Questions.Single();

        var ergebnis = (await a.Dienst.SubmitExamAsync(null, Code,
            [new QuizExamAnswerDto(frage.Id, frage.Options.Select(o => o.Id).ToList(), null)])).Value!;

        Assert.Equal(0, ergebnis.Points);
        Assert.False(ergebnis.Passed);
    }

    [Fact]
    public async Task Wertung_ZuordnungZaehltGanzOderGarNicht()
    {
        var a = Katalog(1, ("A", 0));
        var frage = ZuordnungsFrage(a);
        var begriffe = frage.Options.OrderBy(o => o.SortOrder).ToList();

        var alleRichtig = (await a.Dienst.SubmitExamAsync(null, Code,
            [new QuizExamAnswerDto(frage.Id, null, new Dictionary<Guid, string> { [begriffe[0].Id] = "E", [begriffe[1].Id] = "C" })])).Value!;
        var teilweise = (await a.Dienst.SubmitExamAsync(null, Code,
            [new QuizExamAnswerDto(frage.Id, null, new Dictionary<Guid, string> { [begriffe[0].Id] = "E", [begriffe[1].Id] = "A" })])).Value!;

        Assert.Equal(2, alleRichtig.Points);
        Assert.Equal(2, alleRichtig.MaxPoints);
        Assert.Equal(0, teilweise.Points);
        // Für die Auswertung: die richtige Zuordnung kommt mit.
        var loesung = teilweise.Questions.Single().CorrectAssignments;
        Assert.Equal("E", loesung[begriffe[0].Id]);
        Assert.Equal("C", loesung[begriffe[1].Id]);
    }

    [Fact]
    public async Task Auswertung_LiefertJeFrageRichtigFalschUndDieRichtigeLoesung()
    {
        var a = Katalog(4, ("A", 4));
        var pruefung = await ZiehenAsync(a);

        var ergebnis = (await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 1))).Value!;

        Assert.Equal(4, ergebnis.Questions.Count);
        Assert.Equal(1, ergebnis.Questions.Count(q => q.Correct));
        foreach (var q in ergebnis.Questions)
            Assert.Equal([Richtige(a.Fragen.Single(f => f.Id == q.QuestionId))], q.CorrectOptionIds);
    }

    [Fact]
    public async Task Auswertung_RechnetPunkteJeKomplex()
    {
        var a = Katalog(6, ("A", 6), ("B", 6));
        var pruefung = await ZiehenAsync(a);   // 3 aus A, 3 aus B
        var antworten = pruefung.Questions
            .Select(q =>
            {
                var frage = a.Fragen.Single(f => f.Id == q.Id);
                // Alle aus A richtig, alle aus B falsch.
                return new QuizExamAnswerDto(q.Id, [q.Section == "A" ? Richtige(frage) : Falsche(frage)], null);
            })
            .ToList();

        var ergebnis = (await a.Dienst.SubmitExamAsync(null, Code, antworten)).Value!;

        Assert.Equal(2, ergebnis.Sections.Count);
        Assert.Equal(new QuizExamSectionResultDto("A", "Komplex A", 6, 6), ergebnis.Sections[0]);
        Assert.Equal(new QuizExamSectionResultDto("B", "Komplex B", 0, 6), ergebnis.Sections[1]);
    }

    // ---- Anonym: nichts wird gespeichert ----

    [Fact]
    public async Task Anonym_WirdGewertetAberNichtsGespeichert()
    {
        var a = Katalog(5, ("A", 10));
        var pruefung = await ZiehenAsync(a);

        var ergebnis = await a.Dienst.SubmitExamAsync(null, Code, Antworten(a, pruefung, 2));

        Assert.True(ergebnis.Succeeded);
        Assert.False(ergebnis.Value!.Saved);
        Assert.Equal(4, ergebnis.Value.Points);
        Assert.Empty(a.Db.QuizExamAttempts.IgnoreQueryFilters());
        Assert.Empty(a.Db.QuizMasteries.IgnoreQueryFilters());
    }

    // ---- Angemeldet: Ergebnis und Fehlerspeicher ----

    [Fact]
    public async Task Angemeldet_SpeichertDasErgebnis()
    {
        var a = Katalog(25, ("A", 30));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);

        var ergebnis = (await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 20))).Value!;

        Assert.True(ergebnis.Saved);
        var gespeichert = await a.Db.QuizExamAttempts.SingleAsync();
        Assert.Equal(nutzer, gespeichert.UserId);
        Assert.Equal(a.Katalog.Id, gespeichert.CatalogId);
        Assert.Equal(40, gespeichert.Points);
        Assert.Equal(50, gespeichert.MaxPoints);
        Assert.True(gespeichert.Passed);
        Assert.Equal(25, gespeichert.QuestionCount);
        Assert.Equal(20, gespeichert.CorrectCount);
        Assert.NotEqual(default, gespeichert.SubmittedAt);
        Assert.Contains("\"Points\":40", gespeichert.SectionResultsJson);
    }

    [Fact]
    public async Task Angemeldet_FalscheKommenInDenFehlerspeicher_WieImLernmodus()
    {
        var a = Katalog(6, ("A", 10));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);

        await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 4));   // 2 falsch

        var falsch = pruefung.Questions.Skip(4).Select(q => q.Id).ToList();
        var stand = await a.Db.QuizMasteries.ToListAsync();
        Assert.Equal(2, stand.Count);
        Assert.All(stand, m =>
        {
            Assert.Contains(m.QuestionId, falsch);
            Assert.Equal(1, m.Box);
            Assert.False(m.LastWasCorrect);
            Assert.Equal(1, m.WrongCount);
            Assert.NotNull(m.DueAt);
            Assert.True(m.DueAt <= DateTimeOffset.UtcNow);   // sofort wieder fällig
        });

        var fortschritt = (await a.Dienst.GetProgressAsync(nutzer, Code)).Value!;
        Assert.Equal(2, fortschritt.InMistakes);
        var fehler = (await a.Dienst.GetSessionAsync(nutzer, Code, "mistakes", 20)).Value!;
        Assert.Equal(falsch.Order(), fehler.Questions.Select(q => q.Id).Order());
    }

    [Fact]
    public async Task Angemeldet_UnbeantworteteFragenKommenAuchInDenFehlerspeicher()
    {
        var a = Katalog(3, ("A", 5));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 3).Select(x => x with { SelectedOptionIds = [] }).ToList();

        await a.Dienst.SubmitExamAsync(nutzer, Code, antworten);

        Assert.Equal(3, (await a.Dienst.GetProgressAsync(nutzer, Code)).Value!.InMistakes);
    }

    [Fact]
    public async Task Angemeldet_RichtigeAendernDenLernstandNicht()
    {
        var a = Katalog(6, ("A", 10));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);

        // Zwei Fragen stehen schon in Fach 3; die Prüfung beantwortet sie richtig.
        var vorbelegt = pruefung.Questions.Take(2).Select(q => q.Id).ToList();
        var faelligkeit = DateTimeOffset.UtcNow.AddDays(4);
        foreach (var id in vorbelegt)
            a.Db.QuizMasteries.Add(new QuizMastery
            {
                UserId = nutzer, QuestionId = id, Box = 3, CorrectCount = 2, LastWasCorrect = true,
                LastAnsweredAt = DateTimeOffset.UtcNow.AddDays(-1), DueAt = faelligkeit,
            });
        a.Db.SaveChanges();

        await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 6));   // alles richtig

        var stand = await a.Db.QuizMasteries.ToListAsync();
        Assert.Equal(2, stand.Count);          // für die übrigen richtigen entstehen keine Zeilen
        Assert.All(stand, m =>
        {
            Assert.Equal(3, m.Box);            // nicht hochgestuft
            Assert.Equal(2, m.CorrectCount);   // nicht mitgezählt
            Assert.Equal(faelligkeit, m.DueAt);
        });
    }

    [Fact]
    public async Task Angemeldet_EineFalscheAntwortSetztEinBestehendesFachZurueck()
    {
        var a = Katalog(3, ("A", 5));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);
        var erste = pruefung.Questions[0].Id;
        a.Db.QuizMasteries.Add(new QuizMastery
        {
            UserId = nutzer, QuestionId = erste, Box = 4, CorrectCount = 3, LastWasCorrect = true,
            LastAnsweredAt = DateTimeOffset.UtcNow.AddDays(-2), DueAt = DateTimeOffset.UtcNow.AddDays(9),
        });
        a.Db.SaveChanges();

        await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 0));   // alles falsch

        var m = await a.Db.QuizMasteries.SingleAsync(x => x.QuestionId == erste);
        Assert.Equal(1, m.Box);
        Assert.Equal(3, m.CorrectCount);   // wie im Lernmodus bleiben frühere richtige Antworten gezählt
        Assert.Equal(1, m.WrongCount);
        Assert.False(m.LastWasCorrect);
        Assert.Equal(3, await a.Db.QuizMasteries.CountAsync());
    }

    [Fact]
    public async Task Angemeldet_FremderLernstandBleibtUnberuehrt()
    {
        var a = Katalog(3, ("A", 5));
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);

        await a.Dienst.SubmitExamAsync(ich, Code, Antworten(a, pruefung, 0));

        Assert.Equal(0, (await a.Dienst.GetProgressAsync(andere, Code)).Value!.InMistakes);
        Assert.Empty(a.Db.QuizExamAttempts.Where(x => x.UserId == andere));
    }

    // ---- Doppelabgabe: dieselbe Kennung legt kein zweites Ergebnis an ----

    [Fact]
    public async Task Start_VergibtJedesMalEineNeueAbgabeKennung()
    {
        var a = Katalog(5, ("A", 10));

        var erste = await ZiehenAsync(a);
        var zweite = await ZiehenAsync(a);

        Assert.NotEqual(Guid.Empty, erste.SubmissionId);
        Assert.NotEqual(erste.SubmissionId, zweite.SubmissionId);
        // Die Kennung wird nicht gespeichert, nur mit dem Ergebnis abgegeben.
        Assert.Empty(a.Db.QuizExamAttempts.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Angemeldet_MitKennung_SpeichertDieKennungAmErgebnis()
    {
        var a = Katalog(5, ("A", 10));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);

        await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 3), pruefung.SubmissionId);

        Assert.Equal(pruefung.SubmissionId, (await a.Db.QuizExamAttempts.SingleAsync()).SubmissionId);
    }

    [Fact]
    public async Task Doppelabgabe_DieselbeKennung_EinErgebnis_GleicheAntwort()
    {
        var a = Katalog(6, ("A", 10));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 4);

        var erste = (await a.Dienst.SubmitExamAsync(nutzer, Code, antworten, pruefung.SubmissionId)).Value!;
        var zweite = (await a.Dienst.SubmitExamAsync(nutzer, Code, antworten, pruefung.SubmissionId)).Value!;

        Assert.Single(a.Db.QuizExamAttempts);
        Assert.Equal(
            (erste.Points, erste.MaxPoints, erste.Passed, erste.CorrectCount, erste.QuestionCount, erste.Saved),
            (zweite.Points, zweite.MaxPoints, zweite.Passed, zweite.CorrectCount, zweite.QuestionCount, zweite.Saved));
        Assert.Equal(erste.Sections.Select(x => (x.Key, x.Points)), zweite.Sections.Select(x => (x.Key, x.Points)));
        Assert.Equal(erste.Questions.Select(x => (x.QuestionId, x.Correct)), zweite.Questions.Select(x => (x.QuestionId, x.Correct)));
        Assert.True(zweite.Saved);
    }

    [Fact]
    public async Task Doppelabgabe_FasstDenFehlerspeicherNichtErneutAn()
    {
        var a = Katalog(6, ("A", 10));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 4);
        await a.Dienst.SubmitExamAsync(nutzer, Code, antworten, pruefung.SubmissionId);

        // Zwischen den Abgaben hat die Person eine der Fragen im Lernmodus richtig gelernt (Fach 3).
        var gelernt = await a.Db.QuizMasteries.FirstAsync();
        gelernt.Box = 3;
        gelernt.LastWasCorrect = true;
        gelernt.WrongCount = 1;
        await a.Db.SaveChangesAsync();

        await a.Dienst.SubmitExamAsync(nutzer, Code, antworten, pruefung.SubmissionId);

        var nachher = await a.Db.QuizMasteries.SingleAsync(m => m.Id == gelernt.Id);
        Assert.Equal(3, nachher.Box);
        Assert.True(nachher.LastWasCorrect);
        Assert.Equal(1, nachher.WrongCount);
        Assert.Equal(2, await a.Db.QuizMasteries.CountAsync());
    }

    [Fact]
    public async Task Doppelabgabe_GespeichertesErgebnisGilt_AuchWennDieWiederholungAndereAntwortenTraegt()
    {
        var a = Katalog(6, ("A", 10));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);
        var erste = (await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 6), pruefung.SubmissionId)).Value!;

        var zweite = (await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 0), pruefung.SubmissionId)).Value!;

        Assert.Equal(12, erste.Points);
        Assert.Equal(12, zweite.Points);
        Assert.True(zweite.Passed);
        Assert.Single(a.Db.QuizExamAttempts);
    }

    [Fact]
    public async Task Doppelabgabe_VerschiedeneKennungen_ZweiErgebnisse()
    {
        var a = Katalog(5, ("A", 10));
        var nutzer = Guid.NewGuid();
        var erste = await ZiehenAsync(a);
        var zweite = await ZiehenAsync(a);

        await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, erste, 2), erste.SubmissionId);
        await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, zweite, 2), zweite.SubmissionId);

        Assert.Equal(2, await a.Db.QuizExamAttempts.CountAsync());
    }

    [Fact]
    public async Task Doppelabgabe_KennungEinesAnderenNutzers_StoertNicht()
    {
        var a = Katalog(5, ("A", 10));
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 2);

        await a.Dienst.SubmitExamAsync(Guid.NewGuid(), Code, antworten, pruefung.SubmissionId);
        var ergebnis = await a.Dienst.SubmitExamAsync(Guid.NewGuid(), Code, antworten, pruefung.SubmissionId);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(2, await a.Db.QuizExamAttempts.CountAsync());
    }

    [Fact]
    public async Task Doppelabgabe_OhneKennung_WieBisher_JedeAbgabeEinErgebnis()
    {
        var a = Katalog(5, ("A", 10));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 2);

        await a.Dienst.SubmitExamAsync(nutzer, Code, antworten);
        await a.Dienst.SubmitExamAsync(nutzer, Code, antworten);

        Assert.Equal(2, await a.Db.QuizExamAttempts.CountAsync());
        Assert.All(a.Db.QuizExamAttempts, x => Assert.Null(x.SubmissionId));
    }

    [Fact]
    public async Task Doppelabgabe_Anonym_UnveraendertGewertetNichtGespeichert()
    {
        var a = Katalog(5, ("A", 10));
        var pruefung = await ZiehenAsync(a);
        var antworten = Antworten(a, pruefung, 2);

        var erste = (await a.Dienst.SubmitExamAsync(null, Code, antworten, pruefung.SubmissionId)).Value!;
        var zweite = (await a.Dienst.SubmitExamAsync(null, Code, antworten, pruefung.SubmissionId)).Value!;

        Assert.False(erste.Saved);
        Assert.False(zweite.Saved);
        Assert.Equal(erste.Points, zweite.Points);
        Assert.Empty(a.Db.QuizExamAttempts.IgnoreQueryFilters());
    }

    // ---- Rennen zweier gleichzeitiger Abgaben (Unique-Verletzung, SqlState 23505) ----

    /// <summary>Eine Datenbankausnahme wie die von Postgres bei einem doppelten Schlüssel.</summary>
    private sealed class EindeutigerIndexVerletzt(string sqlState = "23505") : DbException("Doppelter Schlüssel.")
    {
        public override string? SqlState => sqlState;
    }

    /// <summary>
    /// Ein Context, dessen erstes Speichern an einer schnelleren Abgabe scheitert:
    /// Erst legt "die andere Abgabe" ihr Ergebnis an, dann kommt die Unique-Verletzung -
    /// so, wie es Postgres bei zwei gleichzeitigen Abgaben derselben Kennung täte (der
    /// InMemory-Anbieter prüft keine Indizes). Danach speichert er wie gewohnt.
    /// </summary>
    private sealed class VerlierendesRennenContext(
        DbContextOptions<ApplicationDbContext> options, Func<Task> andereWarSchneller, string sqlState = "23505")
        : ApplicationDbContext(options)
    {
        private bool _ausgeloest;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_ausgeloest) return await base.SaveChangesAsync(cancellationToken);
            _ausgeloest = true;
            await andereWarSchneller();
            throw new DbUpdateException("Speichern fehlgeschlagen.", new EindeutigerIndexVerletzt(sqlState));
        }
    }

    [Fact]
    public async Task Doppelabgabe_Rennen_UniqueVerletzung_LiefertDasErgebnisDerSchnelleren_KeinFehler()
    {
        var optionen = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var nutzer = Guid.NewGuid();
        var kennung = Guid.NewGuid();
        await using var andere = new ApplicationDbContext(optionen);
        Aufbau a = null!;
        a = Katalog(
            new VerlierendesRennenContext(optionen, async () =>
            {
                andere.QuizExamAttempts.Add(new QuizExamAttempt
                {
                    UserId = nutzer, CatalogId = a.Katalog.Id, SubmissionId = kennung, SubmittedAt = DateTimeOffset.UtcNow,
                    Points = 8, MaxPoints = 10, Passed = true, QuestionCount = 5, CorrectCount = 4,
                });
                await andere.SaveChangesAsync();
            }),
            5, ("A", 10));
        var pruefung = await ZiehenAsync(a);

        // Die Antworten dieser Abgabe wären schlechter ausgefallen: Es gilt das Gespeicherte.
        var ergebnis = await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 0), kennung);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(8, ergebnis.Value!.Points);
        Assert.True(ergebnis.Value.Passed);
        Assert.True(ergebnis.Value.Saved);
        Assert.Single(await andere.QuizExamAttempts.ToListAsync());
        // Die Transaktion der Verliererin ist nicht durchgekommen: kein Fehlerspeicher.
        Assert.Empty(await andere.QuizMasteries.ToListAsync());
    }

    [Fact]
    public async Task Doppelabgabe_Rennen_UniqueVerletzungOhneVorhandenesErgebnis_WirdNichtVerschluckt()
    {
        var optionen = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var a = Katalog(new VerlierendesRennenContext(optionen, () => Task.CompletedTask), 5, ("A", 10));
        var pruefung = await ZiehenAsync(a);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            a.Dienst.SubmitExamAsync(Guid.NewGuid(), Code, Antworten(a, pruefung, 2), pruefung.SubmissionId));
    }

    [Fact]
    public async Task Doppelabgabe_Rennen_AndererDatenbankfehler_WirdNichtAlsDoppelabgabeGedeutet()
    {
        var optionen = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var a = Katalog(new VerlierendesRennenContext(optionen, () => Task.CompletedTask, sqlState: "40001"), 5, ("A", 10));
        var pruefung = await ZiehenAsync(a);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            a.Dienst.SubmitExamAsync(Guid.NewGuid(), Code, Antworten(a, pruefung, 2), pruefung.SubmissionId));
    }

    // ---- Verlauf ----

    [Fact]
    public async Task Verlauf_ZeigtNurEigeneErgebnisse_NeuesteZuerst()
    {
        var a = Katalog(3, ("A", 5));
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);

        await a.Dienst.SubmitExamAsync(ich, Code, Antworten(a, pruefung, 3));
        await a.Dienst.SubmitExamAsync(andere, Code, Antworten(a, pruefung, 0));
        await a.Dienst.SubmitExamAsync(ich, Code, Antworten(a, pruefung, 0));
        // Das erste Ergebnis liegt länger zurück.
        var aeltestes = await a.Db.QuizExamAttempts.Where(x => x.UserId == ich).OrderBy(x => x.Points).LastAsync();
        aeltestes.SubmittedAt = DateTimeOffset.UtcNow.AddDays(-2);
        a.Db.SaveChanges();

        var verlauf = (await a.Dienst.GetExamHistoryAsync(ich, Code)).Value!;

        Assert.Equal(2, verlauf.Attempts.Count);
        Assert.Equal([0, 6], verlauf.Attempts.Select(x => x.Points));   // neueste (0 Punkte) zuerst
        Assert.Equal(1, verlauf.PassedOfLastFive);
        Assert.Equal(2, verlauf.LastFiveCount);
    }

    [Fact]
    public async Task Verlauf_NurDieLetztenZehn_Kurzbilanzueber5()
    {
        var a = Katalog(3, ("A", 5));
        var nutzer = Guid.NewGuid();
        // 12 Ergebnisse, vom ältesten zum neuesten: die ältesten 7 bestanden, die neuesten 5 nicht.
        for (var i = 0; i < 12; i++)
            a.Db.QuizExamAttempts.Add(new QuizExamAttempt
            {
                UserId = nutzer, CatalogId = a.Katalog.Id, SubmittedAt = DateTimeOffset.UtcNow.AddDays(-12 + i),
                Points = i < 7 ? 6 : 0, MaxPoints = 6, Passed = i < 7, QuestionCount = 3, CorrectCount = i < 7 ? 3 : 0,
            });
        a.Db.SaveChanges();

        var verlauf = (await a.Dienst.GetExamHistoryAsync(nutzer, Code)).Value!;

        Assert.Equal(10, verlauf.Attempts.Count);
        Assert.Equal(5, verlauf.LastFiveCount);
        Assert.Equal(0, verlauf.PassedOfLastFive);
        Assert.True(verlauf.Attempts[0].SubmittedAt > verlauf.Attempts[9].SubmittedAt);
    }

    [Fact]
    public async Task Verlauf_ZaehltNurDenGefragtenKatalog()
    {
        var a = Katalog(3, ("A", 5));
        var nutzer = Guid.NewGuid();
        var anderer = new QuizCatalog { Code = "ANDERS", Name = "Anders", Publisher = "Test" };
        a.Db.QuizCatalogs.Add(anderer);
        a.Db.QuizExamAttempts.Add(new QuizExamAttempt { UserId = nutzer, CatalogId = anderer.Id, SubmittedAt = DateTimeOffset.UtcNow, MaxPoints = 6 });
        a.Db.SaveChanges();

        Assert.Empty((await a.Dienst.GetExamHistoryAsync(nutzer, Code)).Value!.Attempts);
    }

    [Fact]
    public async Task Verlauf_BlendetWeichGeloeschteErgebnisseAus()
    {
        var a = Katalog(3, ("A", 5));
        var nutzer = Guid.NewGuid();
        a.Db.QuizExamAttempts.Add(new QuizExamAttempt
        {
            UserId = nutzer, CatalogId = a.Katalog.Id, SubmittedAt = DateTimeOffset.UtcNow, MaxPoints = 6,
            DeletedAt = DateTimeOffset.UtcNow,
        });
        a.Db.SaveChanges();

        Assert.Empty((await a.Dienst.GetExamHistoryAsync(nutzer, Code)).Value!.Attempts);
    }

    [Fact]
    public async Task Verlauf_UnbekannterKatalog_IstNichtGefunden()
    {
        var a = Katalog(3, ("A", 5));

        Assert.True((await a.Dienst.GetExamHistoryAsync(Guid.NewGuid(), "GIBT-ES-NICHT")).IsNotFound);
    }

    [Fact]
    public async Task Verlauf_VertraegtUnlesbareKomplexspalte()
    {
        var a = Katalog(3, ("A", 5));
        var nutzer = Guid.NewGuid();
        a.Db.QuizExamAttempts.Add(new QuizExamAttempt
        {
            UserId = nutzer, CatalogId = a.Katalog.Id, SubmittedAt = DateTimeOffset.UtcNow, MaxPoints = 6,
            SectionResultsJson = "kaputt",
        });
        a.Db.SaveChanges();

        var eintrag = Assert.Single((await a.Dienst.GetExamHistoryAsync(nutzer, Code)).Value!.Attempts);

        Assert.Empty(eintrag.Sections);
    }

    // ---- Lernstand: "Lernstand zurücksetzen" lässt die Ergebnisse stehen ----

    [Fact]
    public async Task LernstandZuruecksetzen_LoeschtDiePruefungsergebnisNicht()
    {
        var a = Katalog(3, ("A", 5));
        var nutzer = Guid.NewGuid();
        var pruefung = await ZiehenAsync(a);
        await a.Dienst.SubmitExamAsync(nutzer, Code, Antworten(a, pruefung, 0));

        await a.Dienst.ResetAsync(nutzer, Code);

        Assert.Equal(0, (await a.Dienst.GetProgressAsync(nutzer, Code)).Value!.InMistakes);
        Assert.Single((await a.Dienst.GetExamHistoryAsync(nutzer, Code)).Value!.Attempts);
    }
}
