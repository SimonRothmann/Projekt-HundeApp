using Dogity.Api.Hosting;
using Dogity.Application.Betrieb;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Notifications;
using Dogity.Domain.Tracking;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Betrieb;

/// <summary>
/// Aufbewahrungsfristen (Entscheidung des Betreibers, 16.11.2026): gelöschte GPS-Daten 30 Tage nach dem
/// Löschen, Benachrichtigungen 90 Tage nach dem Anlegen. Geprüft wird mit IgnoreQueryFilters - ohne wäre
/// "ausgeblendet" nicht von "endgültig entfernt" zu trennen.
/// </summary>
public class AufbewahrungTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 20, 2, 30, 0, TimeSpan.Zero);

    // ---- Fristberechnung ----

    [Fact]
    public void Fristen_SindDreissigUndNeunzigTage()
    {
        Assert.Equal(Jetzt.AddDays(-30), Aufbewahrungsfristen.GrenzeGeloeschteGpsDaten(Jetzt));
        Assert.Equal(Jetzt.AddDays(-90), Aufbewahrungsfristen.GrenzeBenachrichtigungen(Jetzt));
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 2, 30, 0, TimeSpan.Zero), Aufbewahrungsfristen.GrenzeGeloeschteGpsDaten(Jetzt));
    }

    [Fact]
    public void Lauf_LiegtNachtsUmHalbVierVereinszeit()
    {
        // 20.11. 19:00 Uhr (UTC+1) -> 21.11. 03:30 Uhr = 02:30 UTC
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 2, 30, 0, TimeSpan.Zero), AufbewahrungBackgroundService.NaechsterLauf(new(2026, 11, 20, 18, 0, 0, TimeSpan.Zero)));
        // 03:29 Uhr: noch am selben Tag; genau 03:30 und danach: am Folgetag
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 2, 30, 0, TimeSpan.Zero), AufbewahrungBackgroundService.NaechsterLauf(new(2026, 11, 20, 2, 29, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 2, 30, 0, TimeSpan.Zero), AufbewahrungBackgroundService.NaechsterLauf(new(2026, 11, 20, 2, 30, 0, TimeSpan.Zero)));
        // Sommerzeit: 03:30 Uhr = 01:30 UTC
        Assert.Equal(new DateTimeOffset(2026, 7, 11, 1, 30, 0, TimeSpan.Zero), AufbewahrungBackgroundService.NaechsterLauf(new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new TimeOnly(3, 30), TimeOnly.FromDateTime(Vereinszeit.Lokal(AufbewahrungBackgroundService.NaechsterLauf(new(2026, 3, 28, 21, 0, 0, TimeSpan.Zero)))));
    }

    // ---- Auswahl ----

    private sealed record Faehrte(Guid TrackId, Guid RunId);

    /// <summary>Fährte mit Punkt, Ablauf, Ablaufpunkt, Stockung, Schenkel, Winkel und Gegenstandsangabe - alles, was daran hängen kann.</summary>
    private static Faehrte VollstaendigeFaehrte(ApplicationDbContext db, DateTimeOffset? geloescht, bool kinderMitDatum = true)
    {
        var track = new GpsTrack { TrainingSessionId = Guid.NewGuid(), LengthMeters = 300, DeletedAt = geloescht };
        db.GpsTracks.Add(track);
        var kind = kinderMitDatum ? geloescht : null;
        var gegenstand = new GpsPoint { TrackId = track.Id, Latitude = 48.9, Longitude = 8.5, Timestamp = Jetzt, DeletedAt = kind };
        db.GpsPoints.Add(gegenstand);
        var run = new GpsWalkRun { TrackId = track.Id, DeletedAt = kind };
        db.GpsWalkRuns.Add(run);
        db.GpsWalkPoints.Add(new GpsWalkPoint { WalkRunId = run.Id, Latitude = 48.9, Longitude = 8.5, Timestamp = Jetzt, DeletedAt = kind });
        db.GpsWalkStops.Add(new GpsWalkStop { WalkRunId = run.Id, DeletedAt = kind });
        db.GpsWalkRunLegs.Add(new GpsWalkRunLeg { WalkRunId = run.Id, Number = 1, DeletedAt = kind });
        db.GpsWalkRunCorners.Add(new GpsWalkRunCorner { WalkRunId = run.Id, Number = 1, DeletedAt = kind });
        db.GpsWalkRunArticles.Add(new GpsWalkRunArticle { WalkRunId = run.Id, PointId = gegenstand.Id, Result = ArticleResult.Indicated, DeletedAt = kind });
        return new Faehrte(track.Id, run.Id);
    }

    private static async Task<int> ZeilenAsync(ApplicationDbContext db, Faehrte f) =>
        await db.GpsTracks.IgnoreQueryFilters().CountAsync(t => t.Id == f.TrackId)
        + await db.GpsPoints.IgnoreQueryFilters().CountAsync(p => p.TrackId == f.TrackId)
        + await db.GpsWalkRuns.IgnoreQueryFilters().CountAsync(r => r.Id == f.RunId)
        + await db.GpsWalkPoints.IgnoreQueryFilters().CountAsync(p => p.WalkRunId == f.RunId)
        + await db.GpsWalkStops.IgnoreQueryFilters().CountAsync(s => s.WalkRunId == f.RunId)
        + await db.GpsWalkRunLegs.IgnoreQueryFilters().CountAsync(l => l.WalkRunId == f.RunId)
        + await db.GpsWalkRunCorners.IgnoreQueryFilters().CountAsync(c => c.WalkRunId == f.RunId)
        + await db.GpsWalkRunArticles.IgnoreQueryFilters().CountAsync(a => a.WalkRunId == f.RunId);

    [Fact]
    public async Task FaelligeFaehrte_GehtMitAllemWeg_JungeUndLebendeBleiben()
    {
        var db = InMemoryDbContext.Create();
        var faellig = VollstaendigeFaehrte(db, Jetzt.AddDays(-31));
        var knapp = VollstaendigeFaehrte(db, Jetzt.AddDays(-29));
        var lebendig = VollstaendigeFaehrte(db, null);
        await db.SaveChangesAsync();

        var ergebnis = await new AufbewahrungCleanup(db).CleanupAsync(Jetzt);

        Assert.Equal(1, ergebnis.Faehrten);
        Assert.Equal(0, await ZeilenAsync(db, faellig));
        Assert.Equal(8, await ZeilenAsync(db, knapp));
        Assert.Equal(8, await ZeilenAsync(db, lebendig));
    }

    [Fact]
    public async Task GenauAnDerGrenze_IstNochNichtFaellig_EineSekundeDarueberSchon()
    {
        var db = InMemoryDbContext.Create();
        var grenze = Aufbewahrungsfristen.GrenzeGeloeschteGpsDaten(Jetzt);
        var aufDerGrenze = VollstaendigeFaehrte(db, grenze);
        var darueber = VollstaendigeFaehrte(db, grenze.AddSeconds(-1));
        await db.SaveChangesAsync();

        await new AufbewahrungCleanup(db).CleanupAsync(Jetzt);

        Assert.Equal(8, await ZeilenAsync(db, aufDerGrenze));
        Assert.Equal(0, await ZeilenAsync(db, darueber));
    }

    [Fact]
    public async Task EinzelGeloeschteAltFaehrte_OhneLoeschdatumAnDenPunkten_GehtTrotzdemMitAllemWeg()
    {
        // Altbestand: GpsTrackService.DeleteAsync setzte früher nur das DeletedAt der Fährte selbst.
        var db = InMemoryDbContext.Create();
        var alt = VollstaendigeFaehrte(db, Jetzt.AddDays(-60), kinderMitDatum: false);
        await db.SaveChangesAsync();

        await new AufbewahrungCleanup(db).CleanupAsync(Jetzt);

        Assert.Equal(0, await ZeilenAsync(db, alt));
    }

    [Fact]
    public async Task Benachrichtigungen_NachNeunzigTagenGelesenOderNicht_AusgeblendetOderNicht()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        Notification Neu(int tage, bool gelesen = false, bool geloescht = false) => new()
        {
            UserId = nutzer, Message = "x", IsRead = gelesen, CreatedAt = Jetzt.AddDays(-tage), DeletedAt = geloescht ? Jetzt.AddDays(-1) : null,
        };
        db.Notifications.AddRange(Neu(91), Neu(120, gelesen: true), Neu(95, geloescht: true), Neu(89), Neu(1, gelesen: true));
        await db.SaveChangesAsync();

        var ergebnis = await new AufbewahrungCleanup(db).CleanupAsync(Jetzt);

        Assert.Equal(3, ergebnis.Benachrichtigungen);
        Assert.Equal(2, await db.Notifications.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task MehrAlsEinePortion_WirdInMehrerenDurchgaengenGanzAbgeraeumt()
    {
        var db = InMemoryDbContext.Create();
        var anzahl = AufbewahrungCleanup.Portion * 2 + 7;
        for (var i = 0; i < anzahl; i++)
        {
            db.Notifications.Add(new Notification { UserId = Guid.NewGuid(), Message = "alt", CreatedAt = Jetzt.AddDays(-100) });
            db.GpsTracks.Add(new GpsTrack { TrainingSessionId = Guid.NewGuid(), DeletedAt = Jetzt.AddDays(-40) });
        }
        await db.SaveChangesAsync();

        var ergebnis = await new AufbewahrungCleanup(db).CleanupAsync(Jetzt);

        Assert.Equal(new AufbewahrungErgebnis(anzahl, anzahl), ergebnis);
        Assert.Equal(0, await db.Notifications.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.GpsTracks.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task ZweiterLauf_FindetNichtsMehr()
    {
        var db = InMemoryDbContext.Create();
        VollstaendigeFaehrte(db, Jetzt.AddDays(-31));
        db.Notifications.Add(new Notification { UserId = Guid.NewGuid(), Message = "alt", CreatedAt = Jetzt.AddDays(-91) });
        await db.SaveChangesAsync();
        var cleanup = new AufbewahrungCleanup(db);
        await cleanup.CleanupAsync(Jetzt);

        Assert.Equal(new AufbewahrungErgebnis(0, 0), await cleanup.CleanupAsync(Jetzt));
    }
}
