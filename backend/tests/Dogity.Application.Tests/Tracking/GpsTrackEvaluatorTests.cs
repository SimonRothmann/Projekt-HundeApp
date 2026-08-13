using Dogity.Application.Tracking;
using Dogity.Domain.Tracking;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Testet die Fährten-Auswertung (siehe GpsTrackEvaluator): Abweichung zur
/// gelegten Linie, Versatz-Invarianz (Hundeführer läuft hinterher),
/// Gegenstands-Erkennung und die Klassifizierung von Stockungen.
/// </summary>
public class GpsTrackEvaluatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    // Bezugspunkt irgendwo in Deutschland; 1° Breite ~ 111320 m.
    private const double BaseLat = 50.0;
    private const double BaseLon = 8.0;
    private const double MetersPerDegreeLat = 111320.0;

    private static double LatAt(double meters) => BaseLat + meters / MetersPerDegreeLat;
    private static double LonOffset(double meters) =>
        BaseLon + meters / (MetersPerDegreeLat * Math.Cos(BaseLat * Math.PI / 180));

    /// <summary>Gelegte Fährte: gerade Linie nach Norden, ein Punkt je Meter.</summary>
    private static List<GpsPoint> StraightTrack(int lengthMeters = 100)
    {
        var points = new List<GpsPoint>();
        for (var m = 0; m <= lengthMeters; m++)
        {
            points.Add(new GpsPoint
            {
                Latitude = LatAt(m),
                Longitude = BaseLon,
                Timestamp = T0.AddSeconds(m),
                PointType = GpsPointType.Automatic
            });
        }

        return points;
    }

    private static GpsPoint Marker(double alongMeters, GpsMarkerType type, string? label = null) => new()
    {
        Latitude = LatAt(alongMeters),
        Longitude = BaseLon,
        Timestamp = T0.AddSeconds(alongMeters),
        PointType = GpsPointType.Manual,
        MarkerType = type,
        Label = label
    };

    private static List<GpsWalkPoint> Walk(IEnumerable<(double Along, double Offset, int Second)> spec) =>
        spec.Select(s => new GpsWalkPoint
        {
            Latitude = LatAt(s.Along),
            Longitude = LonOffset(s.Offset),
            Timestamp = T0.AddSeconds(s.Second)
        }).ToList();

    [Fact]
    public void ExactlyOnTrack_HasNearZeroDeviation()
    {
        var track = StraightTrack();
        var walk = Walk(Enumerable.Range(0, 101).Select(m => ((double)m, 0.0, m)));

        var result = GpsTrackEvaluator.Evaluate(track, walk);

        Assert.True(result.AvgDeviationMeters < 0.5, $"Ø war {result.AvgDeviationMeters}");
        Assert.Equal(100, result.OnTrackPercent, 1);
    }

    /// <summary>
    /// Kernannahme des Verfahrens: Der Hundeführer läuft 10 m HINTER dem Hund,
    /// also zeitversetzt auf derselben Spur. Da senkrecht zur Linie gemessen
    /// wird, darf das keine Abweichung erzeugen.
    /// </summary>
    [Fact]
    public void HandlerWalkingBehind_IsNotCountedAsDeviation()
    {
        var track = StraightTrack();
        // Gleiche Linie, aber 10 s später gestartet (= ~10 m Rückstand).
        var walk = Walk(Enumerable.Range(0, 91).Select(m => ((double)m, 0.0, m + 10)));

        var result = GpsTrackEvaluator.Evaluate(track, walk);

        Assert.True(result.AvgDeviationMeters < 0.5, $"Ø war {result.AvgDeviationMeters}");
        Assert.Equal(100, result.OnTrackPercent, 1);
    }

    [Fact]
    public void ParallelOffsetWalk_IsMeasuredAsDeviation()
    {
        var track = StraightTrack();
        // Durchgehend 10 m seitlich versetzt.
        var walk = Walk(Enumerable.Range(0, 101).Select(m => ((double)m, 10.0, m)));

        var result = GpsTrackEvaluator.Evaluate(track, walk);

        Assert.InRange(result.AvgDeviationMeters, 9.0, 11.0);
        Assert.InRange(result.MaxDeviationMeters, 9.0, 11.0);
        Assert.Equal(0, result.OnTrackPercent, 1);
    }

    [Fact]
    public void Articles_CountedAsFoundOnlyWhenApproached()
    {
        var track = StraightTrack();
        track.Add(Marker(30, GpsMarkerType.Article, "Holz 1"));   // wird passiert
        track.Add(Marker(80, GpsMarkerType.Article, "Holz 2"));   // liegt weit ab vom Ablauf

        // Ablauf endet bei 50 m -> nur der erste Gegenstand wird erreicht.
        var walk = Walk(Enumerable.Range(0, 51).Select(m => ((double)m, 0.0, m)));

        var result = GpsTrackEvaluator.Evaluate(track, walk);

        Assert.Equal(2, result.ArticlesTotal);
        Assert.Equal(1, result.ArticlesFound);
    }

    [Fact]
    public void TreatPotMarker_IsNotCountedAsArticle()
    {
        var track = StraightTrack();
        track.Add(Marker(20, GpsMarkerType.Article));
        track.Add(Marker(40, GpsMarkerType.TreatPot));

        var walk = Walk(Enumerable.Range(0, 101).Select(m => ((double)m, 0.0, m)));

        var result = GpsTrackEvaluator.Evaluate(track, walk);

        Assert.Equal(1, result.ArticlesTotal);
    }

    [Fact]
    public void StopAtArticle_IsClassifiedAsIndication()
    {
        var track = StraightTrack();
        track.Add(Marker(50, GpsMarkerType.Article, "Holz"));

        // Bis 50 m laufen, dort 20 s stehen bleiben, dann weiter.
        var spec = new List<(double, double, int)>();
        for (var m = 0; m <= 50; m++) spec.Add((m, 0.0, m));
        for (var s = 1; s <= 20; s++) spec.Add((50, 0.0, 50 + s));
        for (var m = 51; m <= 70; m++) spec.Add((m, 0.0, 70 + (m - 50)));

        var result = GpsTrackEvaluator.Evaluate(track, Walk(spec));

        var stop = Assert.Single(result.Stops);
        Assert.Equal(WalkStopKind.Indication, stop.Kind);
        Assert.Equal("Holz", stop.MarkerLabel);
        Assert.True(stop.DurationSeconds >= GpsTrackEvaluator.StopMinDurationSeconds);
    }

    [Fact]
    public void StopAtTreatPot_IsClassifiedAsExplained()
    {
        var track = StraightTrack();
        track.Add(Marker(50, GpsMarkerType.TreatPot, "Leckerli"));

        var spec = new List<(double, double, int)>();
        for (var m = 0; m <= 50; m++) spec.Add((m, 0.0, m));
        for (var s = 1; s <= 20; s++) spec.Add((50, 0.0, 50 + s));

        var result = GpsTrackEvaluator.Evaluate(track, Walk(spec));

        var stop = Assert.Single(result.Stops);
        Assert.Equal(WalkStopKind.Explained, stop.Kind);
    }

    /// <summary>
    /// Der eigentliche Zweck der Stockungs-Erkennung: Der Hund sucht/kreist im
    /// Leinenradius, der Hundeführer bleibt stehen. Die Position weicht dabei
    /// NICHT ab - sichtbar wird es nur über den Halt.
    /// </summary>
    [Fact]
    public void StopAwayFromAnyMarker_IsUnexplained()
    {
        var track = StraightTrack();

        var spec = new List<(double, double, int)>();
        for (var m = 0; m <= 40; m++) spec.Add((m, 0.0, m));
        for (var s = 1; s <= 25; s++) spec.Add((40, 0.0, 40 + s));

        var result = GpsTrackEvaluator.Evaluate(track, Walk(spec));

        var stop = Assert.Single(result.Stops);
        Assert.Equal(WalkStopKind.Unexplained, stop.Kind);
        Assert.Null(stop.MarkerLabel);
        // Trotz Halt keine Positionsabweichung - genau der blinde Fleck, den
        // die Stockung schließt.
        Assert.True(result.AvgDeviationMeters < 0.5);
    }

    [Fact]
    public void BriefPause_BelowThreshold_IsNotAStop()
    {
        var track = StraightTrack();

        var spec = new List<(double, double, int)>();
        for (var m = 0; m <= 40; m++) spec.Add((m, 0.0, m));
        // Nur 5 s stehen -> unter StopMinDurationSeconds (10 s).
        for (var s = 1; s <= 5; s++) spec.Add((40, 0.0, 40 + s));
        for (var m = 41; m <= 60; m++) spec.Add((m, 0.0, 45 + (m - 40)));

        var result = GpsTrackEvaluator.Evaluate(track, Walk(spec));

        Assert.Empty(result.Stops);
    }

    [Fact]
    public void EmptyInput_DoesNotThrow()
    {
        var empty = GpsTrackEvaluator.Evaluate([], []);
        Assert.Equal(0, empty.ArticlesTotal);
        Assert.Empty(empty.Points);

        var noWalk = GpsTrackEvaluator.Evaluate(StraightTrack(), []);
        Assert.Empty(noWalk.Points);
        Assert.Empty(noWalk.Stops);
    }

    [Fact]
    public void ManualMarkers_DoNotDistortTheLine()
    {
        var track = StraightTrack();
        // Marker deutlich neben der Linie (wie in der Praxis: Gegenstand liegt
        // seitlich) - darf die gemessene Linie nicht verbiegen.
        track.Add(new GpsPoint
        {
            Latitude = LatAt(50),
            Longitude = LonOffset(25),
            Timestamp = T0.AddSeconds(50),
            PointType = GpsPointType.Manual,
            MarkerType = GpsMarkerType.Article
        });

        var walk = Walk(Enumerable.Range(0, 101).Select(m => ((double)m, 0.0, m)));
        var result = GpsTrackEvaluator.Evaluate(track, walk);

        Assert.True(result.AvgDeviationMeters < 0.5, $"Ø war {result.AvgDeviationMeters}");
    }
}
