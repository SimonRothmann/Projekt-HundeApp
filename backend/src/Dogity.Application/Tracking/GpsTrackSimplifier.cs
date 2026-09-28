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

    // Douglas-Peucker kostet im ungünstigsten Fall quadratisch viel: Bei einem
    // Zickzack teilt jeder Schritt nur einen Punkt ab und misst dafür den
    // ganzen Rest erneut. 50.000 solche Punkte - und die schickt der Client -
    // waren über 20 Sekunden Rechenzeit, bei jedem Anzeigen der Fährte. Längere
    // Linien werden deshalb vorher gleichmäßig ausgedünnt. Das trifft erst
    // Aufzeichnungen über rund anderthalb Stunden bei einem Punkt pro Sekunde;
    // dort liegen die verbleibenden Punkte wenige Meter auseinander.
    private const int MaxPointsForDouglasPeucker = 5000;

    public static IReadOnlyList<T> Simplify<T>(IReadOnlyList<T> points) where T : IGeoPoint
    {
        if (points.Count < MinPointsBeforeSimplifying)
            return points;

        var input = points.Count > MaxPointsForDouglasPeucker ? Thin(points, MaxPointsForDouglasPeucker) : points;
        var projected = Project(input);
        var keepIndices = new HashSet<int> { 0, input.Count - 1 };
        Reduce(projected, 0, input.Count - 1, ToleranceMeters, keepIndices);

        return keepIndices.OrderBy(i => i).Select(i => input[i]).ToList();
    }

    /// <summary>Jeder n-te Punkt, so dass höchstens <paramref name="max"/> (plus der letzte) bleiben.</summary>
    private static IReadOnlyList<T> Thin<T>(IReadOnlyList<T> points, int max)
    {
        var step = (int)Math.Ceiling(points.Count / (double)max);
        var result = new List<T>(max + 1);
        for (var i = 0; i < points.Count; i += step)
            result.Add(points[i]);
        // Das Ende der Fährte bleibt immer erhalten.
        if ((points.Count - 1) % step != 0)
            result.Add(points[^1]);
        return result;
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

    // Mit eigenem Stapel statt Rekursion: Douglas-Peucker teilt im
    // ungünstigsten Fall (etwa eine Spirale) bei jedem Schritt nur einen Punkt
    // ab. Rekursiv wären das so viele Aufrufebenen wie Punkte - bei zehntausenden
    // Punkten ein StackOverflow, und der beendet den ganzen Prozess, nicht nur
    // die Anfrage. Seit 2026-09-28 wertet der Server große Fährten über diese
    // Vereinfachung aus, und die Punkte schickt der Client.
    private static void Reduce((double X, double Y)[] points, int startIndex, int endIndex, double tolerance, HashSet<int> keepIndices)
    {
        var offen = new Stack<(int Start, int End)>();
        offen.Push((startIndex, endIndex));

        while (offen.Count > 0)
        {
            var (von, bis) = offen.Pop();
            if (bis <= von + 1) continue;

            var start = points[von];
            var end = points[bis];
            var maxDistance = 0.0;
            var maxIndex = -1;

            for (var i = von + 1; i < bis; i++)
            {
                var distance = PerpendicularDistance(points[i], start, end);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }

            if (maxIndex == -1 || maxDistance <= tolerance) continue;

            keepIndices.Add(maxIndex);
            offen.Push((von, maxIndex));
            offen.Push((maxIndex, bis));
        }
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
