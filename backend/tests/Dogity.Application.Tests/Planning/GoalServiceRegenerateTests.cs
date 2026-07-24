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
        return new GoalService(db, TimeProvider.System);
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
}
