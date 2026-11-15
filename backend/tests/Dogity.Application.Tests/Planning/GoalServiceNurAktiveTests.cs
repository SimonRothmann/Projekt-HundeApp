using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;

namespace Dogity.Application.Tests.Planning;

/// <summary>Die Startseite lädt mit nurAktive nur aktive Ziele aus der Datenbank.</summary>
public class GoalServiceNurAktiveTests
{
    [Fact]
    public async Task GetByDog_NurAktive_LiefertKeinErreichtesUndKeinAbgebrochenesZiel()
    {
        var db = InMemoryDbContext.Create();
        var service = new GoalService(db, TimeProvider.System, new FakeNotificationService(), new ExerciseMasteryService(db));
        var userId = Guid.NewGuid();
        var hund = new Dog { Name = "Rex" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = userId, Role = DogOwnerRole.Owner });
        var ziele = new Dictionary<GoalStatus, Goal>();
        foreach (var status in Enum.GetValues<GoalStatus>())
        {
            var ziel = new Goal { DogId = hund.Id, SportId = Guid.NewGuid(), TargetDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(2), Status = status };
            ziele[status] = ziel;
            db.Goals.Add(ziel);
        }
        await db.SaveChangesAsync();

        var alle = await service.GetByDogAsync(userId, hund.Id);
        var aktive = await service.GetByDogAsync(userId, hund.Id, nurAktive: true);

        Assert.Equal(3, alle.Value!.Count);
        Assert.Equal(ziele[GoalStatus.Active].Id, Assert.Single(aktive.Value!).Id);
    }
}
