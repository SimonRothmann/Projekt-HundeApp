using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Testet das Leitner-/Spaced-Repetition-Modell des ExerciseMasteryService
/// (P2, siehe docs/SMART_TRAINING_PLAN.md): Box-Anpassung nach Trainingsausgang,
/// Fälligkeit (DueAt), Upsert je (Hund, Übung) und den einmaligen Backfill.
/// </summary>
public class ExerciseMasteryServiceTests
{
    private static readonly DateOnly Day = new(2026, 1, 1);
    private static DateTimeOffset At(DateOnly d) => new(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public void ApplyOutcome_GoodSuccess_RaisesBoxAndSetsDue()
    {
        var m = new ExerciseMastery { DogId = Guid.NewGuid(), ExerciseId = Guid.NewGuid() }; // Box 1

        ExerciseMasteryService.ApplyOutcome(m, rating: 5, success: true, date: Day);

        Assert.Equal(2, m.Box);
        Assert.Equal(5.0, m.RecentAvgRating);
        Assert.Equal(1, m.SessionCount);
        Assert.Equal(At(Day).AddDays(4), m.DueAt); // Box 2 -> 4 Tage
    }

    [Fact]
    public void ApplyOutcome_Failure_LowersBox_NeverBelowOne()
    {
        var m = new ExerciseMastery { Box = 3 };
        ExerciseMasteryService.ApplyOutcome(m, rating: 2, success: false, date: Day);
        Assert.Equal(2, m.Box);

        var atFloor = new ExerciseMastery { Box = 1 };
        ExerciseMasteryService.ApplyOutcome(atFloor, rating: 1, success: false, date: Day);
        Assert.Equal(1, atFloor.Box);
    }

    [Fact]
    public void ApplyOutcome_MidRating_KeepsBox()
    {
        var m = new ExerciseMastery { Box = 3 };
        ExerciseMasteryService.ApplyOutcome(m, rating: 3, success: true, date: Day);
        Assert.Equal(3, m.Box);
    }

    [Fact]
    public async Task ApplyLogAsync_UpsertsSingleRow_AndProgressesBox()
    {
        var db = InMemoryDbContext.Create();
        var service = new ExerciseMasteryService(db);
        var dogId = Guid.NewGuid();
        var exId = Guid.NewGuid();

        await service.ApplyLogAsync(dogId, exId, 5, true, Day);
        await db.SaveChangesAsync();
        await service.ApplyLogAsync(dogId, exId, 5, true, Day.AddDays(3));
        await db.SaveChangesAsync();

        var rows = await db.ExerciseMasteries.Where(m => m.DogId == dogId && m.ExerciseId == exId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(2, rows[0].SessionCount);
        Assert.Equal(3, rows[0].Box); // 1 -> 2 -> 3
    }

    [Fact]
    public async Task BackfillIfEmptyAsync_BuildsMasteryFromHistory_AndIsIdempotent()
    {
        var db = InMemoryDbContext.Create();
        var service = new ExerciseMasteryService(db);
        var dog = new Dog { Name = "Bello" };
        var ex = new Exercise { Name = "Fußarbeit" };
        db.Dogs.Add(dog);
        db.Exercises.Add(ex);

        var s1 = new TrainingSession { UserId = Guid.NewGuid(), DogId = dog.Id, Date = Day, DurationMinutes = 10 };
        s1.Exercises.Add(new TrainingExercise { TrainingSessionId = s1.Id, ExerciseId = ex.Id, Rating = 5, Success = true });
        var s2 = new TrainingSession { UserId = Guid.NewGuid(), DogId = dog.Id, Date = Day.AddDays(3), DurationMinutes = 10 };
        s2.Exercises.Add(new TrainingExercise { TrainingSessionId = s2.Id, ExerciseId = ex.Id, Rating = 4, Success = true });
        db.TrainingSessions.AddRange(s1, s2);
        await db.SaveChangesAsync();

        await service.BackfillIfEmptyAsync();

        var m = await db.ExerciseMasteries.SingleAsync(x => x.DogId == dog.Id && x.ExerciseId == ex.Id);
        Assert.Equal(2, m.SessionCount);
        Assert.Equal(3, m.Box); // 1 -> 2 (rating 5) -> 3 (rating 4)

        // Idempotent: erneuter Aufruf legt nichts Neues an.
        await service.BackfillIfEmptyAsync();
        Assert.Single(await db.ExerciseMasteries.Where(x => x.DogId == dog.Id).ToListAsync());
    }
}
