using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Hoopers im echten Katalog (VDH-PO Hoopers ab 01.01.2027): vier Klassen,
/// gewertet nur über Fehlerpunkte - und ein Ziel bekommt einen Plan, der nicht
/// nur aus den vier Geräten besteht, sondern auch das Führen auf Distanz übt.
/// </summary>
public class HoopersKatalogTests
{
    private static readonly string[] Fertigkeiten =
    [
        "Start vor dem Starthoop", "Distanz zum Führbereich", "Richtungszeichen",
        "Flüssiger Lauf", "Reihenfolge und Verleitungen", "Zielhoop und Abschluss",
    ];

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private static async Task<Dogity.Infrastructure.Persistence.ApplicationDbContext> KatalogAsync()
    {
        var db = InMemoryDbContext.Create();
        var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        await SportCatalogSeeder.SeedAsync(services);
        return db;
    }

    [Fact]
    public async Task VierKlassen_AlleFassung2027_OhneUebungspunkte()
    {
        await using var db = await KatalogAsync();

        var sport = await db.Sports.SingleAsync(s => s.Code == "HOOPERS");
        var klassen = await db.Regulations.Where(r => r.SportId == sport.Id).Select(r => r.Name).OrderBy(n => n).ToListAsync();
        Assert.Equal(["Hoopers 0 (H0)", "Hoopers 1 (H1)", "Hoopers 2 (H2)", "Hoopers 3 (H3)"], klassen);

        var verknuepfungen = await db.RegulationExercises
            .Where(re => re.RegulationVersion!.Regulation!.SportId == sport.Id)
            .Select(re => new { re.RegulationVersion!.VersionLabel, re.RegulationVersion.ValidFrom, re.MaxPoints, re.IsMandatory })
            .ToListAsync();
        Assert.Equal(40, verknuepfungen.Count);
        Assert.All(verknuepfungen, v =>
        {
            Assert.Equal("2027", v.VersionLabel);
            Assert.Equal(new DateOnly(2027, 1, 1), v.ValidFrom);
            Assert.Equal(0, v.MaxPoints);
            Assert.True(v.IsMandatory);
        });
    }

    [Fact]
    public async Task ZielH1_PlanUebtGeraeteUndFuehrenAufDistanz()
    {
        await using var db = await KatalogAsync();
        var sport = await db.Sports.SingleAsync(s => s.Code == "HOOPERS");
        var h1 = await db.Regulations.SingleAsync(r => r.Name == "Hoopers 1 (H1)");
        var halter = Guid.NewGuid();
        var hund = new Dog { Name = "Flocke" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = halter, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        var dienst = new GoalService(db, new FesteUhr(), new FakeNotificationService(), new ExerciseMasteryService(db));

        var ergebnis = await dienst.CreateAsync(halter, new CreateGoalRequest(hund.Id, sport.Id, h1.Id, new DateOnly(2027, 5, 1), null));

        Assert.True(ergebnis.Succeeded);
        var imPlan = await db.TrainingPlanItems.Select(i => i.Exercise!.Name).Distinct().ToListAsync();
        string[] erwartet = ["Hoop", "Tonne", "Gate", "Tunnel", .. Fertigkeiten];
        Assert.Superset(erwartet.ToHashSet(), imPlan.ToHashSet());
    }
}
