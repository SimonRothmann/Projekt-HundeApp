using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Testet die Plan-Ziel-Verknüpfung beim Anlegen von Tagebucheinträgen
/// (TrainingService.CreateAsync + ValidatePlanItemsAsync) - insbesondere den
/// Freitext-Fall: Ein Freitext-Plan-Ziel muss sich per Schnelleintrag
/// abschließen lassen (Bug: "Eintragen" tat bei Freitext-Übungen nichts),
/// eine Pausenwoche dagegen nie (hat wie Freitext ExerciseId null).
/// </summary>
public class TrainingServiceTests
{
    private static TrainingService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService());
    }

    private sealed record Setup(Guid UserId, Guid DogId, Guid CatalogExerciseId, Guid CatalogItemId, Guid FreeTextItemId, Guid RestWeekItemId);

    private static async Task<Setup> SetupPlanAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var userId = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId, Role = DogOwnerRole.Owner });

        var sport = new Sport { Code = "TEST", Name = "Testsport" };
        var exercise = new Exercise { SportId = sport.Id, Name = "Sitz" };
        db.Sports.Add(sport);
        db.Exercises.Add(exercise);

        var goal = new Goal { DogId = dog.Id, SportId = sport.Id, TargetDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(3)) };
        var plan = new TrainingPlan { GoalId = goal.Id };
        var catalogItem = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, ExerciseId = exercise.Id, RepetitionsTarget = 3 };
        var freeTextItem = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, FreeTextLabel = "Kopfarbeit ausprobieren", RepetitionsTarget = 2 };
        var restWeekItem = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 2, IsRestWeek = true };
        db.Goals.Add(goal);
        db.TrainingPlans.Add(plan);
        db.TrainingPlanItems.AddRange(catalogItem, freeTextItem, restWeekItem);
        await db.SaveChangesAsync();

        return new Setup(userId, dog.Id, exercise.Id, catalogItem.Id, freeTextItem.Id, restWeekItem.Id);
    }

    private static CreateTrainingSessionRequest MakeRequest(Guid dogId, CreateTrainingExerciseRequest exercise) => new(
        dogId,
        DateOnly.FromDateTime(DateTime.Today),
        10,
        null,
        [exercise]);

    [Fact]
    public async Task Create_FreeTextExercise_LinkedToFreeTextPlanItem_Succeeds()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        var result = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(null, 5, ExerciseDifficulty.Beginner, true, null, setup.FreeTextItemId, "Kopfarbeit ausprobieren")));

        Assert.True(result.Succeeded);
        var saved = Assert.Single(result.Value!.Exercises);
        Assert.Equal(setup.FreeTextItemId, saved.TrainingPlanItemId);
        Assert.Equal("Kopfarbeit ausprobieren", saved.ExerciseName);
    }

    [Fact]
    public async Task Create_FreeTextExercise_LinkedToCatalogPlanItem_Fails()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        var result = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(null, 5, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId, "Irgendwas")));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_CatalogExercise_LinkedToFreeTextPlanItem_Fails()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        var result = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 5, ExerciseDifficulty.Beginner, true, null, setup.FreeTextItemId)));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_FreeTextExercise_LinkedToRestWeek_Fails()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        var result = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(null, 5, ExerciseDifficulty.Beginner, true, null, setup.RestWeekItemId, "Trotzdem trainiert")));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_CatalogExercise_LinkedToMatchingPlanItem_Succeeds()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        var result = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));

        Assert.True(result.Succeeded);
        Assert.Equal(setup.CatalogItemId, Assert.Single(result.Value!.Exercises).TrainingPlanItemId);
    }
}
