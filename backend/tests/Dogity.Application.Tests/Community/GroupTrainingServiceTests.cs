using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Testet die Vereins-Trainingsbibliothek (siehe docs/GROUP_TRAINING_LIBRARY.md):
/// verein-weit geteilte Bausteine + daraus zusammengestellte Einheiten,
/// Zugriff nur für ClubTrainer.
/// </summary>
public class GroupTrainingServiceTests
{
    private static GroupTrainingService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new GroupTrainingService(db);
    }

    /// <summary>Legt einen Verein an und macht userId zum Vereinstrainer. Gibt die ClubId zurück.</summary>
    private static async Task<Guid> MakeClubTrainerAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db, Guid userId, string name = "Verein")
    {
        var club = new Club { Name = name };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = userId });
        await db.SaveChangesAsync();
        return club.Id;
    }

    private static UpsertExerciseRequest Ex(string title, int? min = 10, GroupTrainingCategory cat = GroupTrainingCategory.Puppy, GroupExamTarget exams = GroupExamTarget.None) =>
        new(cat, title, "Fokus", min, "Ablauf", exams);

    [Fact]
    public async Task GetLibrary_NonClubTrainer_Fails()
    {
        var service = MakeService(out var db);
        var clubId = await MakeClubTrainerAsync(db, Guid.NewGuid());

        var result = await service.GetLibraryAsync(Guid.NewGuid(), clubId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateExercise_ClubTrainer_AppearsInLibrary()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);

        var created = await service.CreateExerciseAsync(userId, clubId, Ex("Sitz aus Bewegung"));
        Assert.True(created.Succeeded);

        var lib = await service.GetLibraryAsync(userId, clubId);
        Assert.True(lib.Succeeded);
        Assert.Single(lib.Value!.Exercises);
        Assert.Equal("Sitz aus Bewegung", lib.Value.Exercises[0].Title);
        Assert.Equal("Verein", lib.Value.ClubName);
    }

    [Fact]
    public async Task CreateExercise_NonTrainer_Fails()
    {
        var service = MakeService(out var db);
        var clubId = await MakeClubTrainerAsync(db, Guid.NewGuid());

        var result = await service.CreateExerciseAsync(Guid.NewGuid(), clubId, Ex("X"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ExamTargets_Flags_RoundTrip()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);

        var created = await service.CreateExerciseAsync(userId, clubId,
            Ex("Leinenführigkeit", cat: GroupTrainingCategory.Basis, exams: GroupExamTarget.BH | GroupExamTarget.IBGH1));

        Assert.True(created.Succeeded);
        Assert.True(created.Value!.ExamTargets.HasFlag(GroupExamTarget.BH));
        Assert.True(created.Value.ExamTargets.HasFlag(GroupExamTarget.IBGH1));
        Assert.False(created.Value.ExamTargets.HasFlag(GroupExamTarget.IBGH3));
    }

    [Fact]
    public async Task CreateUnit_ComposesExercisesInGivenOrder_AndSumsMinutes()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);
        var a = (await service.CreateExerciseAsync(userId, clubId, Ex("A", min: 10))).Value!;
        var b = (await service.CreateExerciseAsync(userId, clubId, Ex("B", min: 5))).Value!;

        var unit = await service.CreateUnitAsync(userId, clubId,
            new UpsertUnitRequest(GroupTrainingCategory.Puppy, "Welpenstunde 1", "Beschr", [b.Id, a.Id]));

        Assert.True(unit.Succeeded);
        Assert.Equal(new[] { "B", "A" }, unit.Value!.Items.Select(i => i.Exercise.Title).ToArray());
        Assert.Equal(15, unit.Value.TotalMinutes);
        Assert.Equal(0, unit.Value.Items[0].SortOrder);
        Assert.Equal(1, unit.Value.Items[1].SortOrder);
    }

    [Fact]
    public async Task CreateUnit_WithForeignClubExercise_Fails()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);
        // Baustein eines fremden Vereins direkt einfügen.
        var foreign = new GroupTrainingExercise { ClubId = Guid.NewGuid(), Title = "Fremd", Category = GroupTrainingCategory.Puppy };
        db.GroupTrainingExercises.Add(foreign);
        await db.SaveChangesAsync();

        var result = await service.CreateUnitAsync(userId, clubId,
            new UpsertUnitRequest(GroupTrainingCategory.Puppy, "Stunde", null, [foreign.Id]));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateUnit_ReplacesItems()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);
        var a = (await service.CreateExerciseAsync(userId, clubId, Ex("A"))).Value!;
        var b = (await service.CreateExerciseAsync(userId, clubId, Ex("B"))).Value!;
        var unit = (await service.CreateUnitAsync(userId, clubId, new UpsertUnitRequest(GroupTrainingCategory.Puppy, "U", null, [a.Id, b.Id]))).Value!;

        var updated = await service.UpdateUnitAsync(userId, unit.Id,
            new UpsertUnitRequest(GroupTrainingCategory.YoungDog, "U neu", null, [b.Id]));

        Assert.True(updated.Succeeded);
        Assert.Equal(GroupTrainingCategory.YoungDog, updated.Value!.Category);
        Assert.Single(updated.Value.Items);
        Assert.Equal("B", updated.Value.Items[0].Exercise.Title);
    }

    [Fact]
    public async Task DeleteExercise_RemovesItFromUnits()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);
        var a = (await service.CreateExerciseAsync(userId, clubId, Ex("A"))).Value!;
        var b = (await service.CreateExerciseAsync(userId, clubId, Ex("B"))).Value!;
        var unit = (await service.CreateUnitAsync(userId, clubId, new UpsertUnitRequest(GroupTrainingCategory.Puppy, "U", null, [a.Id, b.Id]))).Value!;

        var del = await service.DeleteExerciseAsync(userId, a.Id);
        Assert.True(del.Succeeded);

        var lib = await service.GetLibraryAsync(userId, clubId);
        Assert.Single(lib.Value!.Exercises); // nur noch B
        var reloadedUnit = lib.Value.Units.Single(u => u.Id == unit.Id);
        Assert.Single(reloadedUnit.Items); // A-Referenz ist raus
        Assert.Equal("B", reloadedUnit.Items[0].Exercise.Title);
    }

    [Fact]
    public async Task DuplicateUnit_CopiesItemsWithKopieSuffix()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);
        var a = (await service.CreateExerciseAsync(userId, clubId, Ex("A"))).Value!;
        var b = (await service.CreateExerciseAsync(userId, clubId, Ex("B"))).Value!;
        var unit = (await service.CreateUnitAsync(userId, clubId, new UpsertUnitRequest(GroupTrainingCategory.Basis, "Basis 1", null, [a.Id, b.Id]))).Value!;

        var copy = await service.DuplicateUnitAsync(userId, unit.Id);

        Assert.True(copy.Succeeded);
        Assert.NotEqual(unit.Id, copy.Value!.Id);
        Assert.Contains("Kopie", copy.Value.Title);
        Assert.Equal(2, copy.Value.Items.Count);

        var lib = await service.GetLibraryAsync(userId, clubId);
        Assert.Equal(2, lib.Value!.Units.Count); // Original + Kopie
    }

    [Fact]
    public async Task DeleteUnit_RemovesFromLibrary_ExercisesRemain()
    {
        var service = MakeService(out var db);
        var userId = Guid.NewGuid();
        var clubId = await MakeClubTrainerAsync(db, userId);
        var a = (await service.CreateExerciseAsync(userId, clubId, Ex("A"))).Value!;
        var unit = (await service.CreateUnitAsync(userId, clubId, new UpsertUnitRequest(GroupTrainingCategory.Puppy, "U", null, [a.Id]))).Value!;

        var del = await service.DeleteUnitAsync(userId, unit.Id);

        Assert.True(del.Succeeded);
        var lib = await service.GetLibraryAsync(userId, clubId);
        Assert.Empty(lib.Value!.Units);
        Assert.Single(lib.Value.Exercises); // Baustein bleibt erhalten
    }
}
