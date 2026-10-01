using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Fährten gelöschter Trainings: Das Training selbst bekam ein DeletedAt, die
/// Fährten samt GPS-Punkten blieben stehen. Geprüft wird mit IgnoreQueryFilters
/// auf DeletedAt - ohne wäre "ausgeblendet" nicht von "gelöscht" zu trennen.
/// </summary>
public class GpsTrackRemovalTests
{
    private sealed record Faehrte(Guid TrackId, Guid RunId);

    private static TrainingService Dienst(ApplicationDbContext db) =>
        new(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService());

    private static TrainingSession Training(ApplicationDbContext db, Guid nutzer, Guid? hund = null)
    {
        var dogId = hund ?? Guid.NewGuid();
        if (hund is null)
        {
            db.Dogs.Add(new Dog { Id = dogId, Name = "Bello" });
            db.DogOwners.Add(new DogOwner { DogId = dogId, UserId = nutzer });
        }
        var s = new TrainingSession { UserId = nutzer, DogId = dogId, Date = new DateOnly(2026, 9, 1), DurationMinutes = 30 };
        db.TrainingSessions.Add(s);
        return s;
    }

    /// <summary>Fährte mit Punkt, Ablauf, Ablaufpunkt und Stockung - alles, was daran hängen kann.</summary>
    private static Faehrte VollstaendigeFaehrte(ApplicationDbContext db, Guid trainingId)
    {
        var track = new GpsTrack { TrainingSessionId = trainingId, LengthMeters = 300 };
        db.GpsTracks.Add(track);
        db.GpsPoints.Add(new GpsPoint { TrackId = track.Id, Latitude = 48.9, Longitude = 8.5, Timestamp = DateTimeOffset.UtcNow });
        var run = new GpsWalkRun { TrackId = track.Id };
        db.GpsWalkRuns.Add(run);
        db.GpsWalkPoints.Add(new GpsWalkPoint { WalkRunId = run.Id, Latitude = 48.9, Longitude = 8.5, Timestamp = DateTimeOffset.UtcNow });
        db.GpsWalkStops.Add(new GpsWalkStop { WalkRunId = run.Id });
        return new Faehrte(track.Id, run.Id);
    }

    private static async Task AssertAllesGeloeschtAsync(ApplicationDbContext db, Faehrte f)
    {
        Assert.NotNull((await db.GpsTracks.IgnoreQueryFilters().SingleAsync(t => t.Id == f.TrackId)).DeletedAt);
        Assert.All(db.GpsPoints.IgnoreQueryFilters().Where(p => p.TrackId == f.TrackId), p => Assert.NotNull(p.DeletedAt));
        Assert.NotNull((await db.GpsWalkRuns.IgnoreQueryFilters().SingleAsync(r => r.Id == f.RunId)).DeletedAt);
        Assert.All(db.GpsWalkPoints.IgnoreQueryFilters().Where(p => p.WalkRunId == f.RunId), p => Assert.NotNull(p.DeletedAt));
        Assert.All(db.GpsWalkStops.IgnoreQueryFilters().Where(s => s.WalkRunId == f.RunId), s => Assert.NotNull(s.DeletedAt));
    }

    private static async Task AssertNichtsGeloeschtAsync(ApplicationDbContext db, Faehrte f)
    {
        Assert.Null((await db.GpsTracks.IgnoreQueryFilters().SingleAsync(t => t.Id == f.TrackId)).DeletedAt);
        Assert.All(db.GpsPoints.IgnoreQueryFilters().Where(p => p.TrackId == f.TrackId), p => Assert.Null(p.DeletedAt));
        Assert.Null((await db.GpsWalkRuns.IgnoreQueryFilters().SingleAsync(r => r.Id == f.RunId)).DeletedAt);
        Assert.All(db.GpsWalkPoints.IgnoreQueryFilters().Where(p => p.WalkRunId == f.RunId), p => Assert.Null(p.DeletedAt));
        Assert.All(db.GpsWalkStops.IgnoreQueryFilters().Where(s => s.WalkRunId == f.RunId), s => Assert.Null(s.DeletedAt));
    }

    [Fact]
    public async Task TrainingLoeschen_LoeschtSeineFaehrtenSamtAllemMitWeich()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var geloescht = Training(db, nutzer);
        var behalten = Training(db, nutzer);
        var seine = VollstaendigeFaehrte(db, geloescht.Id);
        var zweite = VollstaendigeFaehrte(db, geloescht.Id); // mehrere Fährten je Training
        var fremde = VollstaendigeFaehrte(db, behalten.Id);
        await db.SaveChangesAsync();

