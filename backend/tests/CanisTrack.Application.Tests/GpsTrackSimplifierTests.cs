using CanisTrack.Application.Tracking;
using CanisTrack.Domain.Tracking;

namespace CanisTrack.Application.Tests;

public class GpsTrackSimplifierTests
{
    private static GpsPoint MakePoint(double latitude, double longitude) =>
        new() { Latitude = latitude, Longitude = longitude, Timestamp = DateTimeOffset.UtcNow };

    [Fact]
    public void Simplify_BelowThreshold_ReturnsAllPointsUnchanged()
    {
        var points = Enumerable.Range(0, 1000)
            .Select(i => MakePoint(52.5 + i * 0.0001, 13.4 + i * 0.0001))
            .ToList();

        var result = GpsTrackSimplifier.Simplify(points);

        Assert.Equal(points.Count, result.Count);
        Assert.Same(points, result);
    }

    [Fact]
    public void Simplify_StraightLineWithManyPoints_ReducesToEndpoints()
    {
        // 2500 Punkte exakt auf einer Geraden - alle Zwischenpunkte sind
        // redundant und sollten entfernt werden.
        var points = Enumerable.Range(0, 2500)
            .Select(i => MakePoint(52.5 + i * 0.000001, 13.4 + i * 0.000001))
            .ToList();

        var result = GpsTrackSimplifier.Simplify(points);

        Assert.True(result.Count < points.Count);
        Assert.Equal(points[0].Latitude, result[0].Latitude);
        Assert.Equal(points[^1].Latitude, result[^1].Latitude);
    }

    [Fact]
    public void Simplify_KeepsSignificantDetour()
    {
        // 2200 Punkte auf einer Geraden, aber ein Punkt in der Mitte weicht
        // deutlich (>1m Toleranz) ab - dieser muss erhalten bleiben, damit
        // die Linienführung nicht verfälscht wird.
        var points = Enumerable.Range(0, 2200)
            .Select(i => MakePoint(52.5 + i * 0.000001, 13.4))
            .ToList();
        var detourIndex = points.Count / 2;
        points[detourIndex] = MakePoint(points[detourIndex].Latitude, 13.4 + 0.001);

        var result = GpsTrackSimplifier.Simplify(points);

        Assert.Contains(result, p => p.Longitude == points[detourIndex].Longitude);
    }

    [Fact]
    public void Simplify_AlwaysKeepsFirstAndLastPoint()
    {
        var points = Enumerable.Range(0, 2100)
            .Select(i => MakePoint(52.5 + i * 0.000002, 13.4 + i * 0.000003))
            .ToList();

        var result = GpsTrackSimplifier.Simplify(points);

        Assert.Equal(points[0].Latitude, result[0].Latitude);
        Assert.Equal(points[0].Longitude, result[0].Longitude);
        Assert.Equal(points[^1].Latitude, result[^1].Latitude);
        Assert.Equal(points[^1].Longitude, result[^1].Longitude);
    }
}
