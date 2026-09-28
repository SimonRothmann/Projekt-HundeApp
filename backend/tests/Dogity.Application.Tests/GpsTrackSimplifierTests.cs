using Dogity.Application.Tracking;
using Dogity.Domain.Tracking;

namespace Dogity.Application.Tests;

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

    [Fact]
    public void Simplify_ZickzackMitSehrVielenPunkten_BleibtSchnell()
    {
        // Der ungünstigste Fall für Douglas-Peucker: Jeder Schritt teilt nur
        // einen Punkt ab. 50.000 solche Punkte brauchten über 20 Sekunden -
        // jetzt wird vorher auf höchstens 5.000 ausgedünnt.
        var points = Enumerable.Range(0, 50_000)
            .Select(i => MakePoint(52.5 + i * 0.000005, i % 2 == 0 ? 13.4 : 13.40003))
            .ToList();

        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var result = GpsTrackSimplifier.Simplify(points);
        uhr.Stop();

        Assert.Equal(points[0], result[0]);
        Assert.Equal(points[^1], result[^1]);
        Assert.True(result.Count <= 5_001, $"{result.Count} Punkte");
        // Großzügig, damit ein langsamer Testrechner nicht wackelt - vorher
        // waren es über 20 Sekunden.
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(5), $"{uhr.Elapsed.TotalSeconds:F1} s");
    }

    [Fact]
    public void Simplify_AusduennenBehaeltDenLetztenPunkt()
    {
        var points = Enumerable.Range(0, 12_345)
            .Select(i => MakePoint(52.5 + i * 0.00001, 13.4 + (i % 7) * 0.00001))
            .ToList();

        var result = GpsTrackSimplifier.Simplify(points);

        Assert.Same(points[^1], result[^1]);
    }
}
