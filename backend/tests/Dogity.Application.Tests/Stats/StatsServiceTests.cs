using Dogity.Application.Stats;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Stats;

/// <summary>
/// Testet StatsService.GetDashboardAsync - insbesondere, dass die
/// Batch-Abfragen (siehe Kommentar in StatsService.cs zur N+1-Vermeidung)
/// pro Hund korrekt zugeordnete, nicht querkontaminierte Werte liefern.
/// </summary>
public class StatsServiceTests
{
    private static StatsService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new StatsService(db);
    }

    [Fact]
    public async Task GetDashboard_NoDogs_ReturnsEmptyWeeksAndEmptyPerDog()
    {
        var service = MakeService(out _);
        var userId = Guid.NewGuid();

        var result = await service.GetDashboardAsync(userId);

        Assert.True(result.Succeeded);
        Assert.Equal(12, result.Value!.WeeklyActivity.Count);
        Assert.All(result.Value.WeeklyActivity, w => Assert.Equal(0, w.Count));
        Assert.Empty(result.Value.PerDog);
    }

    [Fact]
    public async Task GetDashboard_TwoDogs_AggregatesPerDogWithoutCrossContamination()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();

        var dogA = new Dog { Name = "Bello" };
        var dogB = new Dog { Name = "Rex" };
        db.Dogs.AddRange(dogA, dogB);
        db.DogOwners.Add(new DogOwner { DogId = dogA.Id, UserId = userId });
        db.DogOwners.Add(new DogOwner { DogId = dogB.Id, UserId = userId });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Hund A: 2 Trainingseinheiten, 1 aktives Ziel
        db.TrainingSessions.Add(new TrainingSession { UserId = userId, DogId = dogA.Id, Date = today, DurationMinutes = 30 });
        db.TrainingSessions.Add(new TrainingSession { UserId = userId, DogId = dogA.Id, Date = today.AddDays(-1), DurationMinutes = 30 });
        db.Goals.Add(new Goal { DogId = dogA.Id, SportId = Guid.NewGuid(), TargetDate = today.AddYears(1), Status = GoalStatus.Active });

        // Hund B: 1 Trainingseinheit, kein aktives Ziel (sondern abgeschlossen)
        db.TrainingSessions.Add(new TrainingSession { UserId = userId, DogId = dogB.Id, Date = today, DurationMinutes = 45 });
        db.Goals.Add(new Goal { DogId = dogB.Id, SportId = Guid.NewGuid(), TargetDate = today.AddYears(1), Status = GoalStatus.Achieved });

        await db.SaveChangesAsync();

        var result = await service.GetDashboardAsync(userId);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.PerDog.Count);

        var statsA = result.Value.PerDog.Single(d => d.DogId == dogA.Id);
        var statsB = result.Value.PerDog.Single(d => d.DogId == dogB.Id);

        Assert.Equal(2, statsA.SessionCount);
        Assert.Equal(1, statsA.ActiveGoals);

        Assert.Equal(1, statsB.SessionCount);
        Assert.Equal(0, statsB.ActiveGoals);
    }

    [Fact]
    public async Task GetDashboard_OnlyOwnDogsAreIncluded()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var myDog = new Dog { Name = "Bello" };
        var foreignDog = new Dog { Name = "Fremder Hund" };
        db.Dogs.AddRange(myDog, foreignDog);
        db.DogOwners.Add(new DogOwner { DogId = myDog.Id, UserId = userId });
        db.DogOwners.Add(new DogOwner { DogId = foreignDog.Id, UserId = otherUserId });
        await db.SaveChangesAsync();

        var result = await service.GetDashboardAsync(userId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.PerDog);
        Assert.Equal(myDog.Id, result.Value.PerDog[0].DogId);
    }

    [Fact]
    public async Task GetDashboard_AvgRating_OnlyConsidersLast30Days()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId });

        var recentSession = new TrainingSession { UserId = userId, DogId = dog.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow), DurationMinutes = 30 };
        var oldSession = new TrainingSession { UserId = userId, DogId = dog.Id, Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)), DurationMinutes = 30 };
        db.TrainingSessions.AddRange(recentSession, oldSession);
        await db.SaveChangesAsync();

        db.TrainingExercises.Add(new TrainingExercise { TrainingSessionId = recentSession.Id, FreeTextLabel = "Sitz", Rating = 4, Difficulty = ExerciseDifficulty.Intermediate, Success = true });
        // Außerhalb der 30-Tage-Grenze - darf den Durchschnitt nicht beeinflussen.
        db.TrainingExercises.Add(new TrainingExercise { TrainingSessionId = oldSession.Id, FreeTextLabel = "Platz", Rating = 1, Difficulty = ExerciseDifficulty.Intermediate, Success = false });
        await db.SaveChangesAsync();

        var result = await service.GetDashboardAsync(userId);

        Assert.True(result.Succeeded);
        var stats = result.Value!.PerDog.Single();
        Assert.Equal(4, stats.AvgRating30d);
    }
}
