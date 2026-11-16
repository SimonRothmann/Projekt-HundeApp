using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Die Kennung vom Gerät (ClientRequestId): Dieselbe Anfrage aus der Offline-Warteschlange legt nichts
/// doppelt an - auch dann nicht, wenn sie an die Einheit des Tages angehängt wurde, wo es keine eigene
/// Einheit gibt. Vorher addierte jedes erneute Senden Dauer und Übungen noch einmal.
///
/// Der eindeutige Index selbst greift auf dem In-Memory-Ersatz nicht; geprüft wird die Logik im Dienst.
/// </summary>
public class TrainingAnfragekennungTests
{
    private static readonly DateOnly Tag = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

    private static TrainingService MakeService(out ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService(), TimeProvider.System);
    }

    private static async Task<(Guid UserId, Guid DogId)> HundAnlegenAsync(ApplicationDbContext db)
    {
        var userId = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = userId, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return (userId, hund.Id);
    }

    private static CreateTrainingSessionRequest Anfrage(Guid dogId, Guid? kennung, int minuten = 10, params string[] uebungen) => new(
        dogId,
        Tag,
        minuten,
        null,
        uebungen.Select(u => new CreateTrainingExerciseRequest(null, 4, ExerciseDifficulty.Beginner, true, null, null, u)).ToList(),
        ClientRequestId: kennung);

    [Fact]
    public async Task NeuerTag_DasErneuteSendenLegtKeineZweiteEinheitAnUndAntwortetMitDerVorhandenen()
    {
        var dienst = MakeService(out var db);
        var (user, hund) = await HundAnlegenAsync(db);
        var kennung = Guid.NewGuid();

        var erste = await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Sitz"));
        var zweite = await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Sitz"));

        Assert.True(erste.Succeeded);
        Assert.True(zweite.Succeeded);
        Assert.Equal(erste.Value!.Id, zweite.Value!.Id);
        Assert.Equal(1, await db.TrainingSessions.CountAsync());
        Assert.Equal(1, await db.TrainingExercises.CountAsync());
        Assert.Equal(10, zweite.Value.DurationMinutes);
    }

    [Fact]
    public async Task AnDieEinheitDesTagesAngehaengt_DasErneuteSendenAddiertNichtNocheinmal()
    {
        var dienst = MakeService(out var db);
        var (user, hund) = await HundAnlegenAsync(db);
        Assert.True((await dienst.CreateAsync(user, Anfrage(hund, null, 20, "Sitz"))).Succeeded);
        var kennung = Guid.NewGuid();

        var erste = await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Platz", "Bleib"));
        var zweite = await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Platz", "Bleib"));

        Assert.True(erste.Succeeded);
        Assert.True(zweite.Succeeded);
        Assert.Equal(erste.Value!.Id, zweite.Value!.Id);
        Assert.Equal(1, await db.TrainingSessions.CountAsync());
        Assert.Equal(3, await db.TrainingExercises.CountAsync());
        Assert.Equal(30, (await db.TrainingSessions.SingleAsync()).DurationMinutes);
    }

    [Fact]
    public async Task NurDieErsteUebungTraegtDieKennung()
    {
        var dienst = MakeService(out var db);
        var (user, hund) = await HundAnlegenAsync(db);
        var kennung = Guid.NewGuid();

        await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Sitz", "Platz"));

        Assert.Single(await db.TrainingExercises.Where(e => e.ClientRequestId == kennung).ToListAsync());
    }

    [Fact]
    public async Task OhneKennung_BleibtEsBeimBisherigenVerhalten()
    {
        var dienst = MakeService(out var db);
        var (user, hund) = await HundAnlegenAsync(db);

        await dienst.CreateAsync(user, Anfrage(hund, null, 10, "Sitz"));
        await dienst.CreateAsync(user, Anfrage(hund, null, 10, "Sitz"));

        // Angehängt an die Einheit des Tages: zwei Übungen, 20 Minuten.
        Assert.Equal(2, await db.TrainingExercises.CountAsync());
        Assert.Equal(20, (await db.TrainingSessions.SingleAsync()).DurationMinutes);
    }

    [Fact]
    public async Task ZweiVerschiedeneKennungen_SindZweiAnfragen()
    {
        var dienst = MakeService(out var db);
        var (user, hund) = await HundAnlegenAsync(db);

        await dienst.CreateAsync(user, Anfrage(hund, Guid.NewGuid(), 10, "Sitz"));
        await dienst.CreateAsync(user, Anfrage(hund, Guid.NewGuid(), 10, "Sitz"));

        Assert.Equal(2, await db.TrainingExercises.CountAsync());
        Assert.Equal(20, (await db.TrainingSessions.SingleAsync()).DurationMinutes);
    }

    [Fact]
    public async Task InzwischenGeloeschtesTraining_WirdNichtNeuAngelegt()
    {
        var dienst = MakeService(out var db);
        var (user, hund) = await HundAnlegenAsync(db);
        var kennung = Guid.NewGuid();
        var erste = await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Sitz"));
        (await db.TrainingSessions.SingleAsync(s => s.Id == erste.Value!.Id)).DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var zweite = await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Sitz"));

        Assert.True(zweite.IsNotFound);
        Assert.Equal(1, await db.TrainingSessions.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task KennungEinesAnderenHundes_IstEin404()
    {
        var dienst = MakeService(out var db);
        var (user, hund) = await HundAnlegenAsync(db);
        var zweiterHund = new Dog { Name = "Rex" };
        db.Dogs.Add(zweiterHund);
        db.DogOwners.Add(new DogOwner { DogId = zweiterHund.Id, UserId = user, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        var kennung = Guid.NewGuid();
        Assert.True((await dienst.CreateAsync(user, Anfrage(hund, kennung, 10, "Sitz"))).Succeeded);

        var ergebnis = await dienst.CreateAsync(user, Anfrage(zweiterHund.Id, kennung, 10, "Sitz"));

        Assert.True(ergebnis.IsNotFound);
        Assert.Equal(1, await db.TrainingExercises.CountAsync());
    }
}
