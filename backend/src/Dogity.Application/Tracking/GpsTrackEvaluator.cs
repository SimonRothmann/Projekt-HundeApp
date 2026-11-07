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

/// <summary>Ein Schenkel der Fährte mit dem, was der Ablauf darauf gemacht hat (siehe <see cref="GpsWalkRunLeg"/>).</summary>
public readonly record struct EvaluatedLeg(
    int Number,
    double LengthMeters,
    int? DurationSeconds,
    double? SpeedMetersPerMinute,
    double? AvgDeviationMeters);

/// <summary>Ein Winkel der Fährte mit dem Umgang des Ablaufs damit (siehe <see cref="GpsWalkRunCorner"/>).</summary>
public readonly record struct EvaluatedCorner(
    int Number,
    CornerDirection Direction,
    bool Sharp,
    double TurnDegrees,
    double Latitude,
    double Longitude,
    bool Reached,
    double? OvershootMeters,
    bool Overshot,
    int HoldSeconds,
    int? TimeAtCornerSeconds);

/// <summary>Gesamtergebnis der Auswertung eines Ablaufs.</summary>
public readonly record struct WalkRunEvaluation(
    double AvgDeviationMeters,
    double MaxDeviationMeters,
    double OnTrackPercent,
    int ArticlesFound,
    int ArticlesTotal,
    IReadOnlyList<EvaluatedWalkPoint> Points,
    IReadOnlyList<EvaluatedStop> Stops,
    // Nur Fährte (sonst leer): siehe FaehrtenGeometrie.
    IReadOnlyList<EvaluatedLeg> Legs,
    IReadOnlyList<EvaluatedCorner> Corners);

