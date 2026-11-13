using Dogity.Application.Stats;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Stats;

/// <summary>
/// Entwicklung des Hundes bei den Winkeln: Anteil "sauber" je Gruppe (links / rechts / spitz) über die
/// erreichten Winkel, erst ab drei Winkeln je Gruppe.
/// </summary>
public class FaehrtenWinkelStatistikTests
{
    private sealed record Welt(StatsService Dienst, Guid Nutzer, Guid Hund, Infrastructure.Persistence.ApplicationDbContext Db);

    private static Welt Aufbauen()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var hund = new Dog { Name = "Rex" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer });
        return new Welt(new StatsService(db, TimeProvider.System), nutzer, hund.Id, db);
    }

    /// <summary>Ein ausgewerteter Fährten-Ablauf mit den angegebenen Winkeln (Richtung, spitz, erreicht, überschossen).</summary>
    private static void Ablauf(Welt w, int tag, params (CornerDirection Richtung, bool Spitz, bool Erreicht, bool Ueberschossen)[] winkel)
    {
        var einheit = new TrainingSession { UserId = w.Nutzer, DogId = w.Hund, Date = new DateOnly(2026, 10, tag), DurationMinutes = 30 };
        var spur = new GpsTrack { TrainingSessionId = einheit.Id };
        var lauf = new GpsWalkRun { TrackId = spur.Id, AvgDeviationMeters = 2, OnTrackPercent = 90, EvaluatedAt = DateTimeOffset.UtcNow };
        w.Db.TrainingSessions.Add(einheit);
        w.Db.GpsTracks.Add(spur);
        w.Db.GpsWalkRuns.Add(lauf);
        var nr = 1;
        foreach (var (richtung, spitz, erreicht, ueberschossen) in winkel)
        {
            w.Db.GpsWalkRunCorners.Add(new GpsWalkRunCorner
            {
                WalkRunId = lauf.Id, Number = nr++, Direction = richtung, Sharp = spitz, Reached = erreicht, Overshot = ueberschossen
            });
        }
    }

    [Fact]
    public async Task GruppiertNachLinksRechtsUndSpitz_MitAnteilSauber()
    {
        var w = Aufbauen();
        // Links: 4 erreicht (der fünfte nicht), 1 überschossen (75 %). Rechts: 3 erreicht, 1 überschossen (67 %).
        // Spitz: ein spitzer Winkel zählt in seiner Richtung UND bei spitz - 3 erreicht, 1 überschossen (67 %).
        Ablauf(w, 1, (CornerDirection.Left, false, true, false), (CornerDirection.Left, false, true, true), (CornerDirection.Right, true, true, false));
        Ablauf(w, 2, (CornerDirection.Left, false, true, false), (CornerDirection.Left, true, true, false), (CornerDirection.Right, false, true, false));
        Ablauf(w, 3, (CornerDirection.Right, true, true, true), (CornerDirection.Left, false, false, false));
        await w.Db.SaveChangesAsync();

        var gruppen = (await w.Dienst.GetDogTrackStatsAsync(w.Nutzer, w.Hund)).Value!.CornerGroups;

        var links = Assert.Single(gruppen, g => g.Group == CornerGroup.Left);
        Assert.Equal(4, links.Reached);
        Assert.Equal(75, links.CleanPercent);
        var rechts = Assert.Single(gruppen, g => g.Group == CornerGroup.Right);
        Assert.Equal(3, rechts.Reached);
        Assert.Equal(67, rechts.CleanPercent);
        var spitz = Assert.Single(gruppen, g => g.Group == CornerGroup.Sharp);
        Assert.Equal(3, spitz.Reached);
        Assert.Equal(67, spitz.CleanPercent);
    }

    [Fact]
    public async Task GruppenMitWenigerAlsDreiErreichtenWinkelnFehlen_NichtErreichteZaehlenNicht()
    {
        var w = Aufbauen();
        // Rechts: nur 2 erreicht (der dritte wurde nie erreicht) - keine Aussage.
        Ablauf(w, 1, (CornerDirection.Left, false, true, false), (CornerDirection.Left, false, true, false), (CornerDirection.Left, false, true, false));
        Ablauf(w, 2, (CornerDirection.Right, false, true, false), (CornerDirection.Right, false, true, true), (CornerDirection.Right, false, false, false));
        await w.Db.SaveChangesAsync();

        var gruppen = (await w.Dienst.GetDogTrackStatsAsync(w.Nutzer, w.Hund)).Value!.CornerGroups;

        Assert.Equal(CornerGroup.Left, Assert.Single(gruppen).Group);
    }

    [Fact]
    public async Task OhneWinkel_KeineGruppen()
    {
        var w = Aufbauen();
        Ablauf(w, 1);
        await w.Db.SaveChangesAsync();

        var stats = (await w.Dienst.GetDogTrackStatsAsync(w.Nutzer, w.Hund)).Value!;

        Assert.Single(stats.Runs);
        Assert.Empty(stats.CornerGroups);
    }

    [Fact]
    public async Task ZaehltNurDieAngezeigtenZwoelfJuengstenAblaeufe()
    {
        var w = Aufbauen();
        // Der älteste Ablauf (Tag 1) fällt aus den 12 heraus; seine Winkel zählen nicht.
        Ablauf(w, 1, (CornerDirection.Left, false, true, true), (CornerDirection.Left, false, true, true), (CornerDirection.Left, false, true, true));
        for (var tag = 2; tag <= 13; tag++)
            Ablauf(w, tag, (CornerDirection.Left, false, true, false));
        await w.Db.SaveChangesAsync();

        var links = Assert.Single((await w.Dienst.GetDogTrackStatsAsync(w.Nutzer, w.Hund)).Value!.CornerGroups);

        Assert.Equal(12, links.Reached);
        Assert.Equal(100, links.CleanPercent);
    }

    [Fact]
    public async Task EinFremderHundBekommtNichts()
    {
        var w = Aufbauen();
        Ablauf(w, 1, (CornerDirection.Left, false, true, false));
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Dienst.GetDogTrackStatsAsync(Guid.NewGuid(), w.Hund)).IsNotFound);
    }
}
