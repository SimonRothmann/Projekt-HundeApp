using Dogity.Domain.Tracking;

namespace Dogity.Application.Tracking;

/// <summary>Ein Winkel der gelegten Fährte: Richtungsänderung, Lage und Stationierung (Meter ab Start).</summary>
public readonly record struct FaehrtenWinkel(
    int Number,
    CornerDirection Direction,
    bool Sharp,
    double TurnDegrees,
    double Latitude,
    double Longitude,
    double StationMeters);

/// <summary>Ein Schenkel der gelegten Fährte: der Abschnitt zwischen Start, Winkeln und Ende.</summary>
public readonly record struct FaehrtenSchenkel(int Number, double LengthMeters, double StartMeters, double EndMeters);

/// <summary>
/// Winkel und Schenkel einer gelegten Fährte - rein und deterministisch, wie
/// <see cref="GpsTrackEvaluator"/>. Eingabe sind die automatischen Punkte der Legung (ohne
/// Marker, nach Zeit sortiert).
///
/// Die Linie wird vorher vereinfacht (Douglas-Peucker), damit GPS-Zittern keine Winkel erzeugt;
/// alle Längen und Stationierungen (Meter ab Start) gelten entlang dieser vereinfachten Linie.
/// Die Winkelerkennung ist bewusst von der Haptik (use-walk-run-haptics, warnt früh ab 30 Grad)
/// getrennt: Dort zählt die frühe Warnung, hier das sichere Zählen.
/// </summary>
public sealed class FaehrtenGeometrie
{
    /// <summary>
    /// Toleranz der Vereinfachung. Der GPS-Fehler liegt bei 3-8 m; ein Ausschlag unter 4 m
    /// seitlich der Linie ist Rauschen, keine Richtungsänderung. Eigene Zahl, unabhängig von der
    /// Anzeige-Vereinfachung (GpsTrackSimplifier, 1 m), die jede Abbiegung erhalten soll.
    /// </summary>
    public const double VereinfachungToleranzMeters = 4.0;

    /// <summary>
    /// Ab dieser Richtungsänderung zählt ein Knick als Winkel. Fährten werden mit rechten und
    /// spitzen Winkeln gelegt; Kurven und Geländeversatz bleiben deutlich darunter.
    /// </summary>
    public const double WinkelMinDegrees = 45.0;

    /// <summary>
    /// Über dieser Richtungsänderung ist ein Winkel spitz (Innenwinkel unter 75 Grad). Ein rechter
    /// Winkel (90 Grad) bleibt auch mit 15 Grad GPS-Streuung normal.
    /// </summary>
    public const double SpitzAbDegrees = 105.0;

    /// <summary>
    /// Mindestlänge eines Abschnitts beiderseits eines Winkels und zugleich der Abstand, ab dem zwei
    /// Knicke getrennte Winkel sind. Kürzeres liegt im GPS-Fehler: Zwei Knicke dichter beisammen
    /// sind ein Winkel (etwa ein Bogen aus zwei halben Wendungen), ein Winkel kurz nach dem Start
    /// oder vor dem Ende ist nicht sicher zu erkennen.
    /// </summary>
    public const double MinAbschnittMeters = 10.0;

    /// <summary>
    /// Ab dieser Richtungsänderung nimmt ein Knick an der Zusammenfassung naher Knicke teil.
    /// Darunter ist es ein Rest der Vereinfachung einer Kurve; würde er mitzählen, verkettete sich
    /// eine lange, leicht wellige Strecke zu einem einzigen "Winkel".
    /// </summary>
    private const double KnickMinDegrees = 20.0;

    /// <summary>
    /// Obergrenze für die Zahl der Winkel. Echte Fährten haben eine Handvoll; die Punkte schickt
    /// der Client, und eine Zickzack-Aufzeichnung mit Abschnitten ab 10 m erzeugte tausende Winkel
    /// und Schenkel - Rechenzeit je Auswertung und ebenso viele Zeilen je Ablauf. Darüber gibt es
    /// weder Winkel noch Schenkel.
    /// </summary>
    public const int MaxWinkel = 100;

    private static readonly FaehrtenGeometrie Leer = new([], [], 0, [], [], 0);

    public IReadOnlyList<FaehrtenWinkel> Winkel { get; }
    public IReadOnlyList<FaehrtenSchenkel> Schenkel { get; }

    /// <summary>Länge der vereinfachten Linie in Metern.</summary>
    public double LengthMeters { get; }

    // Die vereinfachte Linie in der lokalen Meter-Ebene (Bezug: Breitengrad des ersten Punkts) mit
    // der Stationierung jedes Punkts - die Auswertung projiziert die Ablaufpunkte darauf.
    private readonly (double X, double Y)[] _linie;
    private readonly double[] _stationen;
    private readonly double _originLat;

    private FaehrtenGeometrie(
        IReadOnlyList<FaehrtenWinkel> winkel,
        IReadOnlyList<FaehrtenSchenkel> schenkel,
        double length,
        (double X, double Y)[] linie,
        double[] stationen,
        double originLat)
    {
        Winkel = winkel;
        Schenkel = schenkel;
        LengthMeters = length;
        _linie = linie;
        _stationen = stationen;
        _originLat = originLat;
    }

