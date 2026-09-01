using Dogity.Application.Learning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Learning;

namespace Dogity.Application.Tests.Learning;

/// <summary>
/// Testet die Leitner-Mechanik des Fragentrainers.
///
/// Der Unterschied zum <c>ExerciseMasteryService</c> ist hier der Punkt: dort
/// sinkt die Box bei einem schwachen Training um EINE Stufe, hier fällt sie bei
/// einer falschen Antwort auf ANFANG zurück. Eine Übung, die heute schlechter
/// lief, ist nicht verlernt - eine falsch beantwortete Frage war schlicht nicht
/// gewusst.
/// </summary>
public class SachkundeServiceTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 9, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Richtig_HebtFachUndSetztWiedervorlage()
    {
        var m = new QuizMastery(); // Fach 1

        SachkundeService.ApplyOutcome(m, correct: true, Jetzt);

        Assert.Equal(2, m.Box);
        Assert.Equal(1, m.CorrectCount);
        Assert.True(m.LastWasCorrect);
        Assert.Equal(Jetzt.AddDays(2), m.DueAt); // Fach 2 -> 2 Tage
        Assert.Equal(Jetzt, m.LastAnsweredAt);
    }

    [Fact]
    public void Falsch_SetztAufAnfangZurueck_NichtNurEineStufe()
    {
        var m = new QuizMastery { Box = 4, CorrectCount = 3 };

        SachkundeService.ApplyOutcome(m, correct: false, Jetzt);

        Assert.Equal(1, m.Box);
        Assert.Equal(1, m.WrongCount);
        Assert.Equal(3, m.CorrectCount); // richtige Antworten bleiben gezählt
        Assert.False(m.LastWasCorrect);
    }

    [Fact]
    public void Falsch_IstSofortWiederFaellig()
    {
        var m = new QuizMastery { Box = 3 };

        SachkundeService.ApplyOutcome(m, correct: false, Jetzt);

        // Genau das trägt das "kommt immer wieder": die Frage ist ab sofort
        // fällig und taucht in der nächsten Runde erneut auf.
        Assert.Equal(Jetzt, m.DueAt);
        Assert.False(m.DueAt > Jetzt);
    }

    [Fact]
    public void Fach_SteigtHoechstensBisFuenf()
    {
        var m = new QuizMastery { Box = 5 };

        SachkundeService.ApplyOutcome(m, correct: true, Jetzt);

        Assert.Equal(5, m.Box);
        Assert.Equal(Jetzt.AddDays(21), m.DueAt);
    }

    [Fact]
    public void Wiedervorlage_WirdMitJedemFachLaenger()
    {
        var m = new QuizMastery();
        var abstaende = new List<double>();

        for (var i = 0; i < 5; i++)
        {
            SachkundeService.ApplyOutcome(m, correct: true, Jetzt);
            abstaende.Add((m.DueAt!.Value - Jetzt).TotalDays);
        }

        Assert.Equal([2, 4, 9, 21, 21], abstaende);
    }

    [Fact]
    public void EineFalscheAntwortLoeschtDenAufbau()
    {
        var m = new QuizMastery();
        for (var i = 0; i < 4; i++)
            SachkundeService.ApplyOutcome(m, correct: true, Jetzt);
        Assert.Equal(5, m.Box);

        SachkundeService.ApplyOutcome(m, correct: false, Jetzt);

        Assert.Equal(1, m.Box);
        Assert.Equal(4, m.CorrectCount);
        Assert.Equal(1, m.WrongCount);
    }

    // ---- Zuordnungsaufgaben ----
    //
    // Diese Fragen waren zunächst als Karte zum Selbsteinschätzen gebaut. Die
    // Fragestellung lautet aber "Ordnen Sie den aufgelisteten Rassen die
    // Merkmale zu" - und aufgelistet war nichts, die Aufgabe ließ sich gar
    // nicht versuchen. Jetzt ordnet man wirklich zu, und der Server prüft.

    private static (SachkundeService Service, QuizQuestion Frage, List<QuizOption> Begriffe) MitZuordnung()
    {
        var db = InMemoryDbContext.Create();
        var katalog = new QuizCatalog { Code = "TEST", Name = "Test", Publisher = "Test" };
        var frage = new QuizQuestion
        {
            CatalogId = katalog.Id,
            Catalog = katalog,
            Number = "A18",
            Section = "A",
            Text = "Ordnen Sie zu:",
            Kind = QuizQuestionKind.Assignment,
        };

        var begriffe = new List<QuizOption>
        {
            new() { Question = frage, Kind = QuizOptionKind.Term, Text = "Boxer", MatchKey = "E", SortOrder = 101 },
            new() { Question = frage, Kind = QuizOptionKind.Term, Text = "Basset", MatchKey = "C", SortOrder = 102 },
            new() { Question = frage, Kind = QuizOptionKind.Term, Text = "Pudel", MatchKey = "D", SortOrder = 103 },
        };
        foreach (var b in begriffe) frage.Options.Add(b);

        db.QuizCatalogs.Add(katalog);
        db.QuizQuestions.Add(frage);
        db.QuizOptions.AddRange(begriffe);
        db.SaveChanges();

        return (new SachkundeService(db, TimeProvider.System), frage, begriffe);
    }

    [Fact]
    public async Task Zuordnung_AlleRichtig_IstRichtig()
    {
        var (service, frage, begriffe) = MitZuordnung();
        var belegung = begriffe.ToDictionary(b => b.Id, b => b.MatchKey!);

        var ergebnis = await service.SubmitAnswerAsync(Guid.NewGuid(), frage.Id, null, null, belegung);

        Assert.True(ergebnis.Succeeded);
        Assert.True(ergebnis.Value!.Correct);
        Assert.All(ergebnis.Value.TermResults.Values, Assert.True);
        Assert.Equal(2, ergebnis.Value.Box);
    }

    [Fact]
    public async Task Zuordnung_EinBegriffFalsch_IstGanzFalsch()
    {
        var (service, frage, begriffe) = MitZuordnung();
        var belegung = begriffe.ToDictionary(b => b.Id, b => b.MatchKey!);
        belegung[begriffe[0].Id] = "A"; // Boxer falsch

        var ergebnis = await service.SubmitAnswerAsync(Guid.NewGuid(), frage.Id, null, null, belegung);

        // Eine Zuordnung ist ganz richtig oder gar nicht - "zwei von drei" gibt
        // es beim Zuordnen nicht.
        Assert.False(ergebnis.Value!.Correct);
        Assert.False(ergebnis.Value.TermResults[begriffe[0].Id]);
        Assert.True(ergebnis.Value.TermResults[begriffe[1].Id]);
        Assert.Equal(1, ergebnis.Value.Box);
    }

    [Fact]
    public async Task Zuordnung_GrossKleinschreibungEgal()
    {
        var (service, frage, begriffe) = MitZuordnung();
        var belegung = begriffe.ToDictionary(b => b.Id, b => b.MatchKey!.ToLowerInvariant());

        var ergebnis = await service.SubmitAnswerAsync(Guid.NewGuid(), frage.Id, null, null, belegung);

        Assert.True(ergebnis.Value!.Correct);
    }

    [Fact]
    public async Task Zuordnung_Unvollstaendig_WirdAbgewiesen()
    {
        var (service, frage, begriffe) = MitZuordnung();
        var belegung = new Dictionary<Guid, string> { [begriffe[0].Id] = "E" };

        var ergebnis = await service.SubmitAnswerAsync(Guid.NewGuid(), frage.Id, null, null, belegung);

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Zuordnung_FremderBegriff_WirdAbgewiesen()
    {
        var (service, frage, begriffe) = MitZuordnung();
        var belegung = begriffe.ToDictionary(b => b.Id, b => b.MatchKey!);
        belegung[Guid.NewGuid()] = "A";

        var ergebnis = await service.SubmitAnswerAsync(Guid.NewGuid(), frage.Id, null, null, belegung);

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Zuordnung_SelbsteinschaetzungReichtNichtMehr()
    {
        var (service, frage, _) = MitZuordnung();

        var ergebnis = await service.SubmitAnswerAsync(Guid.NewGuid(), frage.Id, null, selfAssessedCorrect: true, null);

        Assert.False(ergebnis.Succeeded);
    }
}
