using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Testet die Admin-Bearbeitung von Sportarten und Prüfungsordnungen:
/// Berechtigung (global = nur Admin), Punkte/Pflicht ändern, Übung
/// hinzufügen/entfernen.
/// </summary>
public class RegulationManagementServiceTests
{
    private static RegulationManagementService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new RegulationManagementService(db);
    }

    private static async Task<(Guid SportId, Guid RegulationId, Guid FreeExerciseId, Guid LinkedExerciseId)> SeedGlobalRegulationAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var sport = new Sport { Code = "BH", Name = "Begleithund" };
        db.Sports.Add(sport);
        var linked = new Exercise { SportId = sport.Id, Name = "Fußarbeit" };
        var free = new Exercise { SportId = sport.Id, Name = "Sitz aus der Bewegung" };
        db.Exercises.AddRange(linked, free);
        var regulation = new Regulation { SportId = sport.Id, Name = "BH" };
        db.Regulations.Add(regulation);
        var version = new RegulationVersion { RegulationId = regulation.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        db.RegulationVersions.Add(version);
        db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = version.Id, ExerciseId = linked.Id, IsMandatory = true, MaxPoints = 60 });
        await db.SaveChangesAsync();
        return (sport.Id, regulation.Id, free.Id, linked.Id);
    }

    [Fact]
    public async Task UpdateSport_AsAdmin_Succeeds()
    {
        var service = MakeService(out var db);
        var (sportId, _, _, _) = await SeedGlobalRegulationAsync(db);

        var result = await service.UpdateSportAsync(Guid.NewGuid(), isAdmin: true, sportId, new UpdateSportRequest("Begleithundeprüfung", "Neu"));

        Assert.True(result.Succeeded);
        Assert.Equal("Begleithundeprüfung", (await db.Sports.SingleAsync(s => s.Id == sportId)).Name);
    }

    [Fact]
    public async Task UpdateSport_GlobalAsNonAdmin_Fails()
    {
        var service = MakeService(out var db);
        var (sportId, _, _, _) = await SeedGlobalRegulationAsync(db);

        var result = await service.UpdateSportAsync(Guid.NewGuid(), isAdmin: false, sportId, new UpdateSportRequest("X", null));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UpdateRegulationExercise_AsAdmin_ChangesPointsAndMandatory()
    {
        var service = MakeService(out var db);
        var (_, regulationId, _, linkedExerciseId) = await SeedGlobalRegulationAsync(db);

        var result = await service.UpdateRegulationExerciseAsync(
            Guid.NewGuid(), isAdmin: true, regulationId, linkedExerciseId,
            new UpdateRegulationExerciseRequest(IsMandatory: false, MaxPoints: 45, ScoringNotes: "korrigiert"));

        Assert.True(result.Succeeded);
        var link = await db.RegulationExercises.SingleAsync(re => re.ExerciseId == linkedExerciseId);
        Assert.Equal(45, link.MaxPoints);
        Assert.False(link.IsMandatory);
        Assert.Equal("korrigiert", link.ScoringNotes);
    }

    [Fact]
    public async Task UpdateRegulationExercise_GlobalAsNonAdmin_Fails()
    {
        var service = MakeService(out var db);
        var (_, regulationId, _, linkedExerciseId) = await SeedGlobalRegulationAsync(db);

        var result = await service.UpdateRegulationExerciseAsync(
            Guid.NewGuid(), isAdmin: false, regulationId, linkedExerciseId,
            new UpdateRegulationExerciseRequest(true, 10, null));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AddRegulationExercise_New_Succeeds_Duplicate_Fails()
    {
        var service = MakeService(out var db);
        var (_, regulationId, freeExerciseId, linkedExerciseId) = await SeedGlobalRegulationAsync(db);

        var add = await service.AddRegulationExerciseAsync(Guid.NewGuid(), true, regulationId, new AddRegulationExerciseRequest(freeExerciseId, true, 20, null));
        Assert.True(add.Succeeded);

        var dup = await service.AddRegulationExerciseAsync(Guid.NewGuid(), true, regulationId, new AddRegulationExerciseRequest(linkedExerciseId, true, 10, null));
        Assert.False(dup.Succeeded);
    }

    [Fact]
    public async Task AddRegulationExercise_AppendsAtEndOfRegulation()
    {
        var service = MakeService(out var db);
        var (_, regulationId, freeExerciseId, linkedExerciseId) = await SeedGlobalRegulationAsync(db);

        // Bestehende Übung steht an Position 4 der Prüfungsordnung.
        var existing = await db.RegulationExercises.SingleAsync(re => re.ExerciseId == linkedExerciseId);
        existing.SortOrder = 4;
        await db.SaveChangesAsync();

        var add = await service.AddRegulationExerciseAsync(
            Guid.NewGuid(), true, regulationId, new AddRegulationExerciseRequest(freeExerciseId, true, 20, null));

        Assert.True(add.Succeeded);
        var link = await db.RegulationExercises.SingleAsync(re => re.ExerciseId == freeExerciseId);
        Assert.Equal(5, link.SortOrder);
    }

    [Fact]
    public async Task UpdateRegulationExercise_KeepsSortOrder()
    {
        var service = MakeService(out var db);
        var (_, regulationId, _, linkedExerciseId) = await SeedGlobalRegulationAsync(db);
        var link = await db.RegulationExercises.SingleAsync(re => re.ExerciseId == linkedExerciseId);
        link.SortOrder = 7;
        await db.SaveChangesAsync();

        var result = await service.UpdateRegulationExerciseAsync(
            Guid.NewGuid(), isAdmin: true, regulationId, linkedExerciseId,
            new UpdateRegulationExerciseRequest(IsMandatory: true, MaxPoints: 30, ScoringNotes: null));

        Assert.True(result.Succeeded);
        Assert.Equal(7, (await db.RegulationExercises.SingleAsync(re => re.ExerciseId == linkedExerciseId)).SortOrder);
    }

    [Fact]
    public async Task RemoveRegulationExercise_SoftDeletesLink()
    {
        var service = MakeService(out var db);
        var (_, regulationId, _, linkedExerciseId) = await SeedGlobalRegulationAsync(db);

        var result = await service.RemoveRegulationExerciseAsync(Guid.NewGuid(), true, regulationId, linkedExerciseId);

        Assert.True(result.Succeeded);
        Assert.False(await db.RegulationExercises.AnyAsync(re => re.ExerciseId == linkedExerciseId));
        Assert.True(await db.RegulationExercises.IgnoreQueryFilters().AnyAsync(re => re.ExerciseId == linkedExerciseId && re.DeletedAt != null));
    }
}
