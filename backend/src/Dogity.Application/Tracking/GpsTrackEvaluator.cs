using Dogity.Domain.Tracking;

namespace Dogity.Application.Tracking;

/// <summary>Ein ausgewerteter Ablaufpunkt: Position samt Abweichung zur Fährte.</summary>
public readonly record struct EvaluatedWalkPoint(Guid PointId, double DeviationMeters);

/// <summary>Ein erkannter Halt (siehe <see cref="GpsWalkStop"/>).</summary>
public readonly record struct EvaluatedStop(
    double Latitude,
    double Longitude,
    int DurationSeconds,
    WalkStopKind Kind,
    string? MarkerLabel);

/// <summary>Gesamtergebnis der Auswertung eines Ablaufs.</summary>
public readonly record struct WalkRunEvaluation(
    double AvgDeviationMeters,
    double MaxDeviationMeters,
    double OnTrackPercent,
    int ArticlesFound,
    int ArticlesTotal,
    IReadOnlyList<EvaluatedWalkPoint> Points,
    IReadOnlyList<EvaluatedStop> Stops);

/// <summary>
/// Wertet einen Ablauf-Versuch gegen die gelegte Fährte aus - rein,
/// deterministisch und ohne Datenbank, damit identisch für Live-Pfad,
/// Backfill und Tests.
///
/// Messgrundlage ist der SENKRECHTE Abstand jedes Ablaufpunkts zur Linie der
/// gelegten Fährte (Punkt-zu-Segment, nicht Punkt-zu-Punkt). Das ist bewusst
/// versatz-invariant: Der Hundeführer läuft 5-10 m hinter dem Hund, liegt
/// dabei aber auf derselben Spur - die Abweichung bleibt dadurch ~0. Punkt-zu-
/// Punkt-Vergleiche würden den zeitlichen Versatz fälschlich als Fehler werten.
///
/// Bewusste Grenze: Gemessen wird die Linie des HUNDEFÜHRERS. Der Hund kann im
/// Radius der Fährtenleine (~10 m) ausscheren und zurückkommen, ohne dass sich
/// das Gerät bewegt. Diese Lücke schließen die erkannten Stockungen: bleibt der
/// Hundeführer stehen (weil der Hund sucht, verweist oder frisst), sinkt die
/// Verschiebung gegen null - das ist auch ohne Positionsabweichung sichtbar.
/// </summary>
public static class GpsTrackEvaluator
{
    /// <summary>Bis hierher gilt ein Punkt als "auf der Fährte" (großzügig wegen 3-8 m GPS-Fehler).</summary>
    public const double OnTrackThresholdMeters = 3.0;

    /// <summary>Ampel: bis hier grün, bis <see cref="AmberMaxMeters"/> gelb, darüber rot.</summary>
    public const double GreenMaxMeters = 3.0;
    public const double AmberMaxMeters = 6.0;

    /// <summary>
    /// Radius, in dem ein Marker als erreicht bzw. ein Halt als erklärt gilt -
    /// identisch mit dem Auslöseradius der Ablauf-Haptik (use-walk-run-haptics),
    /// damit Vibration und Auswertung dieselbe Wahrheit verwenden.
    /// </summary>
    public const double MarkerProximityMeters = 8.0;

    /// <summary>Ein Halt: Netto-Verschiebung unter diesem Wert über mindestens <see cref="StopMinDurationSeconds"/>.</summary>
    public const double StopMaxDisplacementMeters = 3.0;
    public const int StopMinDurationSeconds = 10;

    public static WalkRunEvaluation Evaluate(
        IReadOnlyList<GpsPoint> laidPoints,
        IReadOnlyList<GpsWalkPoint> walkPoints)
    {
        // Die gelegte Linie besteht nur aus automatischen Punkten - manuelle
        // Marker liegen neben der Laufspur und würden die Linie verzerren
        // (dieselbe Regel wie in estimateLengthMeters/GpsTrackSimplifier).
        var line = laidPoints
            .Where(p => p.PointType != GpsPointType.Manual)
            .OrderBy(p => p.Timestamp)
            .ToList();
        var markers = laidPoints.Where(p => p.PointType == GpsPointType.Manual).ToList();
        var walk = walkPoints.OrderBy(p => p.Timestamp).ToList();

        var articlesTotal = markers.Count(m => m.MarkerType == GpsMarkerType.Article);

        if (line.Count == 0 || walk.Count == 0)
            return new WalkRunEvaluation(0, 0, 0, 0, articlesTotal, [], []);

        // Gemeinsamer lokaler Meter-Bezug für alle Punkte (äquirektangulär,
        // für Fährtendistanzen ausreichend genau - siehe GpsTrackSimplifier).
        var originLat = line[0].Latitude;
        var linePlane = line.Select(p => Project(p.Latitude, p.Longitude, originLat)).ToArray();

        // ---- Abweichung je Ablaufpunkt ----
        var evaluated = new List<EvaluatedWalkPoint>(walk.Count);
        var deviations = new double[walk.Count];
        for (var i = 0; i < walk.Count; i++)
        {
            var wp = Project(walk[i].Latitude, walk[i].Longitude, originLat);
            var deviation = DistanceToPolyline(wp, linePlane);
            deviations[i] = deviation;
            evaluated.Add(new EvaluatedWalkPoint(walk[i].Id, deviation));
        }

        var avg = deviations.Average();
        var max = deviations.Max();
        var onTrack = deviations.Count(d => d <= OnTrackThresholdMeters) * 100.0 / deviations.Length;

        // ---- Gegenstände: kam der Hundeführer nah genug heran? ----
        var articlesFound = markers
            .Where(m => m.MarkerType == GpsMarkerType.Article)
            .Count(m =>
            {
                var mp = Project(m.Latitude, m.Longitude, originLat);
                return walk.Any(w => Distance(Project(w.Latitude, w.Longitude, originLat), mp) <= MarkerProximityMeters);
            });

        var stops = DetectStops(walk, markers, originLat);

        return new WalkRunEvaluation(avg, max, onTrack, articlesFound, articlesTotal, evaluated, stops);
    }

