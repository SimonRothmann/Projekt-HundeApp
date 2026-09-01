using Dogity.Application.Learning;
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
}