    public static FaehrtenGeometrie Berechnen(IReadOnlyList<GpsPoint> automatischePunkte)
    {
        if (automatischePunkte.Count < 2) return Leer;

        var originLat = automatischePunkte[0].Latitude;
        var vereinfacht = GpsTrackSimplifier.ReduceToTolerance(automatischePunkte, VereinfachungToleranzMeters);

        // Aufeinanderfolgende Punkte an derselben Stelle ergäben Abschnitte ohne Richtung.
        var punkte = new List<GpsPoint>(vereinfacht.Count);
        var ebene = new List<(double X, double Y)>(vereinfacht.Count);
        foreach (var p in vereinfacht)
        {
            var xy = GpsTrackEvaluator.Project(p.Latitude, p.Longitude, originLat);
            if (ebene.Count > 0 && GpsTrackEvaluator.Distance(xy, ebene[^1]) < 0.01) continue;
            punkte.Add(p);
            ebene.Add(xy);
        }

        if (ebene.Count < 2) return Leer;

        var stationen = new double[ebene.Count];
        for (var i = 1; i < ebene.Count; i++)
            stationen[i] = stationen[i - 1] + GpsTrackEvaluator.Distance(ebene[i], ebene[i - 1]);
        var laenge = stationen[^1];

        var winkel = ErkenneWinkel(punkte, ebene, stationen, laenge);
        if (winkel.Count > MaxWinkel) return Leer;

        var grenzen = new List<double> { 0 };
        grenzen.AddRange(winkel.Select(w => w.StationMeters));
        grenzen.Add(laenge);
        var schenkel = new List<FaehrtenSchenkel>(grenzen.Count - 1);
        for (var i = 0; i < grenzen.Count - 1; i++)
            schenkel.Add(new FaehrtenSchenkel(i + 1, grenzen[i + 1] - grenzen[i], grenzen[i], grenzen[i + 1]));

        return new FaehrtenGeometrie(winkel, schenkel, laenge, ebene.ToArray(), stationen, originLat);
    }

    /// <summary>
    /// Stationierung (Meter ab Start) des Punkts auf der Linie, der dem übergebenen am nächsten liegt.
    /// </summary>
    public double Stationierung(double latitude, double longitude)
    {
        var punkt = GpsTrackEvaluator.Project(latitude, longitude, _originLat);
        var bester = double.MaxValue;
        var station = 0.0;
        for (var i = 0; i < _linie.Length - 1; i++)
        {
            var a = _linie[i];
            var dx = _linie[i + 1].X - a.X;
            var dy = _linie[i + 1].Y - a.Y;
            var t = Math.Clamp(((punkt.X - a.X) * dx + (punkt.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            var abstand = GpsTrackEvaluator.Distance(punkt, (a.X + t * dx, a.Y + t * dy));
            if (abstand < bester)
            {
                bester = abstand;
                station = _stationen[i] + t * (_stationen[i + 1] - _stationen[i]);
            }
        }

        return station;
    }

    private static List<FaehrtenWinkel> ErkenneWinkel(
        List<GpsPoint> punkte, List<(double X, double Y)> ebene, double[] stationen, double laenge)
    {
        // Richtungsänderung je innerem Eckpunkt, mit Vorzeichen: Das Kreuzprodukt von
        // ankommender und abgehender Richtung ist positiv bei einer Linkskurve (X Ost, Y Nord).
        var knicke = new List<(int Index, double Turn)>();
        for (var i = 1; i < ebene.Count - 1; i++)
        {
            var inX = ebene[i].X - ebene[i - 1].X;
            var inY = ebene[i].Y - ebene[i - 1].Y;
            var outX = ebene[i + 1].X - ebene[i].X;
            var outY = ebene[i + 1].Y - ebene[i].Y;
            var turn = Math.Atan2(inX * outY - inY * outX, inX * outX + inY * outY) * 180 / Math.PI;
            if (Math.Abs(turn) >= KnickMinDegrees) knicke.Add((i, turn));
        }

        // Knicke, die näher als MinAbschnittMeters beieinander liegen, bilden einen Winkel.
        var gruppen = new List<List<(int Index, double Turn)>>();
        foreach (var knick in knicke)
        {
            if (gruppen.Count > 0 && stationen[knick.Index] - stationen[gruppen[^1][^1].Index] < MinAbschnittMeters)
                gruppen[^1].Add(knick);
            else
                gruppen.Add([knick]);
        }

        var winkel = new List<FaehrtenWinkel>();
        foreach (var gruppe in gruppen)
        {
            var summe = gruppe.Sum(k => k.Turn);
            var ersteStation = stationen[gruppe[0].Index];
            var letzteStation = stationen[gruppe[^1].Index];
            // Zwischen den Gruppen liegen per Bau mindestens MinAbschnittMeters; offen bleiben nur
            // der erste Abschnitt ab Start und der letzte bis zum Ende.
            if (Math.Abs(summe) < WinkelMinDegrees) continue;
            if (ersteStation < MinAbschnittMeters || laenge - letzteStation < MinAbschnittMeters) continue;

            var lage = (ersteStation + letzteStation) / 2;
            var (lat, lon) = LageBei(punkte, stationen, lage);
            winkel.Add(new FaehrtenWinkel(
                winkel.Count + 1,
                summe > 0 ? CornerDirection.Left : CornerDirection.Right,
                Math.Abs(summe) > SpitzAbDegrees,
                Math.Abs(summe),
                lat,
                lon,
                lage));
        }

        return winkel;
    }

    /// <summary>Der Punkt der Linie bei dieser Stationierung (linear zwischen den Stützpunkten).</summary>
    private static (double Latitude, double Longitude) LageBei(List<GpsPoint> punkte, double[] stationen, double station)
    {
        var i = 0;
        while (i < stationen.Length - 2 && stationen[i + 1] < station) i++;
        var t = (station - stationen[i]) / (stationen[i + 1] - stationen[i]);
        return (
            punkte[i].Latitude + t * (punkte[i + 1].Latitude - punkte[i].Latitude),
            punkte[i].Longitude + t * (punkte[i + 1].Longitude - punkte[i].Longitude));
    }
}
