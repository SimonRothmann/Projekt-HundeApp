using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
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
    public async Task GetByDog_WithDateRange_ReturnsOnlySessionsInRange()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var daysAgo in new[] { 0, 10, 40 })
        {
            db.TrainingSessions.Add(new Dogity.Domain.Training.TrainingSession
            {
                UserId = setup.UserId,
                DogId = setup.DogId,
                Date = today.AddDays(-daysAgo),
                DurationMinutes = 30,
            });
        }
        await db.SaveChangesAsync();

        var all = await service.GetByDogAsync(setup.UserId, setup.DogId);
        var lastMonth = await service.GetByDogAsync(setup.UserId, setup.DogId, from: today.AddDays(-30), to: today);

        Assert.Equal(3, all.Value!.Count);
        Assert.Equal(2, lastMonth.Value!.Count);
        Assert.All(lastMonth.Value, s => Assert.True(s.Date >= today.AddDays(-30)));
    }

    [Fact]
    public async Task GetByDog_SetsHasGpsTrackOnlyForSessionsWithTrack()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var withTrack = new Dogity.Domain.Training.TrainingSession { UserId = setup.UserId, DogId = setup.DogId, Date = today, DurationMinutes = 30 };
        var withoutTrack = new Dogity.Domain.Training.TrainingSession { UserId = setup.UserId, DogId = setup.DogId, Date = today.AddDays(-1), DurationMinutes = 30 };
        db.TrainingSessions.AddRange(withTrack, withoutTrack);
        db.GpsTracks.Add(new Dogity.Domain.Tracking.GpsTrack { TrainingSessionId = withTrack.Id });
        await db.SaveChangesAsync();

        var result = await service.GetByDogAsync(setup.UserId, setup.DogId);

        Assert.True(result.Value!.Single(s => s.Id == withTrack.Id).HasGpsTrack);
        Assert.False(result.Value!.Single(s => s.Id == withoutTrack.Id).HasGpsTrack);
    }

    [Fact]
    public async Task Create_SameDayWithoutClientId_MergesIntoExistingSession()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        var first = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));
        var second = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(null, 5, ExerciseDifficulty.Beginner, true, null, setup.FreeTextItemId, "Kopfarbeit ausprobieren")));

        // Zweiter Eintrag am selben Tag hängt sich an die bestehende Einheit
        // an (Tagebuch: EIN Feld pro Trainingstag), statt eine neue anzulegen.
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(2, second.Value!.Exercises.Count);
        Assert.Equal(20, second.Value!.DurationMinutes); // 10 + 10 summiert

        var all = await service.GetByDogAsync(setup.UserId, setup.DogId);
        Assert.Single(all.Value!);
    }

    [Fact]
    public async Task Create_SameDayWithClientId_KeepsSeparateSession()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));
        // FahrteRecorder-Pfad: client-generierte Id (Offline-Idempotenz, der
        // GPS-Track referenziert genau diese Id) - darf NICHT gemergt werden.
        var withId = new CreateTrainingSessionRequest(
            setup.DogId, DateOnly.FromDateTime(DateTime.Today), 5, "Fährtenaufnahme", [], Id: Guid.NewGuid());
        var second = await service.CreateAsync(setup.UserId, withId);

        Assert.True(second.Succeeded);
        var all = await service.GetByDogAsync(setup.UserId, setup.DogId);
        Assert.Equal(2, all.Value!.Count);
    }

    [Fact]
    public async Task Create_SameDay_MergesNotes()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);

        await service.CreateAsync(setup.UserId, new CreateTrainingSessionRequest(
            setup.DogId, DateOnly.FromDateTime(DateTime.Today), 10, "Morgens gut drauf",
            [new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)]));
        var second = await service.CreateAsync(setup.UserId, new CreateTrainingSessionRequest(
            setup.DogId, DateOnly.FromDateTime(DateTime.Today), 10, "Abends müde",
            [new CreateTrainingExerciseRequest(null, 3, ExerciseDifficulty.Beginner, false, null, null, "Spaziergang")]));

        Assert.Equal("Morgens gut drauf\nAbends müde", second.Value!.Notes);
    }

    [Fact]
    public async Task UpdateSessionNotes_ByOwner_ChangesDayComment()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var created = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));

        var result = await service.UpdateSessionNotesAsync(setup.UserId, created.Value!.Id, "  Guter Tag  ");

        Assert.True(result.Succeeded);
        var reloaded = await service.GetByIdAsync(setup.UserId, created.Value!.Id);
        Assert.Equal("Guter Tag", reloaded.Value!.Notes);
    }

    [Fact]
    public async Task UpdateSessionNotes_ByOtherUser_Fails()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var created = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));

        var result = await service.UpdateSessionNotesAsync(Guid.NewGuid(), created.Value!.Id, "fremd");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateExerciseNotes_ByOwner_ChangesNote()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var created = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, "alte Notiz", setup.CatalogItemId)));
        var exerciseId = Assert.Single(created.Value!.Exercises).Id;

        var result = await service.UpdateExerciseNotesAsync(setup.UserId, exerciseId, "  neue Notiz  ");

        Assert.True(result.Succeeded);
        var reloaded = await service.GetByIdAsync(setup.UserId, created.Value!.Id);
        Assert.Equal("neue Notiz", Assert.Single(reloaded.Value!.Exercises).Notes);
    }

    [Fact]
    public async Task UpdateExerciseNotes_EmptyString_ClearsNote()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var created = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, "eine Notiz", setup.CatalogItemId)));
        var exerciseId = Assert.Single(created.Value!.Exercises).Id;

        await service.UpdateExerciseNotesAsync(setup.UserId, exerciseId, "   ");

        var reloaded = await service.GetByIdAsync(setup.UserId, created.Value!.Id);
        Assert.Null(Assert.Single(reloaded.Value!.Exercises).Notes);
    }

    [Fact]
    public async Task UpdateExerciseNotes_ByOtherUser_Fails()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var created = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 4, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));
        var exerciseId = Assert.Single(created.Value!.Exercises).Id;

        var result = await service.UpdateExerciseNotesAsync(Guid.NewGuid(), exerciseId, "fremd");

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

    // Legt eine Übung an und weist dem Hund einen Trainer zu; gibt Trainer-Id,
    // Session-Id und Übungs-Id für die Trainer-Bewertungstests zurück.
    private static async Task<(Guid TrainerId, Guid SessionId, Guid ExerciseId)> SetupTrainerAndExerciseAsync(
        TrainingService service, Dogity.Infrastructure.Persistence.ApplicationDbContext db, Setup setup)
    {
        var created = await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 3, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));
        var trainerId = Guid.NewGuid();
        db.TrainerAssignments.Add(new TrainerAssignment
        {
            DogId = setup.DogId,
            TrainerId = trainerId,
            MemberId = setup.UserId,
            StartDate = DateOnly.FromDateTime(DateTime.Today)
        });
        await db.SaveChangesAsync();
        return (trainerId, created.Value!.Id, Assert.Single(created.Value.Exercises).Id);
    }

    [Fact]
    public async Task SetExerciseTrainerRating_AssignedTrainer_SetsRatingAndTrimmedNote()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var (trainerId, sessionId, exerciseId) = await SetupTrainerAndExerciseAsync(service, db, setup);

        var result = await service.SetExerciseTrainerRatingAsync(trainerId, exerciseId, 5, "  Sauber ausgeführt  ");

        Assert.True(result.Succeeded);
        var reloaded = await service.GetByIdAsync(trainerId, sessionId);
        var saved = Assert.Single(reloaded.Value!.Exercises);
        Assert.Equal(5, saved.TrainerRating);
        Assert.Equal("Sauber ausgeführt", saved.TrainerNote);
    }

    [Fact]
    public async Task SetExerciseTrainerRating_OwnerNotAssignedTrainer_Fails()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var (_, _, exerciseId) = await SetupTrainerAndExerciseAsync(service, db, setup);

        // Der Besitzer selbst darf keine Trainer-Bewertung setzen (bewertet über Rating).
        var result = await service.SetExerciseTrainerRatingAsync(setup.UserId, exerciseId, 4, null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SetExerciseTrainerRating_RatingOutOfRange_Fails()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        var (trainerId, _, exerciseId) = await SetupTrainerAndExerciseAsync(service, db, setup);

        var result = await service.SetExerciseTrainerRatingAsync(trainerId, exerciseId, 6, null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetSessionsToRate_ShowsSessionWithHandlerAndExercises_UntilFeedbackAndAllRated()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var service = new TrainingService(db, new FakeNotificationService(), lookup);
        var setup = await SetupPlanAsync(db);
        lookup.Register(setup.UserId, "max@dogity.test", "Max", "Mustermann");
        var (trainerId, sessionId, exerciseId) = await SetupTrainerAndExerciseAsync(service, db, setup);

        var before = await service.GetSessionsToRateAsync(trainerId);
        Assert.True(before.Succeeded);
        var session = Assert.Single(before.Value!);
        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal("Bello", session.DogName);
        Assert.Equal("Max Mustermann", session.HandlerName);
        Assert.Null(session.TrainerFeedback);
        var ex = Assert.Single(session.Exercises);
        Assert.Equal(exerciseId, ex.ExerciseId);
        Assert.Null(ex.TrainerRating);

        // Nur Übung bewertet, aber noch kein Gesamt-Feedback -> Training bleibt offen.
        await service.SetExerciseTrainerRatingAsync(trainerId, exerciseId, 4, null);
        var afterRating = await service.GetSessionsToRateAsync(trainerId);
        Assert.Single(afterRating.Value!);

        // Zusätzlich Gesamt-Feedback -> Training ist vollständig bearbeitet und verschwindet.
        await service.SetFeedbackAsync(trainerId, sessionId, new SetFeedbackRequest("Gut gemacht."));
        var afterAll = await service.GetSessionsToRateAsync(trainerId);
        Assert.Empty(afterAll.Value!);
    }

    [Fact]
    public async Task GetSessionsToRate_ExcludesDogsWithoutTrainerAssignment()
    {
        var service = MakeService(out var db);
        var setup = await SetupPlanAsync(db);
        // Training anlegen, aber KEINE TrainerAssignment für den anfragenden Trainer.
        await service.CreateAsync(setup.UserId, MakeRequest(setup.DogId,
            new CreateTrainingExerciseRequest(setup.CatalogExerciseId, 3, ExerciseDifficulty.Beginner, true, null, setup.CatalogItemId)));

        var result = await service.GetSessionsToRateAsync(Guid.NewGuid());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
    }
}