    /// <summary>
    /// Erkennt Halte über ein gleitendes Zeitfenster: Ab dem ersten Punkt eines
    /// Fensters wird solange erweitert, wie alle Punkte innerhalb von
    /// <see cref="StopMaxDisplacementMeters"/> um den Startpunkt bleiben. Dauert
    /// das Fenster mindestens <see cref="StopMinDurationSeconds"/>, gilt es als
    /// Halt. Robuster als Momentangeschwindigkeit, die bei GPS-Rauschen
    /// flackert.
    /// </summary>
    private static List<EvaluatedStop> DetectStops(
        IReadOnlyList<GpsWalkPoint> walk,
        IReadOnlyList<GpsPoint> markers,
        double originLat)
    {
        var stops = new List<EvaluatedStop>();
        var i = 0;
        while (i < walk.Count)
        {
            var anchor = Project(walk[i].Latitude, walk[i].Longitude, originLat);
            var j = i + 1;
            while (j < walk.Count && Distance(Project(walk[j].Latitude, walk[j].Longitude, originLat), anchor) <= StopMaxDisplacementMeters)
                j++;

            var lastIndex = j - 1;
            var seconds = (int)Math.Round((walk[lastIndex].Timestamp - walk[i].Timestamp).TotalSeconds);
            if (lastIndex > i && seconds >= StopMinDurationSeconds)
            {
                var (kind, label) = ClassifyStop(anchor, markers, originLat);
                stops.Add(new EvaluatedStop(walk[i].Latitude, walk[i].Longitude, seconds, kind, label));
                i = j; // Fenster abschließen, nicht überlappend erneut zählen
            }
            else
            {
                i++;
            }
        }

        return stops;
    }

    private static (WalkStopKind Kind, string? Label) ClassifyStop(
        (double X, double Y) stop,
        IReadOnlyList<GpsPoint> markers,
        double originLat)
    {
        GpsPoint? nearest = null;
        var nearestDistance = double.MaxValue;
        foreach (var m in markers)
        {
            var d = Distance(Project(m.Latitude, m.Longitude, originLat), stop);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearest = m;
            }
        }

        if (nearest is null || nearestDistance > MarkerProximityMeters)
            return (WalkStopKind.Unexplained, null);

        // Halt am Gegenstand = Verweisen (erwünscht); an Leckerlipot/Verleitung
        // erklärt und neutral.
        var kind = nearest.MarkerType == GpsMarkerType.Article ? WalkStopKind.Indication : WalkStopKind.Explained;
        return (kind, nearest.Label);
    }

    /// <summary>Kürzester Abstand eines Punkts zur Polylinie (Minimum über alle Segmente).</summary>
    private static double DistanceToPolyline((double X, double Y) point, (double X, double Y)[] line)
    {
        if (line.Length == 1) return Distance(point, line[0]);

        var min = double.MaxValue;
        for (var i = 0; i < line.Length - 1; i++)
        {
            var d = DistanceToSegment(point, line[i], line[i + 1]);
            if (d < min) min = d;
        }

        return min;
    }

    private static double DistanceToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return Distance(p, a);

        // Projektionsparameter auf [0,1] begrenzen: außerhalb des Segments
        // zählt der Abstand zum jeweiligen Endpunkt.
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return Distance(p, (a.X + t * dx, a.Y + t * dy));
    }

    private static (double X, double Y) Project(double latitude, double longitude, double originLat)
    {
        const double earthRadiusMeters = 6371000;
        var lat0Rad = originLat * Math.PI / 180;
        return (
            X: longitude * Math.PI / 180 * Math.Cos(lat0Rad) * earthRadiusMeters,
            Y: latitude * Math.PI / 180 * earthRadiusMeters);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
