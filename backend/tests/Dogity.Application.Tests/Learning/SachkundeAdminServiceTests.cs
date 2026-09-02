using Dogity.Application.Learning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Learning;

namespace Dogity.Application.Tests.Learning;

/// <summary>
/// Testet die Verwaltung der Fragenkataloge: was gespeichert werden darf, was
/// abgelehnt wird, und dass eine Handkorrektur den Seeder überlebt.
/// </summary>
public class SachkundeAdminServiceTests
{
    private static (SachkundeAdminService Dienst, QuizQuestion Frage, List<QuizOption> Zeilen) MitAuswahlfrage(
        string text = "Welche Aussage ist richtig?")
    {
        var db = InMemoryDbContext.Create();
        var katalog = new QuizCatalog { Code = "TEST", Name = "Test", Publisher = "Test" };
        var frage = new QuizQuestion
        {
            CatalogId = katalog.Id, Catalog = katalog, Number = "A1", Section = "A",
            Text = text, Kind = QuizQuestionKind.SingleChoice,
        };
        var zeilen = new List<QuizOption>
        {
            new() { Question = frage, Kind = QuizOptionKind.Answer, Text = "richtig", IsCorrect = true, SortOrder = 1 },
            new() { Question = frage, Kind = QuizOptionKind.Answer, Text = "falsch", IsCorrect = false, SortOrder = 2 },
        };
        foreach (var z in zeilen) frage.Options.Add(z);

        db.QuizCatalogs.Add(katalog);
        db.QuizQuestions.Add(frage);
        db.QuizOptions.AddRange(zeilen);
        db.SaveChanges();

        return (new SachkundeAdminService(db, TimeProvider.System), frage, zeilen);
    }

    private static UpdateQuizQuestionRequest Fassung(
        string text, List<QuizOption> zeilen, Func<QuizOption, bool>? richtig = null) =>
        new(text, null, zeilen.Select(z => new UpdateQuizOptionRequest(
            z.Id, z.Kind.ToString(), z.Text, richtig?.Invoke(z) ?? z.IsCorrect, z.MatchKey, z.ImageName)).ToList());

    [Fact]
    public async Task Speichern_MarkiertDieFrageAlsVonHandBearbeitet()
    {
        var (dienst, frage, zeilen) = MitAuswahlfrage();

        var ergebnis = await dienst.UpdateQuestionAsync(
            Guid.NewGuid(), frage.Id, Fassung("Korrigierte Fragestellung?", zeilen));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal("Korrigierte Fragestellung?", ergebnis.Value!.Text);
        // Ohne diese Marke wäre die Korrektur beim nächsten Start der App weg -
        // der Seeder schreibt sonst wieder die Katalogfassung.
        Assert.NotNull(ergebnis.Value.EditedAt);
    }

    [Fact]
    public async Task Speichern_OhneRichtigeAntwort_WirdAbgelehnt()
    {
        var (dienst, frage, zeilen) = MitAuswahlfrage();

        var ergebnis = await dienst.UpdateQuestionAsync(
            Guid.NewGuid(), frage.Id, Fassung("Frage?", zeilen, _ => false));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Speichern_MitZweiRichtigenBeiEinfachauswahl_WirdAbgelehnt()
    {
        var (dienst, frage, zeilen) = MitAuswahlfrage();

        var ergebnis = await dienst.UpdateQuestionAsync(
            Guid.NewGuid(), frage.Id, Fassung("Frage?", zeilen, _ => true));

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Speichern_MitLeererFragestellung_WirdAbgelehnt()
    {
        var (dienst, frage, zeilen) = MitAuswahlfrage();

        var ergebnis = await dienst.UpdateQuestionAsync(Guid.NewGuid(), frage.Id, Fassung("   ", zeilen));

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Speichern_MitEinerEinzigenAntwort_WirdAbgelehnt()
    {
        var (dienst, frage, zeilen) = MitAuswahlfrage();

        var ergebnis = await dienst.UpdateQuestionAsync(
            Guid.NewGuid(), frage.Id, Fassung("Frage?", zeilen.Take(1).ToList()));

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Zuruecknehmen_LoeschtNurDieMarke()
    {
        var (dienst, frage, zeilen) = MitAuswahlfrage();
        await dienst.UpdateQuestionAsync(Guid.NewGuid(), frage.Id, Fassung("Neu?", zeilen));

        var ergebnis = await dienst.RevertQuestionAsync(frage.Id);

        Assert.True(ergebnis.Succeeded);
        // Der Text bleibt zunächst stehen; die Katalogfassung holt erst der
        // Seeder beim nächsten Start zurück.
        Assert.Equal("Neu?", frage.Text);
        Assert.Null(frage.EditedAt);
    }

    [Fact]
    public async Task Zuruecknehmen_OhneVorherigeBearbeitung_WirdAbgelehnt()
    {
        var (dienst, frage, _) = MitAuswahlfrage();

        var ergebnis = await dienst.RevertQuestionAsync(frage.Id);

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Auffaelligkeiten_MeldenEchteFundeUndSchweigenBeiAufzaehlungen()
    {
        var (mitFehler, frageMitFehler, _) = MitAuswahlfrage("Was ist das , wirklich?");
        var treffer = await mitFehler.GetQuestionsAsync(null, null, null, false, false);
        Assert.Contains("Leerzeichen vor Satzzeichen",
            treffer.Value!.Single(q => q.Id == frageMitFehler.Id).Flags);

        // "a) ... b) ..." ist eine Aufzählung, keine unpaarige Klammer - das
        // hatte die Musterlösung zu D5 dauerhaft falsch markiert.
        var (sauber, frageSauber, _) = MitAuswahlfrage("a) eins b) zwei c) drei?");
        var ohne = await sauber.GetQuestionsAsync(null, null, null, false, false);
        Assert.DoesNotContain("unpaarige Klammer",
            ohne.Value!.Single(q => q.Id == frageSauber.Id).Flags);
    }

    [Fact]
    public async Task Suche_FindetAuchUeberDieAntworttexte()
    {
        var (dienst, frage, _) = MitAuswahlfrage();

        var treffer = await dienst.GetQuestionsAsync(null, null, "RICHTIG", false, false);

        Assert.Single(treffer.Value!);
        Assert.Equal(frage.Id, treffer.Value![0].Id);
    }
}
