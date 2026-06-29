using Dogity.Application.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Tests;

public class TrainingPlanGeneratorTests
{
    private static Exercise MakeExercise(string name, ExerciseDifficulty difficulty) =>
        new() { Name = name, Difficulty = difficulty, SportId = Guid.NewGuid() };

    [Fact]
    public void Generate_EveryFourthWeek_IsRestWeek()
    {
        var exercises = new[] { MakeExercise("Sitz", ExerciseDifficulty.Beginner) };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 26), exercises);

        Assert.True(items[3].IsRestWeek);
        Assert.Null(items[3].ExerciseId);
        Assert.False(items[0].IsRestWeek);
    }

    [Fact]
    public void Generate_ClampsToMaxTwelveWeeks()
    {
        var exercises = new[] { MakeExercise("Sitz", ExerciseDifficulty.Beginner) };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2030, 1, 1), exercises);

        Assert.Equal(12, items.Count);
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
        var exercises = new[] { MakeExercise("Sitz", ExerciseDifficulty.Beginner) };
        var items = TrainingPlanGenerator.Generate(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 8), exercises);

        Assert.Equal(3, items[0].RepetitionsTarget);
    }
}
