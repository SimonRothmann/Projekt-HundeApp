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

    // --- Zu lange Texte (Prüfung 2026-09-28) --------------------------------
    // Vorher scheiterten sie erst in der Datenbank: 500er ohne Meldung, und
    // ein offline erfasster Eintrag hielt die Warteschlange für immer an.

    [Fact]
    public async Task CreateTrack_ZuLangeMarkerBeschriftung_WirdAbgelehntUndNichtsGespeichert()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);
        var anfrage = Aufnahme(dogId, Heute) with
        {
            Points = [new CreateGpsPointRequest(48.9, 8.5, DateTimeOffset.UtcNow, 5, GpsPointType.Manual, new string('x', 201))],
        };

        var result = await service.CreateAsync(userId, anfrage);

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
        Assert.Contains("zu lang", result.Errors[0]);
        Assert.Empty(db.GpsTracks);
        Assert.Empty(db.TrainingSessions);
    }

    [Fact]
    public async Task CreateTrack_KommentarGenauAnDerGrenze_GehtDurch()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);

        var ok = await service.CreateAsync(userId, Aufnahme(dogId, Heute) with { Comment = new string('x', 2000) });
        var zuLang = await service.CreateAsync(userId, Aufnahme(dogId, Heute) with { Comment = new string('x', 2001) });

        Assert.True(ok.Succeeded);
        Assert.False(zuLang.Succeeded);
    }

    [Fact]
    public async Task UpdateWalkRun_ZuLangerKommentar_BleibtBeimAlten()
    {
        var (service, s) = await MakeAsync();
        await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("bei Regen"));

        var result = await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest(new string('x', 2001)));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateTrack_MehrAlsHoechstzahlPunkte_WirdAbgelehnt()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);
        var punkte = Enumerable.Range(0, GpsTrackService.MaxPunkteJeAufzeichnung + 1)
            .Select(i => new CreateGpsPointRequest(48.9 + i * 1e-6, 8.5, DateTimeOffset.UtcNow.AddSeconds(i), 5))
            .ToList();

        var result = await service.CreateAsync(userId, Aufnahme(dogId, Heute) with { Points = punkte });

        Assert.False(result.Succeeded);
        Assert.Contains("50.000", result.Errors[0]);
        Assert.Empty(db.GpsTracks);
    }

    [Fact]
    public async Task AddWalkRun_ZuGrossFuerDieAuswertung_GiltAlsErledigtOhneKennzahlen()
    {
        // Sonst lüde die Nachauswertung ihn bei jedem Serverstart erneut.
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var (userId, dogId) = await HundAnlegenAsync(db);
        var t0 = DateTimeOffset.UtcNow;
        var zickzack = Enumerable.Range(0, 4_999)
            .Select(i => new CreateGpsPointRequest(48.9 + i * 5e-6, i % 2 == 0 ? 8.5 : 8.5002, t0.AddSeconds(i), 5))
            .ToList();
        var faehrte = await service.CreateAsync(userId, Aufnahme(dogId, Heute) with { Points = zickzack });
        var ablauf = Enumerable.Range(0, 45_000)
            .Select(i => new CreateGpsWalkPointRequest(48.9 + i * 1e-7, 8.5, t0.AddSeconds(i), 5))
            .ToList();

        var result = await service.AddWalkRunAsync(userId, faehrte.Value!.Id, new CreateGpsWalkRunRequest(null, null, ablauf));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value!.EvaluatedAt);
        Assert.Null(result.Value.AvgDeviationMeters);
        Assert.NotNull(db.GpsWalkRuns.Single().EvaluatedAt);
    }

    [Fact]
    public async Task AddWalkRun_TrainingstagSchonVoll_WirdAbgelehnt()
    {
        // Kleine Grenze statt 200.000 - geprüft wird dieselbe Zählung über
        // Legungen und Abläufe des Tages.
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db, new FakeWeatherEnrichmentService(), punkteJeTrainingstag: 100);
        var (userId, dogId) = await HundAnlegenAsync(db);
        var t0 = DateTimeOffset.UtcNow;
        var faehrte = await service.CreateAsync(userId, Aufnahme(dogId, Heute)); // 1 Punkt
        var ablauf = (int n) => Enumerable.Range(0, n)
            .Select(i => new CreateGpsWalkPointRequest(48.9, 8.5, t0.AddSeconds(i), 5))
            .ToList();
        Assert.True((await service.AddWalkRunAsync(userId, faehrte.Value!.Id, new CreateGpsWalkRunRequest(null, null, ablauf(90)))).Succeeded);

        var result = await service.AddWalkRunAsync(userId, faehrte.Value.Id, new CreateGpsWalkRunRequest(null, null, ablauf(10)));
        var zweiteFaehrte = await service.CreateAsync(userId, Aufnahme(dogId, Heute) with
        {
            Points = Enumerable.Range(0, 10).Select(i => new CreateGpsPointRequest(48.9, 8.5, t0.AddSeconds(i), 5)).ToList(),
        });

        Assert.False(result.Succeeded);
        Assert.Contains("höchstens 100", result.Errors[0]);
        Assert.False(zweiteFaehrte.Succeeded);
        Assert.Single(db.GpsWalkRuns);
        // Genau bis an die Grenze geht es noch.
        Assert.True((await service.AddWalkRunAsync(userId, faehrte.Value.Id, new CreateGpsWalkRunRequest(null, null, ablauf(9)))).Succeeded);
    }
}
