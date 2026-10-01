using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Pausenwochen im Trainingsplan: Eine Pausenwoche besteht aus einem
/// Platzhalter ohne Übung (Zielwert 0). Neben echten Übungen derselben Woche
/// erschien er als namenlose Zeile "0/0x erledigt" und zählte in "0/6" mit.
/// Außerdem: Übungen, die inzwischen weich aus dem Katalog gelöscht sind,
/// behalten im Plan ihren Namen.
/// </summary>
public class GoalServicePauseWeekTests
{
    private sealed record Setup(Guid OwnerId, Guid DogId, Guid GoalId, Guid PlanId, Exercise[] Exercises);

    private static GoalService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new GoalService(db, TimeProvider.System, new FakeNotificationService(), new ExerciseMasteryService(db));
    }

    private static async Task<Setup> SetupAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db,
        Action<TrainingPlan, Exercise[]> fillPlan)
    {
        var ownerId = Guid.NewGuid();
        var sportId = Guid.NewGuid();
        var dog = new Dog { Name = "Rocky" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });

        var exercises = new[] { "A", "B", "C", "D", "E" }
            .Select(n => new Exercise { Name = n, SportId = sportId, Difficulty = ExerciseDifficulty.Beginner })
            .ToArray();
        db.Exercises.AddRange(exercises);

        // CreatedAt = jetzt -> aktuelle Woche 1, kommende Woche 2.
        var goal = new Goal
        {
            DogId = dog.Id,
            SportId = sportId,
            TargetDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(2),
            Status = GoalStatus.Active,
            IsCustom = false,
            WeeklyExerciseCount = 3,
            TrainingDaysPerWeek = 1,
        };
        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal };
        fillPlan(plan, exercises);
        goal.TrainingPlan = plan;
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return new Setup(ownerId, dog.Id, goal.Id, plan.Id, exercises);
    }

    private static TrainingPlanItem Item(TrainingPlan plan, int week, Exercise exercise) => new()
    {
        TrainingPlanId = plan.Id,
        WeekNumber = week,
        ExerciseId = exercise.Id,
        RepetitionsTarget = 2,
        Source = PlanItemSource.Auto,
    };

    private static TrainingPlanItem RestPlaceholder(TrainingPlan plan, int week) => new()
    {
        TrainingPlanId = plan.Id,
        WeekNumber = week,
        IsRestWeek = true,
    };

    [Fact]
    public async Task RegenerateDuePlans_LeavesRestWeekAlone()
    {
        var service = MakeService(out var db);
        var setup = await SetupAsync(db, (plan, ex) =>
        {
            plan.Items.Add(Item(plan, 1, ex[0]));
            plan.Items.Add(RestPlaceholder(plan, 2));
        });

        var count = await service.RegenerateDuePlansAsync();

        // Die kommende Woche 2 ist eine Pausenwoche: nichts wird erzeugt.
        Assert.Equal(0, count);
        var week2 = await db.TrainingPlanItems.Where(i => i.TrainingPlanId == setup.PlanId && i.WeekNumber == 2).ToListAsync();
        var placeholder = Assert.Single(week2);
        Assert.True(placeholder.IsRestWeek);
    }

    [Fact]
    public async Task RegenerateWeek_OnRestWeek_ReplacesThePlaceholder()
    {
        var service = MakeService(out var db);
        var setup = await SetupAsync(db, (plan, ex) =>
        {
            plan.Items.Add(Item(plan, 1, ex[0]));
            plan.Items.Add(RestPlaceholder(plan, 2));
        });

        var result = await service.RegenerateWeekAsync(setup.OwnerId, setup.GoalId, weekNumber: 2);

        Assert.True(result.Succeeded);
        var week2 = result.Value!.TrainingPlan!.Items.Where(i => i.WeekNumber == 2).ToList();
        Assert.NotEmpty(week2);
        Assert.DoesNotContain(week2, i => i.IsRestWeek);
        // Der Platzhalter ist weich gelöscht, nicht nur ausgeblendet.
        var stored = await db.TrainingPlanItems.IgnoreQueryFilters()
            .Where(i => i.TrainingPlanId == setup.PlanId && i.WeekNumber == 2 && i.IsRestWeek)
            .ToListAsync();
        Assert.All(stored, i => Assert.NotNull(i.DeletedAt));
    }

    [Fact]
    public async Task GetByDog_HidesRestPlaceholderNextToRealExercises()
    {
        var service = MakeService(out var db);
        // Altbestand: Platzhalter UND echte Übungen in derselben Woche.
        var setup = await SetupAsync(db, (plan, ex) =>
        {
            plan.Items.Add(RestPlaceholder(plan, 12));
            plan.Items.Add(Item(plan, 12, ex[0]));
            plan.Items.Add(Item(plan, 12, ex[1]));
            plan.Items.Add(RestPlaceholder(plan, 4));
        });

        var result = await service.GetByDogAsync(setup.OwnerId, setup.DogId);

        var items = result.Value!.Single().TrainingPlan!.Items;
        var week12 = items.Where(i => i.WeekNumber == 12).ToList();
        Assert.Equal(2, week12.Count);
        Assert.DoesNotContain(week12, i => i.IsRestWeek);
        // Eine reine Pausenwoche behält ihren Platzhalter.
        Assert.Single(items, i => i.WeekNumber == 4 && i.IsRestWeek);
    }

    [Fact]
    public async Task GetByDog_KeepsNameOfExerciseDeletedFromCatalog()
    {
        var service = MakeService(out var db);
        var setup = await SetupAsync(db, (plan, ex) =>
        {
            plan.Items.Add(Item(plan, 1, ex[0]));
            plan.Items.Add(Item(plan, 1, ex[1]));
        });
        // Übung "B" fliegt später aus dem Katalog; der Filter blendet sie auch
        // über die Navigation des Plan-Eintrags aus.
        var deleted = await db.Exercises.SingleAsync(e => e.Id == setup.Exercises[1].Id);
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await service.GetByDogAsync(setup.OwnerId, setup.DogId);

        var names = result.Value!.Single().TrainingPlan!.Items.Select(i => i.ExerciseName).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "A", "B" }, names);
    }
}