        var ergebnis = await Dienst(db).DeleteAsync(nutzer, geloescht.Id);

        Assert.True(ergebnis.Succeeded);
        await AssertAllesGeloeschtAsync(db, seine);
        await AssertAllesGeloeschtAsync(db, zweite);
        await AssertNichtsGeloeschtAsync(db, fremde);
        Assert.Single(db.GpsTracks); // über die normale Abfrage bleibt nur die andere
    }

    [Fact]
    public async Task TrainingLoeschen_BehaeltDenFrueherenLoeschzeitpunktEinerEinzelGeloeschtenFaehrte()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var training = Training(db, nutzer);
        var f = VollstaendigeFaehrte(db, training.Id);
        await db.SaveChangesAsync();
        var frueher = DateTimeOffset.UtcNow.AddDays(-3);
        (await db.GpsTracks.SingleAsync()).DeletedAt = frueher; // Altbestand: früher setzte GpsTrackService.DeleteAsync nur die Fährte selbst
        await db.SaveChangesAsync();

        await Dienst(db).DeleteAsync(nutzer, training.Id);

        Assert.Equal(frueher, (await db.GpsTracks.IgnoreQueryFilters().SingleAsync()).DeletedAt);
        await AssertAllesGeloeschtAsync(db, f);
    }

    [Fact]
    public async Task EinzelneFaehrteLoeschen_LoeschtPunkteAblaeufeUndStockungenMit_AndereFaehrteBleibt()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var training = Training(db, nutzer);
        var weg = VollstaendigeFaehrte(db, training.Id);
        var bleibt = VollstaendigeFaehrte(db, training.Id);
        await db.SaveChangesAsync();

        var ergebnis = await new GpsTrackService(db, new FakeWeatherEnrichmentService()).DeleteAsync(nutzer, weg.TrackId);

        Assert.True(ergebnis.Succeeded);
        await AssertAllesGeloeschtAsync(db, weg);
        await AssertNichtsGeloeschtAsync(db, bleibt);
    }

    [Fact]
    public async Task TrainingLoeschen_OhneFaehrten_FunktioniertWieBisher()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var training = Training(db, nutzer);
        await db.SaveChangesAsync();

        var ergebnis = await Dienst(db).DeleteAsync(nutzer, training.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.NotNull((await db.TrainingSessions.IgnoreQueryFilters().SingleAsync()).DeletedAt);
    }

    // ---- Einmalige Nachholung beim Start ----

    [Fact]
    public async Task Altbestand_FaehrtenGeloeschterOderFehlenderTrainingsWerdenWeichGeloescht()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var lebt = Training(db, nutzer);
        var tot = Training(db, nutzer);
        tot.DeletedAt = DateTimeOffset.UtcNow.AddDays(-20); // gelöscht, als das noch die Fährten stehen ließ
        var fehlt = Guid.NewGuid(); // Training gibt es gar nicht mehr
        var ok = VollstaendigeFaehrte(db, lebt.Id);
        var ausGeloeschtem = VollstaendigeFaehrte(db, tot.Id);
        var ausFehlendem = VollstaendigeFaehrte(db, fehlt);
        await db.SaveChangesAsync();

        var entfernt = await new GpsTrackOrphanCleanup(db).CleanupAsync();

        Assert.Equal(2, entfernt);
        await AssertNichtsGeloeschtAsync(db, ok);
        await AssertAllesGeloeschtAsync(db, ausGeloeschtem);
        await AssertAllesGeloeschtAsync(db, ausFehlendem);
        // Weich, nicht endgültig: die Zeilen sind noch da.
        Assert.Equal(3, await db.GpsTracks.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Altbestand_IstIdempotent_UndArbeitetInPortionenAb()
    {
        var db = InMemoryDbContext.Create();
        for (var i = 0; i < 120; i++) // mehr als eine Portion
            VollstaendigeFaehrte(db, Guid.NewGuid());
        await db.SaveChangesAsync();
        var cleanup = new GpsTrackOrphanCleanup(db);

        var erster = await cleanup.CleanupAsync();
        var zweiter = await cleanup.CleanupAsync();

        Assert.Equal(120, erster);
        Assert.Equal(0, zweiter);
        Assert.Empty(db.GpsTracks);
        Assert.Empty(db.GpsPoints);
    }
}
