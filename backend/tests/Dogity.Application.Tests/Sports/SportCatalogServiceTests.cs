using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Sports;

/// <summary>
/// Testet die Ausgabe einer Prüfungsordnung: Die Übungen müssen in der
/// Reihenfolge der Prüfungsordnung stehen (RegulationExercise.SortOrder),
/// nicht in der zufälligen Reihenfolge der Datenbank - sonst stand auf den
/// öffentlichen Seiten z.B. "Sitz mit Abholen" vor der "Leinenführigkeit".
/// </summary>
public class SportCatalogServiceTests
{
    /// <summary>
    /// Legt eine Prüfungsordnung an. Die Übungen werden bewusst in einer
    /// ANDEREN Reihenfolge eingefügt, als ihr SortOrder vorgibt - sonst
    /// bestünde der Test auch ohne Sortierung.
    /// </summary>
    private static async Task<Guid> SeedRegulationAsync(
        ApplicationDbContext db, params (string Name, int SortOrder)[] exercises)
    {
        var sport = new Sport { Code = "IGP1", Name = "IGP 1" };
        db.Sports.Add(sport);
        var regulation = new Regulation { SportId = sport.Id, Name = "FCI-IGP 1" };
        db.Regulations.Add(regulation);
        var version = new RegulationVersion { RegulationId = regulation.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        db.RegulationVersions.Add(version);

        foreach (var (name, sortOrder) in exercises)
        {
            var exercise = new Exercise { SportId = sport.Id, Name = name };
            db.Exercises.Add(exercise);
            db.RegulationExercises.Add(new RegulationExercise
            {
                RegulationVersionId = version.Id,
                ExerciseId = exercise.Id,
                IsMandatory = true,
                MaxPoints = 10,
                SortOrder = sortOrder
            });
        }

        await db.SaveChangesAsync();
        return regulation.Id;
    }

    [Fact]
    public async Task GetRegulationDetail_ReturnsExercisesInRegulationOrder()
    {
        var db = InMemoryDbContext.Create();
        var regulationId = await SeedRegulationAsync(db,
            ("Sitz mit Abholen", 2),
            ("Leinenführigkeit", 0),
            ("Ablegen unter Ablenkung", 1));
        var service = new SportCatalogService(db);

        var result = await service.GetRegulationDetailAsync(regulationId);

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["Leinenführigkeit", "Ablegen unter Ablenkung", "Sitz mit Abholen"],
            result.Value!.Exercises.Select(e => e.ExerciseName));
    }

    [Fact]
    public async Task GetRegulationDetail_WithoutSortOrder_FallsBackToName()
    {
        // Alle Übungen ohne gepflegten SortOrder (Wert 0, z.B. direkt nach der
        // Migration und vor dem nächsten Seed-Durchlauf): dann muss die
        // Ausgabe wenigstens alphabetisch und damit stabil sein.
        var db = InMemoryDbContext.Create();
        var regulationId = await SeedRegulationAsync(db,
            ("Voran mit Platz", 0),
            ("Apportieren über die Hürde", 0),
            ("Leinenführigkeit", 0));
        var service = new SportCatalogService(db);

        var result = await service.GetRegulationDetailAsync(regulationId);

        Assert.True(result.Succeeded);
        Assert.Equal(
            ["Apportieren über die Hürde", "Leinenführigkeit", "Voran mit Platz"],
            result.Value!.Exercises.Select(e => e.ExerciseName));
    }
}
