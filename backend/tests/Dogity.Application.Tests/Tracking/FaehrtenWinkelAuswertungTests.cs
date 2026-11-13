using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static Dogity.Application.Tests.Tracking.FaehrtenTestHilfe;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Auswertung je Ablauf gegen die Winkel und Schenkel der Fährte (Fortschritt, Überschießen, Halt
/// am Winkel, Zeit am Winkel) und ihre Nachrechnung im Backfill.
/// </summary>
public class FaehrtenWinkelAuswertungTests
{
    // 100 m nach Norden, 60 m nach Osten (Winkel 1 rechts), 80 m nach Norden (Winkel 2 links).
    private static readonly (double X, double Y)[] Weg = [(0, 0), (0, 100), (60, 100), (60, 180)];

    private static WalkRunEvaluation Auswerten(Lauf lauf, IEnumerable<GpsPoint>? marker = null, GpsTrackKind art = GpsTrackKind.Faehrte) =>
        GpsTrackEvaluator.TryEvaluate(Legung(Weg).Concat(marker ?? []).ToList(), lauf.Punkte, art)!.Value;

    private static Lauf SauberAbgelaufen(double sekundenProMeter = 1.5) =>
        new Lauf(0, 0).Gehe(0, 100, sekundenProMeter).Gehe(60, 100, sekundenProMeter).Gehe(60, 180, sekundenProMeter);

    [Fact]
    public void SauberAbgelaufen_AlleWinkelSauber_SchenkelmitDauerUndTempo()
    {
        var ergebnis = Auswerten(SauberAbgelaufen());

        Assert.Equal(3, ergebnis.Legs.Count);
        // Letzter Schenkel: gemessen bis 8 m vor dem Ende (Spielraum am Fährtenende), 72 m bei 1,5 s/m.
        Assert.Equal([150, 90, 108], ergebnis.Legs.Select(l => l.DurationSeconds!.Value));
        // 100 m in 150 s = 40 m/min.
        Assert.All(ergebnis.Legs, l => Assert.Equal(40, l.SpeedMetersPerMinute!.Value, 1));
        Assert.All(ergebnis.Legs, l => Assert.True(l.AvgDeviationMeters < 0.5));

        Assert.Equal(2, ergebnis.Corners.Count);
        Assert.All(ergebnis.Corners, c => Assert.True(c.Reached));
        Assert.All(ergebnis.Corners, c => Assert.False(c.Overshot));
        Assert.All(ergebnis.Corners, c => Assert.True(c.OvershootMeters < 0.5));
        Assert.All(ergebnis.Corners, c => Assert.Equal(0, c.HoldSeconds));
        // 10 m davor bis 10 m dahinter: 20 m bei 1,5 s/m.
        Assert.All(ergebnis.Corners, c => Assert.Equal(30, c.TimeAtCornerSeconds));
    }

    [Fact]
    public void StehenbleibenKurzVorDemEnde_LetzterSchenkelHatTrotzdemEineDauer()
    {
        // Der Hundeführer bleibt 5 m vor dem letzten gelegten Punkt stehen (Hund am Endgegenstand).
        var lauf = new Lauf(0, 0).Gehe(0, 100).Gehe(60, 100).Gehe(60, 175);

        var ergebnis = Auswerten(lauf);

        Assert.NotNull(ergebnis.Legs[2].DurationSeconds);
        Assert.NotNull(ergebnis.Legs[2].SpeedMetersPerMinute);
    }

    [Fact]
    public void Ueberschiessen_AnWinkelZwei_WirdGemeldet()
    {
        // Winkel 1 sauber; an Winkel 2 läuft der Hund 12 m geradeaus nach Osten weiter und kehrt um.
        var lauf = new Lauf(0, 0).Gehe(0, 100).Gehe(60, 100).Gehe(72, 100).Gehe(60, 100).Gehe(60, 180);

        var ergebnis = Auswerten(lauf);

        Assert.False(ergebnis.Corners[0].Overshot);
        Assert.True(ergebnis.Corners[1].Reached);
        Assert.True(ergebnis.Corners[1].Overshot);
        Assert.Equal(12, ergebnis.Corners[1].OvershootMeters!.Value, 1);
    }

    [Fact]
    public void AbbruchVorWinkelZwei_WinkelNichtErreichtUndSchenkelOhneDauer()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 100).Gehe(30, 100);

        var ergebnis = Auswerten(lauf);

        Assert.True(ergebnis.Corners[0].Reached);
        var zwei = ergebnis.Corners[1];
        Assert.False(zwei.Reached);
        Assert.False(zwei.Overshot);
        Assert.Null(zwei.OvershootMeters);
        Assert.Null(zwei.TimeAtCornerSeconds);
        Assert.NotNull(ergebnis.Legs[0].DurationSeconds);
        Assert.Null(ergebnis.Legs[1].DurationSeconds);
        Assert.Null(ergebnis.Legs[1].SpeedMetersPerMinute);
        Assert.NotNull(ergebnis.Legs[1].AvgDeviationMeters);
        Assert.Null(ergebnis.Legs[2].DurationSeconds);
        Assert.Null(ergebnis.Legs[2].AvgDeviationMeters);
    }

    [Fact]
    public void RauschenUndZurueckgehen_SetztDenFortschrittNichtZurueck()
    {
        // Der Hund geht bis 90 m, ein Stück zurück auf 70 m und dann bis zum Ende: Schenkel 1 endet
        // bei der ersten Ankunft auf 100 m, nicht erst nach dem Zurückgehen.
        var lauf = new Lauf(0, 0).Gehe(0, 90).Gehe(0, 70).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var ergebnis = Auswerten(lauf);

        // 90 m + 20 m zurück + 30 m hin = 140 m bei 1,5 s/m - die Rückwärtsstrecke zählt als Zeit.
        Assert.Equal(210, ergebnis.Legs[0].DurationSeconds);
        Assert.All(ergebnis.Corners, c => Assert.True(c.Reached));
    }

    [Fact]
    public void HaltAmWinkel_WirdAmWinkelStattUnerklaert()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 97).Halt(20).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var ergebnis = Auswerten(lauf);

        var halt = Assert.Single(ergebnis.Stops);
        Assert.Equal(WalkStopKind.AtCorner, halt.Kind);
        // Das Fenster des Halts nimmt die letzten Schritte davor mit.
        Assert.InRange(ergebnis.Corners[0].HoldSeconds, 20, 30);
        Assert.Equal(0, ergebnis.Corners[1].HoldSeconds);
    }

    [Fact]
    public void HaltFernabVonWinkelUndMarker_BleibtUnerklaert()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 40).Halt(20).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var halt = Assert.Single(Auswerten(lauf).Stops);

        Assert.Equal(WalkStopKind.Unexplained, halt.Kind);
    }

    [Fact]
    public void HaltAmGegenstandAmWinkel_BleibtVerweisen_ZaehltAberAmWinkelMit()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 97).Halt(20).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var ergebnis = Auswerten(lauf, [Marker(0, 99, GpsMarkerType.Article)]);

        var halt = Assert.Single(ergebnis.Stops);
        Assert.Equal(WalkStopKind.Indication, halt.Kind);
        // Das Fenster des Halts nimmt die letzten Schritte davor mit.
        Assert.InRange(ergebnis.Corners[0].HoldSeconds, 20, 30);
    }

    [Fact]
    public void HaltAmLeckerlipotAmWinkel_BleibtErklaert()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 97).Halt(20).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var halt = Assert.Single(Auswerten(lauf, [Marker(0, 99, GpsMarkerType.TreatPot)]).Stops);

        Assert.Equal(WalkStopKind.Explained, halt.Kind);
    }

    [Fact]
    public void Mantrailing_BekommtWederWinkelNochSchenkel_UndKeinenHaltAmWinkel()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 97).Halt(20).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var ergebnis = Auswerten(lauf, art: GpsTrackKind.Mantrailing);

        Assert.Empty(ergebnis.Legs);
        Assert.Empty(ergebnis.Corners);
        Assert.Equal(WalkStopKind.Unexplained, Assert.Single(ergebnis.Stops).Kind);
    }

    [Fact]
    public void DieGesamtwerte_BleibenVonWinkelnUnberuehrt()
    {
        var ohneHalt = Auswerten(SauberAbgelaufen());

        Assert.Equal(100, ohneHalt.OnTrackPercent, 1);
        Assert.True(ohneHalt.AvgDeviationMeters < 0.5);
    }

    // ---- Nachrechnung beim Start (Backfill) und Neuauswertung ----

    private sealed record Welt(Dogity.Infrastructure.Persistence.ApplicationDbContext Db, Guid Track, Guid Run);

    private static async Task<Welt> AufbauenAsync(GpsTrackKind art, int version, bool mitAltemHalt)
    {
        var db = InMemoryDbContext.Create();
        var einheit = new TrainingSession { UserId = Guid.NewGuid(), DogId = Guid.NewGuid(), Date = new DateOnly(2026, 9, 1), DurationMinutes = 30 };
        var spur = new GpsTrack { TrainingSessionId = einheit.Id, Kind = art };
        db.TrainingSessions.Add(einheit);
        db.GpsTracks.Add(spur);
        foreach (var p in Legung(Weg)) { p.TrackId = spur.Id; db.GpsPoints.Add(p); }
        var lauf = new GpsWalkRun { TrackId = spur.Id, EvaluatedAt = T0, EvaluationVersion = version, AvgDeviationMeters = 99 };
        foreach (var p in SauberAbgelaufen().Punkte) { p.WalkRunId = lauf.Id; lauf.Points.Add(p); }
        db.GpsWalkRuns.Add(lauf);
        if (mitAltemHalt)
            db.GpsWalkStops.Add(new GpsWalkStop { WalkRunId = lauf.Id, DurationSeconds = 11, Kind = WalkStopKind.Unexplained });
        await db.SaveChangesAsync();
        return new Welt(db, spur.Id, lauf.Id);
    }

    [Fact]
    public async Task Backfill_RechnetAeltereVersionNach_UndIstIdempotent()
    {
        var w = await AufbauenAsync(GpsTrackKind.Faehrte, version: 0, mitAltemHalt: true);
        var backfill = new GpsTrackEvaluationBackfill(w.Db, NullLogger<GpsTrackEvaluationBackfill>.Instance);

        Assert.Equal(1, await backfill.BackfillAsync());

        var lauf = await w.Db.GpsWalkRuns.SingleAsync(r => r.Id == w.Run);
        Assert.Equal(GpsTrackEvaluator.AktuelleVersion, lauf.EvaluationVersion);
        Assert.True(lauf.AvgDeviationMeters < 0.5, "Die Gesamtwerte sind neu berechnet.");
        Assert.Equal(3, await w.Db.GpsWalkRunLegs.CountAsync(l => l.WalkRunId == w.Run));
        Assert.Equal(2, await w.Db.GpsWalkRunCorners.CountAsync(c => c.WalkRunId == w.Run));
        // Der alte Halt wurde ersetzt, nicht ergänzt: Der saubere Lauf hat keinen.
        Assert.Equal(0, await w.Db.GpsWalkStops.CountAsync(s => s.WalkRunId == w.Run));

        // Zweiter Start: nichts mehr zu tun, nichts ändert sich.
        Assert.Equal(0, await backfill.BackfillAsync());
        Assert.Equal(3, await w.Db.GpsWalkRunLegs.CountAsync(l => l.WalkRunId == w.Run));
    }

    [Fact]
    public async Task Backfill_LaesstAbgelaufeneAktuelleAuswertungenUnberuehrt()
    {
        var w = await AufbauenAsync(GpsTrackKind.Faehrte, version: GpsTrackEvaluator.AktuelleVersion, mitAltemHalt: false);

        Assert.Equal(0, await new GpsTrackEvaluationBackfill(w.Db, NullLogger<GpsTrackEvaluationBackfill>.Instance).BackfillAsync());

        Assert.Equal(99, (await w.Db.GpsWalkRuns.SingleAsync(r => r.Id == w.Run)).AvgDeviationMeters);
    }

    [Fact]
    public async Task Backfill_BeiMantrailingHebtDieVersionAnOhneWinkel()
    {
        var w = await AufbauenAsync(GpsTrackKind.Mantrailing, version: 0, mitAltemHalt: false);

        await new GpsTrackEvaluationBackfill(w.Db, NullLogger<GpsTrackEvaluationBackfill>.Instance).BackfillAsync();

        Assert.Equal(GpsTrackEvaluator.AktuelleVersion, (await w.Db.GpsWalkRuns.SingleAsync(r => r.Id == w.Run)).EvaluationVersion);
        Assert.Empty(w.Db.GpsWalkRunLegs);
        Assert.Empty(w.Db.GpsWalkRunCorners);
    }

    [Fact]
    public async Task EineNeuAuswertung_ErsetztSchenkelUndWinkelStattSieZuVerdoppeln()
    {
        var w = await AufbauenAsync(GpsTrackKind.Faehrte, version: 0, mitAltemHalt: false);
        var besitzer = BesitzerDerEinheit(w);
        var dienst = new GpsTrackService(w.Db, new FakeWeatherEnrichmentService());

        await dienst.EvaluateWalkRunAsync(besitzer.Nutzer, w.Track, w.Run);
        var zweite = await dienst.EvaluateWalkRunAsync(besitzer.Nutzer, w.Track, w.Run);

        Assert.True(zweite.Succeeded);
        Assert.Equal(3, zweite.Value!.Legs.Count);
        Assert.Equal(2, zweite.Value.Corners.Count);
        Assert.Equal([1, 2], zweite.Value.Corners.Select(c => c.Number));
        Assert.Equal(3, await w.Db.GpsWalkRunLegs.CountAsync(l => l.WalkRunId == w.Run));
        Assert.Equal(2, await w.Db.GpsWalkRunCorners.CountAsync(c => c.WalkRunId == w.Run));
    }

    private static (Guid Nutzer, Guid Hund) BesitzerDerEinheit(Welt w)
    {
        var einheit = w.Db.TrainingSessions.Single();
        w.Db.Dogs.Add(new Dogity.Domain.Dogs.Dog { Id = einheit.DogId, Name = "Bello" });
        w.Db.DogOwners.Add(new Dogity.Domain.Dogs.DogOwner { DogId = einheit.DogId, UserId = einheit.UserId });
        w.Db.SaveChanges();
        return (einheit.UserId, einheit.DogId);
    }
}
