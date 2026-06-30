using Dogity.Application.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Tests;

public class TrainingPlanGeneratorTests
{
    private static PlanExerciseCandidate Candidate(string name, ExerciseDifficulty difficulty, bool isMandatory = true) =>
        new(Guid.NewGuid(), name, difficulty, isMandatory);

    [Fact]
    public void Generate_EveryFourthWeek_IsRestWeek()
    {
        var candidates = new[] { Candidate("Sitz", ExerciseDifficulty.Beginner) };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 26), candidates);

        Assert.True(items.Single(i => i.WeekNumber == 4).IsRestWeek);
        Assert.Null(items.Single(i => i.WeekNumber == 4).ExerciseId);
        Assert.False(items.Single(i => i.WeekNumber == 1).IsRestWeek);
    }

    [Fact]
    public void Generate_ClampsToMaxTwelveWeeks()
    {
        var candidates = new[] { Candidate("Sitz", ExerciseDifficulty.Beginner) };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2030, 1, 1), candidates);

        Assert.Equal(12, items.Select(i => i.WeekNumber).Distinct().Count());
    }

    [Fact]
    public void Generate_NoExercises_AllWeeksAreRestWeeks()
    {
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), []);

        Assert.All(items, item => Assert.True(item.IsRestWeek));
    }

    [Fact]
    public void Generate_AssignsHigherRepetitionsToBeginnerExercises()
    {
        var candidates = new[] { Candidate("Sitz", ExerciseDifficulty.Beginner) };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 8), candidates);

        Assert.Equal(3, items[0].RepetitionsTarget);
    }

    [Fact]
    public void Generate_CoversEveryMandatoryExerciseAtLeastOnceBeforeTargetDate()
    {
        var candidates = new[]
        {
            Candidate("Sitz", ExerciseDifficulty.Beginner),
            Candidate("Platz", ExerciseDifficulty.Beginner),
            Candidate("Fuß", ExerciseDifficulty.Intermediate),
        };
        // Nur eine Woche bis zum Zieldatum - trotzdem müssen alle drei
        // Pflichtübungen mindestens einmal vorkommen statt nur ein fixes Kontingent.
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 8), candidates);
        var week1ExerciseIds = items.Where(i => i.WeekNumber == 1).Select(i => i.ExerciseId).ToList();

        Assert.Equal(3, week1ExerciseIds.Distinct().Count());
        Assert.All(candidates, c => Assert.Contains(c.ExerciseId, week1ExerciseIds));
    }

    [Fact]
    public void Generate_IgnoresNonMandatoryExercisesWhenMandatoryOnesExist()
    {
        var candidates = new[]
        {
            Candidate("Sitz", ExerciseDifficulty.Beginner),
            Candidate("Kür-Trick", ExerciseDifficulty.Beginner, isMandatory: false),
        };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 8), candidates);
        var usedExerciseIds = items.Select(i => i.ExerciseId).Where(id => id != null).Distinct().ToList();

        Assert.DoesNotContain(candidates[1].ExerciseId, usedExerciseIds);
    }

    [Fact]
    public void Generate_NeverDuplicatesExerciseWithinSameWeekWhenOnlyOneAvailable()
    {
        var candidates = new[] { Candidate("Sitz", ExerciseDifficulty.Beginner) };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 8), candidates);
        var week1Items = items.Where(i => i.WeekNumber == 1).ToList();

        Assert.Single(week1Items);
    }

    [Fact]
    public void Generate_PlansAtLeastFourExercisesPerWeekWhenEnoughDistinctExercisesExist()
    {
        var candidates = Enumerable.Range(0, 8)
            .Select(i => Candidate($"Übung {i}", ExerciseDifficulty.Beginner))
            .ToArray();
        // Lange Frist (viele Wochen) - der reine "einmal abdecken"-Bedarf
        // wäre hier sehr gering, trotzdem soll der realistische
        // Mindestrhythmus von 4 Übungen/Woche gelten.
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), candidates);
        var week1Items = items.Where(i => i.WeekNumber == 1).ToList();

        Assert.Equal(4, week1Items.Count);
    }
}
