using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Testet GoalService.RegenerateWeekAsync (P4, siehe docs/SMART_TRAINING_PLAN.md):
/// manuelle/Trainer-Items und Auto-Items mit geloggtem Fortschritt bleiben
/// erhalten, nur fortschrittslose Auto-Items werden ersetzt.
/// </summary>
public class GoalServiceRegenerateTests
{
    private sealed record Setup(Guid OwnerId, Guid GoalId, Guid ManualItemId, Guid LoggedAutoItemId, Guid PlainAutoItemId, Guid PlanId);

    private static GoalService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new GoalService(db, TimeProvider.System, new FakeNotificationService(), new ExerciseMasteryService(db));
    }

    private static async Task<Setup> SetupAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db, bool custom = false)
    {
        var ownerId = Guid.NewGuid();
        var sportId = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });

        // Vier Katalog-Übungen der Sportart (Fallback-Pool ohne Prüfungsordnung).
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
            IsCustom = custom,
            WeeklyExerciseCount = 3,
            TrainingDaysPerWeek = 1
        };
        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal };
        var manual = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, ExerciseId = exA.Id, RepetitionsTarget = 2, Source = PlanItemSource.Manual };
        var loggedAuto = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, ExerciseId = exB.Id, RepetitionsTarget = 2, Source = PlanItemSource.Auto };
        var plainAuto = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, ExerciseId = exC.Id, RepetitionsTarget = 2, Source = PlanItemSource.Auto };
        plan.Items.Add(manual);
        plan.Items.Add(loggedAuto);
        plan.Items.Add(plainAuto);
        goal.TrainingPlan = plan;
        db.Goals.Add(goal);

        // Geloggter Fortschritt auf dem einen Auto-Item.
        var session = new TrainingSession { UserId = ownerId, DogId = dog.Id, Date = DateOnly.FromDateTime(DateTime.Today), DurationMinutes = 10 };
        session.Exercises.Add(new TrainingExercise { TrainingSessionId = session.Id, ExerciseId = exB.Id, Rating = 4, Success = true, TrainingPlanItemId = loggedAuto.Id });
        db.TrainingSessions.Add(session);

        await db.SaveChangesAsync();
        return new Setup(ownerId, goal.Id, manual.Id, loggedAuto.Id, plainAuto.Id, plan.Id);
    }

    [Fact]
    public async Task RegenerateWeek_PreservesManualAndLogged_ReplacesPlainAuto()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.RegenerateWeekAsync(s.OwnerId, s.GoalId, weekNumber: 1);

        Assert.True(result.Succeeded);
        var items = await db.TrainingPlanItems
            .Where(i => i.TrainingPlanId == s.PlanId && i.WeekNumber == 1)
            .ToListAsync();

        var ids = items.Select(i => i.Id).ToHashSet();
        Assert.Contains(s.ManualItemId, ids);       // manuelles Item bleibt
        Assert.Contains(s.LoggedAutoItemId, ids);    // Auto-Item mit Fortschritt bleibt
        Assert.DoesNotContain(s.PlainAutoItemId, ids); // fortschrittsloses Auto-Item ersetzt
        Assert.Equal(3, items.Count);                // auf WeeklyExerciseCount aufgefüllt
    }

    [Fact]
    public async Task RegenerateWeek_CustomGoal_Fails()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, custom: true);

        var result = await service.RegenerateWeekAsync(s.OwnerId, s.GoalId, 1);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RegenerateWeek_NotOwner_Fails()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.RegenerateWeekAsync(Guid.NewGuid(), s.GoalId, 1);

        Assert.False(result.Succeeded);
    }

    private static async Task<Guid> SetupDueGoalAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var ownerId = Guid.NewGuid();
        var sportId = Guid.NewGuid();
        var dog = new Dog { Name = "Rex" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        var exercises = new[] { "A", "B", "C", "D" }
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
            WeeklyExerciseCount = 2,
            TrainingDaysPerWeek = 1,
            LastPlanGeneratedAt = null
        };
        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal };
        plan.Items.Add(new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, ExerciseId = exercises[0].Id, RepetitionsTarget = 2, Source = PlanItemSource.Auto });
        plan.Items.Add(new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 2, ExerciseId = exercises[1].Id, RepetitionsTarget = 2, Source = PlanItemSource.Auto });
        goal.TrainingPlan = plan;
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return goal.Id;
    }

    [Fact]
    public async Task RegenerateDuePlans_RegeneratesUpcomingWeek_AndNotifiesOwner()
    {
        var db = InMemoryDbContext.Create();
        var notifications = new FakeNotificationService();
        var service = new GoalService(db, TimeProvider.System, notifications, new ExerciseMasteryService(db));
        var goalId = await SetupDueGoalAsync(db);

        var count = await service.RegenerateDuePlansAsync();

        Assert.Equal(1, count);
        var goal = await db.Goals.FirstAsync(g => g.Id == goalId);
        Assert.NotNull(goal.LastPlanGeneratedAt);
        Assert.NotEmpty(notifications.Created); // Besitzer wurde benachrichtigt
    }

    [Fact]
    public async Task RegenerateDuePlans_SkipsRecentlyGenerated()
    {
        var service = MakeService(out var db);
        var goalId = await SetupDueGoalAsync(db);
        var goal = await db.Goals.FirstAsync(g => g.Id == goalId);
        goal.LastPlanGeneratedAt = DateTimeOffset.UtcNow; // frisch -> nicht fällig
        await db.SaveChangesAsync();

        var count = await service.RegenerateDuePlansAsync();

        Assert.Equal(0, count);
    }


    // --- Trainer:in übernimmt den Plan ------------------------------------
    // Betreuende Trainer:innen durften den Plan schon immer bearbeiten
    // (DogAccessQueries). Neu ist, dass ihre Bearbeitung den Plan aus der
    // automatischen wöchentlichen Anpassung nimmt - ein von der Trainer:in
    // aufgebauter Plan ist als Ganzes gedacht.

    private static async Task<Guid> AssignTrainerAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db, Guid goalId)
    {
        var trainerId = Guid.NewGuid();
        var dogId = await db.Goals.Where(g => g.Id == goalId).Select(g => g.DogId).SingleAsync();
        db.TrainerAssignments.Add(new Dogity.Domain.Community.TrainerAssignment
        {
            TrainerId = trainerId,
            MemberId = Guid.NewGuid(),
            DogId = dogId,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
        });
        await db.SaveChangesAsync();
        return trainerId;
    }

    [Fact]
    public async Task TrainerEditsPlan_MarksItemAsTrainerAndStopsAutoRegeneration()
    {
        var service = MakeService(out var db);
        var setup = await SetupAsync(db);
        var trainerId = await AssignTrainerAsync(db, setup.GoalId);

        var result = await service.AddPlanItemAsync(trainerId, setup.GoalId,
            new AddTrainingPlanItemRequest(2, null, "Zugarbeit an der Leine", 3, 1));

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.PlanManagedByTrainer);
        var added = await db.TrainingPlanItems.SingleAsync(i => i.FreeTextLabel == "Zugarbeit an der Leine");
        Assert.Equal(PlanItemSource.Trainer, added.Source);
        Assert.Equal(trainerId, (await db.Goals.SingleAsync(g => g.Id == setup.GoalId)).PlanManagedByTrainerId);
    }

    [Fact]
    public async Task OwnerEditsPlan_StaysManualAndKeepsAutoRegeneration()
    {
        var service = MakeService(out var db);
        var setup = await SetupAsync(db);

        var result = await service.AddPlanItemAsync(setup.OwnerId, setup.GoalId,
            new AddTrainingPlanItemRequest(2, null, "Eigene Idee", 3, 1));

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.PlanManagedByTrainer);
        var added = await db.TrainingPlanItems.SingleAsync(i => i.FreeTextLabel == "Eigene Idee");
        Assert.Equal(PlanItemSource.Manual, added.Source);
    }

    [Fact]
    public async Task RegenerateDuePlans_SkipsTrainerManagedPlans()
    {
        var service = MakeService(out var db);
        var setup = await SetupAsync(db);
        var trainerId = await AssignTrainerAsync(db, setup.GoalId);
        await service.AddPlanItemAsync(trainerId, setup.GoalId,
            new AddTrainingPlanItemRequest(2, null, "Zugarbeit", 3, 1));

        var count = await service.RegenerateDuePlansAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task OwnerCanSwitchAutoRegenerationBackOn()
    {
        var service = MakeService(out var db);
        var setup = await SetupAsync(db);
        var trainerId = await AssignTrainerAsync(db, setup.GoalId);
        await service.AddPlanItemAsync(trainerId, setup.GoalId,
            new AddTrainingPlanItemRequest(2, null, "Zugarbeit", 3, 1));

        var result = await service.SetPlanAutoRegenerationAsync(setup.OwnerId, setup.GoalId, enabled: true);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.PlanManagedByTrainer);
        // Der Eintrag der Trainer:in bleibt trotzdem stehen - er trägt die
        // Herkunft Trainer und wird vom Generator weiterhin verschont.
        Assert.Equal(PlanItemSource.Trainer,
            (await db.TrainingPlanItems.SingleAsync(i => i.FreeTextLabel == "Zugarbeit")).Source);
    }
}
