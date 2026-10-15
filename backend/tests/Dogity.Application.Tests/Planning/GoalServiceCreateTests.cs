using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Ziel anlegen: Hat die Sportart Prüfungen, gehört eine davon zum Ziel.
///
/// Anlass (Rückmeldung eines Vereins, 2026-10-15): Ein Ziel "UPr" ohne gewählte
/// Stufe bekam einen Plan aus ALLEN Übungen der Sportart - darunter "Steh aus
/// der Bewegung", das erst ab UPr 2 geprüft wird. Nachgebaut mit einer Sportart,
/// deren Stufe 1 die Übung "Steh" nicht enthält, Stufe 2 aber schon.
/// </summary>
public class GoalServiceCreateTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Zieldatum = new(2027, 3, 1);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Aufbau(ApplicationDbContext Db, GoalService Dienst, Guid Halter, Guid HundId, Guid SportId, Guid Stufe1Id);

    private static async Task<Aufbau> AufsetzenAsync(bool mitPruefungen = true)
    {
        var db = InMemoryDbContext.Create();
        var dienst = new GoalService(db, new FesteUhr(), new FakeNotificationService(), new ExerciseMasteryService(db));

        var halter = Guid.NewGuid();
        var sport = new Sport { Code = "UPR", Name = "Unterordnung" };
        db.Sports.Add(sport);
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = halter, Role = DogOwnerRole.Owner });

        var freifolge = new Exercise { Name = "Freifolge", SportId = sport.Id, Difficulty = ExerciseDifficulty.Beginner };
        var sitz = new Exercise { Name = "Sitz aus der Bewegung", SportId = sport.Id, Difficulty = ExerciseDifficulty.Beginner };
        var steh = new Exercise { Name = "Steh aus der Bewegung", SportId = sport.Id, Difficulty = ExerciseDifficulty.Intermediate };
        db.Exercises.AddRange(freifolge, sitz, steh);

        var stufe1 = new Regulation { SportId = sport.Id, Name = "Stufe 1" };
        if (mitPruefungen)
        {
            var stufe2 = new Regulation { SportId = sport.Id, Name = "Stufe 2" };
            db.Regulations.AddRange(stufe1, stufe2);
            var v1 = new RegulationVersion { RegulationId = stufe1.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
            var v2 = new RegulationVersion { RegulationId = stufe2.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
            db.RegulationVersions.AddRange(v1, v2);
            db.RegulationExercises.AddRange(
                new RegulationExercise { RegulationVersionId = v1.Id, ExerciseId = freifolge.Id, IsMandatory = true, MaxPoints = 50 },
                new RegulationExercise { RegulationVersionId = v1.Id, ExerciseId = sitz.Id, IsMandatory = true, MaxPoints = 50 },
                new RegulationExercise { RegulationVersionId = v2.Id, ExerciseId = freifolge.Id, IsMandatory = true, MaxPoints = 40 },
                new RegulationExercise { RegulationVersionId = v2.Id, ExerciseId = sitz.Id, IsMandatory = true, MaxPoints = 30 },
                new RegulationExercise { RegulationVersionId = v2.Id, ExerciseId = steh.Id, IsMandatory = true, MaxPoints = 30 });
        }

        await db.SaveChangesAsync();
        return new Aufbau(db, dienst, halter, hund.Id, sport.Id, stufe1.Id);
    }

    [Fact]
    public async Task OhneGewaehltePruefung_ScheitertWennDieSportartPruefungenHat()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await a.Dienst.CreateAsync(a.Halter, new CreateGoalRequest(a.HundId, a.SportId, null, Zieldatum, null));

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(await a.Db.Goals.ToListAsync());
    }

    [Fact]
    public async Task MitStufe1_EnthaeltDerPlanNurDeren_Uebungen_KeinSteh()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await a.Dienst.CreateAsync(a.Halter, new CreateGoalRequest(a.HundId, a.SportId, a.Stufe1Id, Zieldatum, null));

        Assert.True(ergebnis.Succeeded);
        var uebungen = await a.Db.TrainingPlanItems
            .Select(i => i.Exercise!.Name)
            .Distinct()
            .ToListAsync();
        Assert.NotEmpty(uebungen);
        Assert.DoesNotContain("Steh aus der Bewegung", uebungen);
    }

    [Fact]
    public async Task IndividuellerPlan_GehtOhnePruefung_UndBleibtLeer()
    {
        var a = await AufsetzenAsync();

        var ergebnis = await a.Dienst.CreateAsync(a.Halter, new CreateGoalRequest(a.HundId, a.SportId, null, Zieldatum, null, IsCustom: true));

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(await a.Db.TrainingPlanItems.ToListAsync());
    }

    [Fact]
    public async Task SportartOhnePruefungen_BrauchtKeine_UndNimmtAlleUebungen()
    {
        var a = await AufsetzenAsync(mitPruefungen: false);

        var ergebnis = await a.Dienst.CreateAsync(a.Halter, new CreateGoalRequest(a.HundId, a.SportId, null, Zieldatum, null));

        Assert.True(ergebnis.Succeeded);
        Assert.NotEmpty(await a.Db.TrainingPlanItems.ToListAsync());
    }
}
