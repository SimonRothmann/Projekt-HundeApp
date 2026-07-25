using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Testet die Pro-Woche-Trainingstage (TrainingPlanWeekConfig) und die
/// Tag-Zuordnung (DayIndex) beim manuellen Hinzufügen/Bearbeiten von
/// Plan-Übungen (siehe docs/SMART_TRAINING_PLAN.md).
/// </summary>
public class GoalServicePlanConfigTests
{
    private sealed record Setup(Guid OwnerId, Guid GoalId, Guid PlanId, Guid ExerciseAId);

    private static GoalService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new GoalService(db, TimeProvider.System, new FakeNotificationService());
    }

    private static async Task<Setup> SetupAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db,
        int trainingDaysPerWeek = 2,
        int weeklyExerciseCount = 4)
    {
        var ownerId = Guid.NewGuid();
        var sportId = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });

        var exA = new Exercise { Name = "A", SportId = sportId, Difficulty = ExerciseDifficulty.Beginner };
        var exB = new Exercise { Name = "B", SportId = sportId, Difficulty = ExerciseDifficulty.Beginner };
        var exC = new Exercise { Name = "C", SportId = sportId, Difficulty = ExerciseDifficulty.Beginner };
        var exD = new Exercise { Name = "D", SportId = sportId, Difficulty = ExerciseDifficulty.Beginner };
        db.Exercises.AddRange(exA, exB, exC, exD);

        var goal = new Goal
        {
            DogId = dog.Id,
            SportId = sportId,
            TargetDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(2),
            Status = GoalStatus.Active,
            IsCustom = false,
            WeeklyExerciseCount = weeklyExerciseCount,
            TrainingDaysPerWeek = trainingDaysPerWeek
        };
        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal };
        goal.TrainingPlan = plan;
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return new Setup(ownerId, goal.Id, plan.Id, exA.Id);
    }

    [Fact]
    public async Task UpdateWeekConfig_SetsPerWeekDays_AndClampsItemsToLastDay()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, trainingDaysPerWeek: 3);
        // Zwei Items in Woche 1: Tag 1 und Tag 3.
        db.TrainingPlanItems.Add(new TrainingPlanItem { TrainingPlanId = s.PlanId, WeekNumber = 1, ExerciseId = s.ExerciseAId, RepetitionsTarget = 2, DayIndex = 1, Source = PlanItemSource.Manual });
        db.TrainingPlanItems.Add(new TrainingPlanItem { TrainingPlanId = s.PlanId, WeekNumber = 1, FreeTextLabel = "Extra", RepetitionsTarget = 2, DayIndex = 3, Source = PlanItemSource.Manual });
        await db.SaveChangesAsync();

        var result = await service.UpdateWeekConfigAsync(s.OwnerId, s.GoalId, weekNumber: 1, trainingDaysPerWeek: 2);

        Assert.True(result.Succeeded);
        var cfg = Assert.Single(result.Value!.WeekConfigs);
        Assert.Equal(1, cfg.WeekNumber);
        Assert.Equal(2, cfg.TrainingDaysPerWeek);

        var days = result.Value.TrainingPlan!.Items.Select(i => i.DayIndex).OrderBy(d => d).ToList();
        Assert.Equal(new[] { 1, 2 }, days); // Tag 3 -> auf letzten gültigen Tag (2) geholt
    }

    [Fact]
    public async Task UpdateWeekConfig_RejectsOutOfRange()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        Assert.False((await service.UpdateWeekConfigAsync(s.OwnerId, s.GoalId, 1, 0)).Succeeded);
        Assert.False((await service.UpdateWeekConfigAsync(s.OwnerId, s.GoalId, 1, 8)).Succeeded);
    }

    [Fact]
    public async Task AddPlanItem_ClampsDayIndexToEffectiveDays()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, trainingDaysPerWeek: 2);

        // Ohne Wochen-Override: Tag 5 -> auf Plan-Default (2) begrenzt.
        var r1 = await service.AddPlanItemAsync(s.OwnerId, s.GoalId,
            new AddTrainingPlanItemRequest(WeekNumber: 1, ExerciseId: null, FreeTextLabel: "Frei", RepetitionsTarget: 2, DayIndex: 5));
        Assert.True(r1.Succeeded);
        Assert.Equal(2, r1.Value!.TrainingPlan!.Items.Single().DayIndex);

        // Mit Wochen-Override auf 4 Tage: Tag 4 bleibt erhalten.
        await service.UpdateWeekConfigAsync(s.OwnerId, s.GoalId, 1, 4);
        var r2 = await service.AddPlanItemAsync(s.OwnerId, s.GoalId,
            new AddTrainingPlanItemRequest(WeekNumber: 1, ExerciseId: null, FreeTextLabel: "Frei2", RepetitionsTarget: 2, DayIndex: 4));
        Assert.True(r2.Succeeded);
        var newItem = r2.Value!.TrainingPlan!.Items.First(i => i.FreeTextLabel == "Frei2");
        Assert.Equal(4, newItem.DayIndex);
    }

    [Fact]
    public async Task AddPlanItem_IsMarkedManual_SurvivesRegeneration()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var add = await service.AddPlanItemAsync(s.OwnerId, s.GoalId,
            new AddTrainingPlanItemRequest(1, null, "Handgemacht", 2, 1));
        var itemId = add.Value!.TrainingPlan!.Items.Single(i => i.FreeTextLabel == "Handgemacht").Id;

        var regen = await service.RegenerateWeekAsync(s.OwnerId, s.GoalId, 1);

        Assert.True(regen.Succeeded);
        Assert.Contains(regen.Value!.TrainingPlan!.Items, i => i.Id == itemId); // manueller Eintrag bleibt
    }

    [Fact]
    public async Task UpdatePlanItem_SetsDayIndex_AndMarksManual()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, trainingDaysPerWeek: 3);
        var autoItem = new TrainingPlanItem { TrainingPlanId = s.PlanId, WeekNumber = 1, ExerciseId = s.ExerciseAId, RepetitionsTarget = 2, DayIndex = 1, Source = PlanItemSource.Auto };
        db.TrainingPlanItems.Add(autoItem);
        await db.SaveChangesAsync();

        var result = await service.UpdatePlanItemAsync(s.OwnerId, s.GoalId, autoItem.Id,
            new UpdateTrainingPlanItemRequest(WeekNumber: 1, ExerciseId: s.ExerciseAId, FreeTextLabel: null, RepetitionsTarget: 2, DayIndex: 3));

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value!.TrainingPlan!.Items.Single().DayIndex);
        var row = await db.TrainingPlanItems.AsNoTracking().SingleAsync(i => i.Id == autoItem.Id);
        Assert.Equal(PlanItemSource.Manual, row.Source);
    }

    [Fact]
    public async Task RegenerateWeek_UsesPerWeekDays()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, trainingDaysPerWeek: 2, weeklyExerciseCount: 4);
        // Ein fortschrittsloses Auto-Item, damit die Woche regeneriert wird.
        db.TrainingPlanItems.Add(new TrainingPlanItem { TrainingPlanId = s.PlanId, WeekNumber = 1, ExerciseId = s.ExerciseAId, RepetitionsTarget = 2, DayIndex = 1, Source = PlanItemSource.Auto });
        await db.SaveChangesAsync();

        // Woche 1 auf genau 1 Trainingstag setzen.
        await service.UpdateWeekConfigAsync(s.OwnerId, s.GoalId, 1, 1);

        var result = await service.RegenerateWeekAsync(s.OwnerId, s.GoalId, 1);

        Assert.True(result.Succeeded);
        var week1 = result.Value!.TrainingPlan!.Items.Where(i => i.WeekNumber == 1 && !i.IsRestWeek).ToList();
        Assert.NotEmpty(week1);
        Assert.All(week1, i => Assert.Equal(1, i.DayIndex)); // alles auf Tag 1
    }
}
