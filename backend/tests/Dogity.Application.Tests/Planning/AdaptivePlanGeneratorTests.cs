using Dogity.Application.Planning;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Testet die reine adaptive Wochenauswahl (P3, siehe
/// docs/SMART_TRAINING_PLAN.md): Schwachstellen häufiger, gemeisterte Übungen
/// tauchen nach Fälligkeit wieder auf, neue werden leicht-zuerst eingeführt,
/// Verteilung auf Trainingstage mit Schwierigkeits-Ordnung, manueller Boost.
/// </summary>
public class AdaptivePlanGeneratorTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);

    private static AdaptiveCandidate C(string name, ExerciseDifficulty diff, int sessions = 0, double avg = 0, DateOnly? due = null, int manual = 0)
        => new(Guid.NewGuid(), name, diff, sessions, avg, due, manual);

    private static AdaptivePlanConfig Config(int weekly, int days = 2) => new(weekly, days);

    [Fact]
    public void WeakExercise_IsSelected_WithWeaknessReasonAndMoreReps()
    {
        var weak = C("Fußarbeit", ExerciseDifficulty.Beginner, sessions: 3, avg: 1.5, due: Today.AddDays(-1));

        var items = AdaptivePlanGenerator.GenerateWeek(Today, weekNumber: 1, [weak], Config(4));

        var item = Assert.Single(items);
        Assert.Equal(weak.ExerciseId, item.ExerciseId);
        Assert.Equal(PlanItemReason.Weakness, item.Reason);
        Assert.Equal(PlanItemSource.Auto, item.Source);
        Assert.Equal(4, item.RepetitionsTarget); // Beginner-Basis 3 + 1 für Schwachstelle
    }

    [Fact]
    public void MasteredButOverdue_Resurfaces_AsRepetition()
    {
        var mastered = C("Sitz", ExerciseDifficulty.Beginner, sessions: 6, avg: 5.0, due: Today.AddDays(-3));

        var items = AdaptivePlanGenerator.GenerateWeek(Today, 1, [mastered], Config(4));

        var item = Assert.Single(items);
        Assert.Equal(PlanItemReason.Repetition, item.Reason);
    }

    [Fact]
    public void NewExercises_IntroducedEasyFirst()
    {
        // Name absichtlich gegenläufig zur Schwierigkeit: entscheidet die
        // Schwierigkeit (leicht zuerst), NICHT der Name.
        var easy = C("z-leicht", ExerciseDifficulty.Beginner);
        var hard = C("a-schwer", ExerciseDifficulty.Advanced);

        var items = AdaptivePlanGenerator.GenerateWeek(Today, 1, [hard, easy], Config(weekly: 1));

        var item = Assert.Single(items);
        Assert.Equal(easy.ExerciseId, item.ExerciseId);
        Assert.Equal(PlanItemReason.Introduction, item.Reason);
    }

    [Fact]
    public void ManualPriority_BoostsSelection()
    {
        // Zwei gleichwertige (gemeisterte, nicht fällige) Übungen; nur der Boost
        // unterscheidet sie. Budget 1 erzwingt die Auswahl.
        var boosted = C("A", ExerciseDifficulty.Beginner, sessions: 4, avg: 4.0, due: Today.AddDays(5), manual: 2);
        var plain = C("B", ExerciseDifficulty.Beginner, sessions: 4, avg: 4.0, due: Today.AddDays(5), manual: 0);

        var items = AdaptivePlanGenerator.GenerateWeek(Today, 1, [plain, boosted], Config(weekly: 1));

        var item = Assert.Single(items);
        Assert.Equal(boosted.ExerciseId, item.ExerciseId);
    }

    [Fact]
    public void DistributesAcrossDays_EachDayAscendingDifficulty_RespectsWeeklyCount()
    {
        var a = C("a", ExerciseDifficulty.Beginner);
        var b = C("b", ExerciseDifficulty.Beginner);
        var c = C("c", ExerciseDifficulty.Intermediate);
        var d = C("d", ExerciseDifficulty.Advanced);

        var items = AdaptivePlanGenerator.GenerateWeek(Today, 1, [a, b, c, d], Config(weekly: 4, days: 2));

        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.InRange(i.DayIndex, 1, 2));

        // Innerhalb jedes Trainingstags: Schwierigkeit nicht absteigend.
        foreach (var day in items.GroupBy(i => i.DayIndex))
        {
            var diffs = day.Select(i => (int)i.Difficulty!.Value).ToList();
            var sorted = diffs.OrderBy(x => x).ToList();
            Assert.Equal(sorted, diffs);
        }
    }

    [Fact]
    public void WeeklyCount_ClampedToAvailableCandidates()
    {
        var only = C("Einzige", ExerciseDifficulty.Beginner);

        var items = AdaptivePlanGenerator.GenerateWeek(Today, 1, [only], Config(weekly: 5));

        Assert.Single(items); // nur 1 Kandidat -> nie doppelt in derselben Woche
    }
}
