using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Testet das nachträgliche Bearbeiten des Ablauf-Versuch-Kommentars
/// (GpsWalkRun.Comment) - inkl. Zugriffsschutz über den Hund der zugehörigen
/// Trainingseinheit (siehe Wunsch 1 / TODO).
/// </summary>
public class GpsTrackServiceTests
{
    private sealed record Setup(Guid UserId, Guid TrackId, Guid WalkRunId);

    private static async Task<(GpsTrackService Service, Setup Setup)> MakeAsync()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());

        var userId = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId, Role = DogOwnerRole.Owner });
        var session = new TrainingSession { UserId = userId, DogId = dog.Id, Date = DateOnly.FromDateTime(DateTime.Today), DurationMinutes = 30 };
        var track = new GpsTrack { TrainingSessionId = session.Id };
        var walkRun = new GpsWalkRun { TrackId = track.Id, Comment = null };
        db.TrainingSessions.Add(session);
        db.GpsTracks.Add(track);
        db.GpsWalkRuns.Add(walkRun);
        await db.SaveChangesAsync();

        return (service, new Setup(userId, track.Id, walkRun.Id));
    }

    [Fact]
    public async Task UpdateWalkRun_ByOwner_SetsAndTrimsComment()
    {
        var (service, s) = await MakeAsync();

        var result = await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("  bei Regen gelaufen  "));

        Assert.True(result.Succeeded);
        Assert.Equal("bei Regen gelaufen", result.Value!.Comment);
    }

    [Fact]
    public async Task UpdateWalkRun_EmptyComment_ClearsToNull()
    {
        var (service, s) = await MakeAsync();
        await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("erst was"));

        var result = await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("   "));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Comment);
    }

    [Fact]
    public async Task UpdateWalkRun_ByOtherUser_Fails()
    {
        var (service, s) = await MakeAsync();

        var result = await service.UpdateWalkRunAsync(Guid.NewGuid(), s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("fremd"));

        Assert.False(result.Succeeded);
    }

    // --- Fährte anlegen: mehrere Fährten pro Übungsstunde gehören in EINE Einheit ---

    private static readonly DateOnly Heute = DateOnly.FromDateTime(DateTime.Today);

    private static async Task<(Guid UserId, Guid DogId)> HundAnlegenAsync(IApplicationDbContext db)
    {
        var userId = Guid.NewGuid();
        var dog = new Dog { Name = "Rex" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return (userId, dog.Id);
    }

    private static CreateGpsTrackRequest Aufnahme(Guid dogId, DateOnly datum, int dauer = 5, Guid? id = null) =>
        new(null, 120, null, "Wiese", null, null, null,
            [new CreateGpsPointRequest(48.9, 8.5, DateTimeOffset.UtcNow, 5)],
            Id: id, DogId: dogId, Date: datum, DurationMinutes: dauer);

    [Fact]
    public async Task CreateTrack_OhneEinheitAmTag_LegtEineAn()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);

        var result = await service.CreateAsync(userId, Aufnahme(dogId, Heute, dauer: 7));

        Assert.True(result.Succeeded);
        var einheit = Assert.Single(db.TrainingSessions.Where(s => s.DogId == dogId));
        Assert.Equal(7, einheit.DurationMinutes);
        Assert.Equal(einheit.Id, Assert.Single(db.GpsTracks).TrainingSessionId);
    }

    [Fact]
    public async Task CreateTrack_ZweiteFaehrteAmSelbenTag_GehoertZurSelbenEinheit()
    {
        // Der Fall vom Hundeplatz: zwei Fährten in einer Übungsstunde. Vorher
        // entstanden zwei Einheiten, und der Tag zeigte alles doppelt.
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);

        await service.CreateAsync(userId, Aufnahme(dogId, Heute, dauer: 4));
        var zweite = await service.CreateAsync(userId, Aufnahme(dogId, Heute, dauer: 6));

        Assert.True(zweite.Succeeded);
        var einheit = Assert.Single(db.TrainingSessions.Where(s => s.DogId == dogId));
        Assert.Equal(10, einheit.DurationMinutes);
        Assert.Equal(2, db.GpsTracks.Count(t => t.TrainingSessionId == einheit.Id));
    }

    [Fact]
    public async Task CreateTrack_AmTagMitEingetragenemTraining_HaengtDortAn()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);
        db.TrainingSessions.Add(new TrainingSession { UserId = userId, DogId = dogId, Date = Heute, DurationMinutes = 30, Notes = "Unterordnung" });
        await db.SaveChangesAsync();

        var result = await service.CreateAsync(userId, Aufnahme(dogId, Heute, dauer: 5));

        Assert.True(result.Succeeded);
        var einheit = Assert.Single(db.TrainingSessions.Where(s => s.DogId == dogId));
        Assert.Equal(35, einheit.DurationMinutes);
        Assert.Equal("Unterordnung", einheit.Notes);
    }

    [Fact]
    public async Task CreateTrack_AnAnderemTag_BekommtEigeneEinheit()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);

        await service.CreateAsync(userId, Aufnahme(dogId, Heute));
        await service.CreateAsync(userId, Aufnahme(dogId, Heute.AddDays(-1)));

        Assert.Equal(2, db.TrainingSessions.Count(s => s.DogId == dogId));
    }

    [Fact]
    public async Task CreateTrack_GleicheIdZweimal_LegtNurEinmalAn()
    {
        // Die Offline-Warteschlange darf eine Anfrage wiederholen - dabei darf
        // weder eine zweite Fährte noch doppelte Dauer entstehen.
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);
        var id = Guid.NewGuid();

        await service.CreateAsync(userId, Aufnahme(dogId, Heute, dauer: 5, id: id));
        var wiederholt = await service.CreateAsync(userId, Aufnahme(dogId, Heute, dauer: 5, id: id));

        Assert.True(wiederholt.Succeeded);
        Assert.Equal(id, wiederholt.Value!.Id);
        Assert.Single(db.GpsTracks);
        Assert.Equal(5, Assert.Single(db.TrainingSessions.Where(s => s.DogId == dogId)).DurationMinutes);
    }

    [Fact]
    public async Task CreateTrack_FuerFremdenHund_LegtNichtsAn()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (_, dogId) = await HundAnlegenAsync(db);

        var result = await service.CreateAsync(Guid.NewGuid(), Aufnahme(dogId, Heute));

        Assert.False(result.Succeeded);
        Assert.Empty(db.TrainingSessions);
        Assert.Empty(db.GpsTracks);
    }

    [Fact]
    public async Task CreateTrack_MitTrainingSessionId_WieBisher()
    {
        // Alte Clients und schon wartende Offline-Anfragen schicken erst die
        // Einheit und dann die Fährte mit deren Id - das muss weiter gehen.
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);
        var session = new TrainingSession { UserId = userId, DogId = dogId, Date = Heute, DurationMinutes = 20 };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync();

        var result = await service.CreateAsync(userId, new CreateGpsTrackRequest(
            session.Id, 100, null, null, null, null, null,
            [new CreateGpsPointRequest(48.9, 8.5, DateTimeOffset.UtcNow, 5)]));

        Assert.True(result.Succeeded);
        Assert.Equal(session.Id, Assert.Single(db.GpsTracks).TrainingSessionId);
        Assert.Equal(20, db.TrainingSessions.Single(s => s.Id == session.Id).DurationMinutes);
    }
}
