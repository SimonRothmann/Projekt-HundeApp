using Dogity.Application.Stats;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Stats;

/// <summary>
/// Paket F3 in der Entwicklung des Hundes: Fundquote je Material (verwiesen + aufgenommen an allen bewerteten
/// Gegenständen des Materials, erst ab drei) und das Herausnehmen bewusst schwerer Übungsfährten.
/// </summary>
public class FaehrtenMaterialStatistikTests
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

    /// <summary>
    /// Ein ausgewerteter Fährten-Ablauf. Je Eintrag ein Gegenstand: Material (null = keins eingetragen) und
    /// Ergebnis (null = keine Angabe im Ablauf).
    /// </summary>
    private static GpsWalkRun Ablauf(Welt w, int tag, bool ausDerEntwicklung = false, GpsTrackKind art = GpsTrackKind.Faehrte,
        params (string? Material, ArticleResult? Ergebnis)[] gegenstaende)
    {
        var einheit = new TrainingSession { UserId = w.Nutzer, DogId = w.Hund, Date = new DateOnly(2026, 10, tag), DurationMinutes = 30 };
        var spur = new GpsTrack { TrainingSessionId = einheit.Id, Kind = art, ExcludeFromStats = ausDerEntwicklung };
        var lauf = new GpsWalkRun { TrackId = spur.Id, AvgDeviationMeters = 2, OnTrackPercent = 90, EvaluatedAt = DateTimeOffset.UtcNow };
        w.Db.TrainingSessions.Add(einheit);
        w.Db.GpsTracks.Add(spur);
        w.Db.GpsWalkRuns.Add(lauf);
        foreach (var (material, ergebnis) in gegenstaende)
        {
            var punkt = new GpsPoint
            {
                TrackId = spur.Id, PointType = GpsPointType.Manual, MarkerType = GpsMarkerType.Article, Material = material,
            };
            w.Db.GpsPoints.Add(punkt);
            if (ergebnis is { } e)
                w.Db.GpsWalkRunArticles.Add(new GpsWalkRunArticle { WalkRunId = lauf.Id, PointId = punkt.Id, Result = e });
        }

        return lauf;
    }

    private static async Task<DogTrackStatsDto> AuswertenAsync(Welt w)
    {
        await w.Db.SaveChangesAsync();
        return (await w.Dienst.GetDogTrackStatsAsync(w.Nutzer, w.Hund)).Value!;
    }

    [Fact]
    public async Task FundquoteJeMaterial_VerwiesenUndAufgenommenZaehlenAlsGefunden_UeberlaufenNicht()
    {
        var w = Aufbauen();
        // Leder: 4 bewertet, 3 gefunden (75 %). Holz: 3 bewertet, 1 gefunden (33 %).
        Ablauf(w, 1, gegenstaende:
        [
            ("Leder", ArticleResult.Indicated), ("Leder", ArticleResult.PickedUp), ("Holz", ArticleResult.Missed),
        ]);
        Ablauf(w, 2, gegenstaende:
        [
            ("Leder", ArticleResult.Indicated), ("Leder", ArticleResult.Missed), ("Holz", ArticleResult.Missed), ("Holz", ArticleResult.PickedUp),
        ]);

        var gruppen = (await AuswertenAsync(w)).MaterialGroups;

        var leder = Assert.Single(gruppen, g => g.Material == "Leder");
        Assert.Equal(4, leder.Rated);
        Assert.Equal(75, leder.FoundPercent);
        var holz = Assert.Single(gruppen, g => g.Material == "Holz");
        Assert.Equal(3, holz.Rated);
        Assert.Equal(33, holz.FoundPercent);
        // In der Reihenfolge der Auswahl, nicht nach Zufall.
        Assert.Equal(["Leder", "Holz"], gruppen.Select(g => g.Material));
    }

    [Fact]
    public async Task OhneAngabeOhneMaterialUndUnterDrei_ZaehltNichtsUndZeigtNichts()
    {
        var w = Aufbauen();
        // Stoff: 2 bewertet (zu wenig). Leder: 2 bewertet + ein Gegenstand ohne Angabe im Ablauf (zählt nicht mit).
        // Ein bewerteter Gegenstand ohne Material zählt nirgends mit.
        Ablauf(w, 1, gegenstaende:
        [
            ("Stoff", ArticleResult.Indicated), ("Stoff", ArticleResult.Indicated), ("Leder", ArticleResult.Indicated),
            ("Leder", ArticleResult.Indicated), ("Leder", null), (null, ArticleResult.Indicated), (null, ArticleResult.Indicated),
            (null, ArticleResult.Indicated),
        ]);

        Assert.Empty((await AuswertenAsync(w)).MaterialGroups);
    }

    [Fact]
    public async Task OhneJedeDaten_KeineMaterialgruppen()
    {
        var w = Aufbauen();
        Ablauf(w, 1);

        var stats = await AuswertenAsync(w);

        Assert.Single(stats.Runs);
        Assert.Empty(stats.MaterialGroups);
    }

    [Fact]
    public async Task ZaehltNurDieAngezeigtenZwoelfJuengstenAblaeufe()
    {
        var w = Aufbauen();
        // Der älteste Ablauf (Tag 1) fällt aus den 12 heraus: seine drei Leder-Funde zählen nicht.
        Ablauf(w, 1, gegenstaende: [("Leder", ArticleResult.Indicated), ("Leder", ArticleResult.Indicated), ("Leder", ArticleResult.Indicated)]);
        for (var tag = 2; tag <= 13; tag++)
            Ablauf(w, tag, gegenstaende: [("Leder", ArticleResult.Missed)]);

        var leder = Assert.Single((await AuswertenAsync(w)).MaterialGroups);

        Assert.Equal(12, leder.Rated);
        Assert.Equal(0, leder.FoundPercent);
    }

    [Fact]
    public async Task EineHerausgenommeneFaehrte_ZaehltWederImVerlaufNochBeiWinkelnUndMaterial()
    {
        var w = Aufbauen();
        var normal = Ablauf(w, 1, gegenstaende: [("Leder", ArticleResult.Indicated), ("Leder", ArticleResult.Indicated)]);
        var schwer = Ablauf(w, 2, ausDerEntwicklung: true, gegenstaende: [("Leder", ArticleResult.Missed), ("Leder", ArticleResult.Missed)]);
        foreach (var lauf in new[] { normal, schwer })
        {
            for (var nr = 1; nr <= 3; nr++)
                w.Db.GpsWalkRunCorners.Add(new GpsWalkRunCorner { WalkRunId = lauf.Id, Number = nr, Direction = CornerDirection.Left, Reached = true, Overshot = lauf == schwer });
        }

        var stats = await AuswertenAsync(w);

        // Verlauf: nur der normale Ablauf. Winkel: 3 saubere, die überschossenen der schweren Fährte fehlen.
        Assert.Single(stats.Runs);
        var links = Assert.Single(stats.CornerGroups);
        Assert.Equal(3, links.Reached);
        Assert.Equal(100, links.CleanPercent);
        // Material: Leder hat ohne die schwere Fährte nur 2 bewertete Gegenstände - keine Gruppe.
        Assert.Empty(stats.MaterialGroups);
    }

    [Fact]
    public async Task WiederHineingenommen_ZaehltDieFaehrteWieder()
    {
        var w = Aufbauen();
        Ablauf(w, 1, ausDerEntwicklung: true);
        await AuswertenAsync(w);
        Assert.Empty((await w.Dienst.GetDogTrackStatsAsync(w.Nutzer, w.Hund)).Value!.Runs);

        w.Db.GpsTracks.Single().ExcludeFromStats = false;
        await w.Db.SaveChangesAsync();

        Assert.Single((await w.Dienst.GetDogTrackStatsAsync(w.Nutzer, w.Hund)).Value!.Runs);
    }
}