/// <summary>
/// Grenzen der Auswertung für eine Art der Aufzeichnung: bis <see cref="GreenMaxMeters"/>
/// grün, bis <see cref="AmberMaxMeters"/> gelb, darüber rot; "auf der Spur" zählt ein
/// Punkt bis <see cref="OnTrackMeters"/>.
/// </summary>
public readonly record struct SpurToleranz(double OnTrackMeters, double GreenMaxMeters, double AmberMaxMeters);

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
    /// <summary>
    /// Fährte: bis 3 m "auf der Fährte" und grün, bis 6 m gelb - großzügig wegen
    /// 3-8 m GPS-Fehler. Die einzige Stelle für diese Zahlen; das Gegenstück im
    /// Frontend ist <c>spurGrenzen</c> in lib/mantrailing.ts (beim Ändern beide anfassen).
    /// </summary>
    public static readonly SpurToleranz FaehrteToleranz = new(3.0, 3.0, 6.0);

    /// <summary>
    /// Mantrailing: bis 10 m "auf der Spur" und grün, bis 25 m gelb. Der Hund arbeitet an
    /// langer Leine und sucht den Geruch auch neben der Laufspur der Versteckperson
    /// (Wind, Gelände); eng an der Linie wie bei der Fährte ist hier nicht das Ziel.
    /// </summary>
    public static readonly SpurToleranz MantrailingToleranz = new(10.0, 10.0, 25.0);

    public static SpurToleranz ToleranzFuer(GpsTrackKind kind) =>
        kind == GpsTrackKind.Mantrailing ? MantrailingToleranz : FaehrteToleranz;

    /// <summary>
    /// Radius, in dem ein Marker als erreicht bzw. ein Halt als erklärt gilt -
    /// identisch mit dem Auslöseradius der Ablauf-Haptik (use-walk-run-haptics),
    /// damit Vibration und Auswertung dieselbe Wahrheit verwenden.
    /// </summary>
    public const double MarkerProximityMeters = 8.0;

    /// <summary>Ein Halt: Netto-Verschiebung unter diesem Wert über mindestens <see cref="StopMinDurationSeconds"/>.</summary>
    public const double StopMaxDisplacementMeters = 3.0;
    public const int StopMinDurationSeconds = 10;

    /// <summary>
    /// Obergrenze für Ablaufpunkte × (Liniensegmente + Marker). Jeder
    /// Ablaufpunkt wird gegen jedes Segment gemessen; 2·10⁸ Abstände sind rund eine Sekunde
    /// Rechenzeit. Echte Fährten liegen weit darunter (zwei Stunden Legen und
    /// Suchen bei einem Punkt pro Sekunde: 7.200 × höchstens 7.200). Darüber
    /// wird nicht ausgewertet - die Punkte schickt der Client, und ohne Grenze
    /// band eine einzige präparierte Anfrage den gemeinsamen Server für
    /// Minuten (Prüfung 2026-09-28).
    /// </summary>
    public const long MaxAuswertungsAufwand = 200_000_000;

    /// <summary>
    /// Stand der Auswertung, wie er am Ablauf steht (<see cref="GpsWalkRun.EvaluationVersion"/>).
    /// Hochzählen, wenn bestehende Auswertungen neu zu rechnen sind - der Backfill holt dann alle
    /// Abläufe darunter beim nächsten Start nach. 1 = mit Winkeln und Schenkeln.
    /// </summary>
    public const int AktuelleVersion = 1;

    /// <summary>
    /// Fenster um einen Winkel, in dem ein Ausschlag als Überschießen zählt: ein kleines Stück davor
    /// (das GPS eilt vor) und weit genug dahinter, um den Hund zu fassen, der geradeaus weiterläuft,
    /// bevor er sich korrigiert.
    /// </summary>
    private const double UeberschiessenDavorMeters = 5.0;
    private const double UeberschiessenDahinterMeters = 20.0;

    /// <summary>Halbe Breite des Fensters, in dem die Zeit am Winkel gemessen wird (10 m davor bis 10 m dahinter).</summary>
    private const double ZeitAmWinkelHalbMeters = 10.0;

    /// <summary>Rundungsspielraum beim Vergleich von Stationierungen (Gleitkomma), kein fachlicher Spielraum.</summary>
    private const double StationEpsilon = 1e-6;

    /// <summary>Punkte, die für die Halteerkennung mindestens so weit auseinanderliegen müssen.</summary>
    private static readonly TimeSpan StopMinPointSpacing = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Die Auswertung - oder <c>null</c>, wenn sie über
    /// <see cref="MaxAuswertungsAufwand"/> läge.
    /// </summary>
    public static WalkRunEvaluation? TryEvaluate(
        IReadOnlyList<GpsPoint> laidPoints,
        IReadOnlyList<GpsWalkPoint> walkPoints,
        GpsTrackKind kind = GpsTrackKind.Faehrte)
    {
        // Die gelegte Linie besteht nur aus automatischen Punkten - manuelle
        // Marker liegen neben der Laufspur und würden die Linie verzerren
        // (dieselbe Regel wie in estimateLengthMeters/GpsTrackSimplifier).
        //
        // Lange Linien (ab 2000 Punkten) werden vorher vereinfacht - genau so,
        // wie die Karte sie zeigt (GpsTrackSimplifier). Kürzere bleiben
        // unverändert, damit sich keine Auswertung einer echten Fährte ändert.
        var line = GpsTrackSimplifier.Simplify(laidPoints
            .Where(p => p.PointType != GpsPointType.Manual)
            .OrderBy(p => p.Timestamp)
            .ToList());
        var markers = laidPoints.Where(p => p.PointType == GpsPointType.Manual).ToList();
        var walk = walkPoints.OrderBy(p => p.Timestamp).ToList();

        // Gegenstände gibt es nur bei der Fährte - beim Mantrailing wird nichts gezählt.
        var articlesTotal = kind == GpsTrackKind.Faehrte ? markers.Count(m => m.MarkerType == GpsMarkerType.Article) : 0;

        if (line.Count == 0 || walk.Count == 0)
            return new WalkRunEvaluation(0, 0, 0, 0, articlesTotal, [], [], [], []);

        // Auch die Marker zählen: Jeder Gegenstand wird gegen jeden
        // Ablaufpunkt geprüft, und wie viele Marker eine Fährte hat, bestimmt
        // der Client.
        if ((long)walk.Count * (line.Count + markers.Count) > MaxAuswertungsAufwand)
            return null;

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
        var onTrackMeters = ToleranzFuer(kind).OnTrackMeters;
        var onTrack = deviations.Count(d => d <= onTrackMeters) * 100.0 / deviations.Length;

        // ---- Gegenstände: kam der Hundeführer nah genug heran? ----
        var articlesFound = articlesTotal > 0 ? ErreichteGegenstaende(markers, walk, originLat).Count : 0;

        // ---- Winkel und Schenkel: nur bei der Fährte ----
        var geometrie = kind == GpsTrackKind.Faehrte ? FaehrtenGeometrie.Berechnen(line) : null;
        var winkelEbene = geometrie?.Winkel.Select(w => Project(w.Latitude, w.Longitude, originLat)).ToArray() ?? [];

        var stops = DetectStops(walk, markers, winkelEbene, originLat);
        var (legs, corners) = geometrie is null
            ? ([], [])
            : WinkelUndSchenkel(geometrie, walk, deviations, stops, winkelEbene, originLat);

        return new WalkRunEvaluation(avg, max, onTrack, articlesFound, articlesTotal, evaluated, stops, legs, corners);
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
        IReadOnlyList<GpsWalkPoint> alleAblaufpunkte,
        IReadOnlyList<GpsPoint> markers,
        (double X, double Y)[] winkelEbene,
        double originLat)
    {
        // Höchstens zwei Punkte pro Sekunde. Das Fenster unten wächst, solange
        // die Punkte beisammen bleiben, und beginnt sonst beim nächsten Punkt
        // neu - bei tausenden Punkten mit demselben Zeitstempel und Ort wäre
        // das quadratisch viel Arbeit. Echte Aufzeichnungen (etwa ein Punkt
        // pro Sekunde) bleiben unverändert.
        var walk = new List<GpsWalkPoint>(alleAblaufpunkte.Count);
        foreach (var p in alleAblaufpunkte)
        {
            if (walk.Count == 0 || p.Timestamp - walk[^1].Timestamp >= StopMinPointSpacing)
                walk.Add(p);
        }

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
                var (kind, label) = ClassifyStop(anchor, markers, winkelEbene, originLat);
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
        (double X, double Y)[] winkelEbene,
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
        {
            // Gegenstand und Leckerli gehen vor; ohne Marker erklärt ein Winkel den Halt: Der Hund
            // arbeitet ihn aus, das ist fachlich normal und kein Warnsignal.
            return (winkelEbene.Any(w => Distance(w, stop) <= MarkerProximityMeters)
                ? WalkStopKind.AtCorner
                : WalkStopKind.Unexplained, null);
        }

        // Halt am Gegenstand = Verweisen (erwünscht); an Leckerlipot/Verleitung
        // erklärt und neutral.
        var kind = nearest.MarkerType == GpsMarkerType.Article ? WalkStopKind.Indication : WalkStopKind.Explained;
        return (kind, nearest.Label);
    }

    /// <summary>
    /// Wertet den Ablauf gegen die Schenkel und Winkel der gelegten Fährte aus. Grundlage ist der
    /// FORTSCHRITT: Jeder Ablaufpunkt wird auf die vereinfachte Linie projiziert (Stationierung),
    /// das laufende Maximum gilt - Rauschen und Zurückgehen setzen den Fortschritt nicht zurück.
    /// </summary>
    private static (List<EvaluatedLeg> Legs, List<EvaluatedCorner> Corners) WinkelUndSchenkel(
        FaehrtenGeometrie geometrie,
        IReadOnlyList<GpsWalkPoint> walk,
        double[] deviations,
        IReadOnlyList<EvaluatedStop> stops,
        (double X, double Y)[] winkelEbene,
        double originLat)
    {
        var fortschritt = new double[walk.Count];
        var bisher = 0.0;
        for (var i = 0; i < walk.Count; i++)
        {
            var station = geometrie.Stationierung(walk[i].Latitude, walk[i].Longitude);
            bisher = i == 0 ? station : Math.Max(bisher, station);
            fortschritt[i] = bisher;
        }

        // Der erste Ablaufpunkt, dessen Fortschritt die Stationierung erreicht (-1 = nie erreicht).
        int ErsterBei(double station) => Array.FindIndex(fortschritt, f => f >= station - StationEpsilon);

        int? SekundenZwischen(int von, int bis) =>
            von < 0 || bis < 0 ? null : (int)Math.Round((walk[bis].Timestamp - walk[von].Timestamp).TotalSeconds);

        var legs = new List<EvaluatedLeg>(geometrie.Schenkel.Count);
        for (var k = 0; k < geometrie.Schenkel.Count; k++)
        {
            var schenkel = geometrie.Schenkel[k];
            // Am Fährtenende bleibt der Hundeführer meist ein paar Meter vor dem letzten gelegten
            // Punkt stehen (der Hund liegt am Endgegenstand, die Leine ist lang) - ohne Spielraum
            // fehlte dem letzten Schenkel fast immer die Dauer.
            var ende = k == geometrie.Schenkel.Count - 1
                ? Math.Max(schenkel.StartMeters, schenkel.EndMeters - MarkerProximityMeters)
                : schenkel.EndMeters;
            var dauer = SekundenZwischen(ErsterBei(schenkel.StartMeters), ErsterBei(ende));

            // Ein Punkt genau auf dem Winkel gehört noch zum Schenkel davor: Läuft der Hund über den
            // Winkel hinaus, bleibt sein Fortschritt dort stehen, die Abweichung wächst aber - sie
            // gehört auf den Schenkel, den er verfehlt hat.
            var abweichungen = Enumerable.Range(0, walk.Count)
                .Where(i => (k == 0 || fortschritt[i] > schenkel.StartMeters + StationEpsilon)
                            && fortschritt[i] <= schenkel.EndMeters + StationEpsilon)
                .Select(i => deviations[i])
                .ToList();

            legs.Add(new EvaluatedLeg(
                schenkel.Number,
                schenkel.LengthMeters,
                dauer,
                // Tempo über die tatsächlich gemessene Strecke (beim letzten Schenkel ohne den Spielraum).
                dauer is > 0 ? (ende - schenkel.StartMeters) / (dauer.Value / 60.0) : null,
                abweichungen.Count > 0 ? abweichungen.Average() : null));
        }

        var erreichtBis = fortschritt[^1];
        var corners = new List<EvaluatedCorner>(geometrie.Winkel.Count);
        for (var k = 0; k < geometrie.Winkel.Count; k++)
        {
            var winkel = geometrie.Winkel[k];
            var erreicht = erreichtBis >= winkel.StationMeters - StationEpsilon;

            double? ueberschiessen = null;
            var ueberschossen = false;
            if (erreicht)
            {
                var imFenster = Enumerable.Range(0, walk.Count)
                    .Where(i => fortschritt[i] >= winkel.StationMeters - UeberschiessenDavorMeters - StationEpsilon
                                && fortschritt[i] <= winkel.StationMeters + UeberschiessenDahinterMeters + StationEpsilon)
                    .Select(i => deviations[i])
                    .ToList();
                if (imFenster.Count > 0)
                {
                    ueberschiessen = imFenster.Max();
                    ueberschossen = ueberschiessen > FaehrteToleranz.AmberMaxMeters;
                }
            }

            var halt = stops
                .Where(s => Distance(Project(s.Latitude, s.Longitude, originLat), winkelEbene[k]) <= MarkerProximityMeters)
                .Sum(s => s.DurationSeconds);

            var zeitAmWinkel = SekundenZwischen(
                ErsterBei(winkel.StationMeters - ZeitAmWinkelHalbMeters),
                ErsterBei(winkel.StationMeters + ZeitAmWinkelHalbMeters));

            corners.Add(new EvaluatedCorner(
                winkel.Number,
                winkel.Direction,
                winkel.Sharp,
                winkel.TurnDegrees,
                winkel.Latitude,
                winkel.Longitude,
                erreicht,
                ueberschiessen,
                ueberschossen,
                halt,
                zeitAmWinkel));
        }

        return (legs, corners);
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

    /// <summary>
    /// Die Gegenstände (Marker der Art Gegenstand), denen der Hundeführer in einem Ablauf auf
    /// <see cref="MarkerProximityMeters"/> nahe kam - die Regel hinter <c>ArticlesFound</c>, einzeln
    /// herausgegeben für den GPS-Hinweis neben der Angabe der Hundeführer:in. Aus den Rohpunkten des
    /// Ablaufs, nicht aus der vereinfachten Anzeige-Linie: Auf einer langen Geraden bliebe sonst nur der
    /// Anfang und das Ende, und ein Gegenstand dazwischen gälte als nicht erreicht.
    /// null, wenn der Aufwand über <see cref="MaxAuswertungsAufwand"/> läge - wie bei der Auswertung selbst,
    /// damit das Lesen eines riesigen Ablaufs nicht teurer wird als dessen Auswertung.
    /// </summary>
    public static IReadOnlyList<Guid>? ErreichteGegenstaende(IEnumerable<GpsPoint> gelegt, IEnumerable<GpsWalkPoint> ablauf)
    {
        var punkte = gelegt.ToList();
        var ablaufPunkte = ablauf.ToList();
        if ((long)ablaufPunkte.Count * punkte.Count > MaxAuswertungsAufwand)
            return null;
        // Derselbe Bezugspunkt wie in TryEvaluate: der Anfang der gelegten Linie.
        var anfang = punkte.Where(p => p.PointType != GpsPointType.Manual).MinBy(p => p.Timestamp);
        return anfang is null ? [] : ErreichteGegenstaende(punkte, ablaufPunkte, anfang.Latitude);
    }

    private static List<Guid> ErreichteGegenstaende(IEnumerable<GpsPoint> marker, IEnumerable<GpsWalkPoint> ablauf, double originLat)
    {
        var ablaufEbene = ablauf.Select(w => Project(w.Latitude, w.Longitude, originLat)).ToList();
        return marker
            .Where(m => m.PointType == GpsPointType.Manual && m.MarkerType == GpsMarkerType.Article)
            .Where(m =>
            {
                var mp = Project(m.Latitude, m.Longitude, originLat);
                return ablaufEbene.Any(w => Distance(w, mp) <= MarkerProximityMeters);
            })
            .Select(m => m.Id)
            .ToList();
    }

    internal static (double X, double Y) Project(double latitude, double longitude, double originLat)
    {
        const double earthRadiusMeters = 6371000;
        var lat0Rad = originLat * Math.PI / 180;
        return (
            X: longitude * Math.PI / 180 * Math.Cos(lat0Rad) * earthRadiusMeters,
            Y: latitude * Math.PI / 180 * earthRadiusMeters);
    }

    internal static double Distance((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
