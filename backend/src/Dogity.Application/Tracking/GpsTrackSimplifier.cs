using Dogity.Domain.Tracking;

namespace Dogity.Application.Tracking;

/// <summary>
/// Reduziert die Anzahl der Punkte einer aufgezeichneten GPS-Linie beim
/// Ausliefern an den Client (Douglas-Peucker-Algorithmus), ohne die in der
/// Datenbank gespeicherten Rohpunkte zu verändern. Lange Fährten (z.B. 30+
/// Minuten bei einem Punkt pro Sekunde, siehe GPS_MAX_POSITION_AGE_MS im
/// Frontend) würden sonst tausende Punkte unkomprimiert über mobile
/// Verbindungen übertragen und client-seitig mit Leaflet gerendert - beides
/// auf dem Hundeplatz spürbar langsam.
/// </summary>
public static class GpsTrackSimplifier
{
    // Bewusst eng: bei Schrittgeschwindigkeit liegen aufeinanderfolgende
    // Punkte oft nur ~1m auseinander, eine scharfe Abbiegung (z.B. 90°-Winkel
    // einer Fährte) weicht dann nur 0,5-1m von der Geraden zwischen
    // Vorgänger- und Nachfolgepunkt ab. Eine zu großzügige Toleranz würde
    // genau die Abbiegungen wegglätten, die für die Fährtenarbeit zählen
    // (siehe TODO.md) - 1m liegt knapp unterhalb solcher Winkel, entfernt
    // aber noch redundante Punkte auf wirklich geraden Abschnitten.
    private const double ToleranceMeters = 1.0;

    // Nur als Sicherheitsnetz für pathologisch lange Aufzeichnungen (z.B.
    // versehentlich stundenlang laufen gelassen) - reguläre Trainingstracks
    // (auch bei mehreren Punkten/Sekunde) sollen unverändert bleiben, damit
    // keine Abbiegung verloren geht.
    private const int MinPointsBeforeSimplifying = 2000;

    public static IReadOnlyList<T> Simplify<T>(IReadOnlyList<T> points) where T : IGeoPoint
    {
        if (points.Count < MinPointsBeforeSimplifying)
            return points;

        var projected = Project(points);
        var keepIndices = new HashSet<int> { 0, points.Count - 1 };
        Reduce(projected, 0, points.Count - 1, ToleranceMeters, keepIndices);

        return keepIndices.OrderBy(i => i).Select(i => points[i]).ToList();
    }

    // Vereinfachte Äquirektangular-Projektion auf eine lokale Meter-Ebene -
    // für die kurzen Distanzen einer Fährte (Meter bis wenige Kilometer)
    // ausreichend genau, ohne die Komplexität einer echten Kartenprojektion.
    private static (double X, double Y)[] Project<T>(IReadOnlyList<T> points) where T : IGeoPoint
    {
        const double earthRadiusMeters = 6371000;
        var lat0Rad = points[0].Latitude * Math.PI / 180;

        var result = new (double X, double Y)[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            result[i] = (
                X: points[i].Longitude * Math.PI / 180 * Math.Cos(lat0Rad) * earthRadiusMeters,
                Y: points[i].Latitude * Math.PI / 180 * earthRadiusMeters);
        }
        return result;
    }

    private static void Reduce((double X, double Y)[] points, int startIndex, int endIndex, double tolerance, HashSet<int> keepIndices)
    {
        if (endIndex <= startIndex + 1) return;

        var start = points[startIndex];
        var end = points[endIndex];
        var maxDistance = 0.0;
        var maxIndex = -1;

        for (var i = startIndex + 1; i < endIndex; i++)
        {
            var distance = PerpendicularDistance(points[i], start, end);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        if (maxIndex == -1 || maxDistance <= tolerance) return;

        keepIndices.Add(maxIndex);
        Reduce(points, startIndex, maxIndex, tolerance, keepIndices);
        Reduce(points, maxIndex, endIndex, tolerance, keepIndices);
    }

    private static double PerpendicularDistance((double X, double Y) point, (double X, double Y) lineStart, (double X, double Y) lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return Distance(point, lineStart);

        var t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared;
        var projection = (X: lineStart.X + t * dx, Y: lineStart.Y + t * dy);
        return Distance(point, projection);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
