using Dogity.Domain.Common;

namespace Dogity.Domain.Tracking;

/// <summary>
/// Eine Aufzeichnung, bei der eine bereits gelegte Fährte (<see cref="GpsTrack"/>)
/// mit dem Hund abgelaufen wurde - separat von der ursprünglichen
/// Legeaufzeichnung, damit beide Linien zum Vergleich nebeneinander
/// dargestellt werden können. Ein GpsTrack kann mehrere Abläufe haben
/// (z.B. Wiederholungsversuche).
/// </summary>
public class GpsWalkRun : Entity
{
    public Guid TrackId { get; set; }
    public GpsTrack? Track { get; set; }

    public double? LengthMeters { get; set; }
    public string? Comment { get; set; }

    // ---- Auswertung (siehe GpsTrackEvaluator) ----
    // Persistiert, damit Trend-Auswertungen nicht sämtliche GPS-Punkte laden
    // müssen. Null = noch nicht ausgewertet (Altbestand vor dem Backfill).

    /// <summary>
    /// Mittlere Abweichung der HUNDEFÜHRER-Linie von der gelegten Fährte in
    /// Metern. Bewusst nicht "Abweichung des Hundes": das Gerät läuft am
    /// Hundeführer, der Hund kann im Radius der Fährtenleine ausscheren, ohne
    /// dass sich das hier zeigt (siehe Stockungen als Ergänzung).
    /// </summary>
    public double? AvgDeviationMeters { get; set; }
    public double? MaxDeviationMeters { get; set; }

    /// <summary>Anteil der Ablaufpunkte innerhalb der "auf Fährte"-Schwelle (0-100).</summary>
    public double? OnTrackPercent { get; set; }

    public int? ArticlesFound { get; set; }
    public int? ArticlesTotal { get; set; }

    public DateTimeOffset? EvaluatedAt { get; set; }

    public ICollection<GpsWalkPoint> Points { get; set; } = new List<GpsWalkPoint>();

    public ICollection<GpsWalkStop> Stops { get; set; } = new List<GpsWalkStop>();
}

/// <summary>
/// Fachliche Einordnung eines erkannten Halts während des Ablaufs.
/// </summary>
public enum WalkStopKind
{
    /// <summary>Halt fernab jedes Markers - das eigentliche Warnsignal.</summary>
    Unexplained,
    /// <summary>Halt an einem Gegenstand = Verweisen (erwünscht).</summary>
    Indication,
    /// <summary>Halt an Leckerlipot/Verleitung - erklärt und neutral.</summary>
    Explained
}

/// <summary>
/// Ein erkannter Halt während eines Ablaufs: Der Hundeführer bleibt stehen,
/// weil der Hund verweist, frisst oder sucht. Fängt genau das ab, was die
/// reine Positionsabweichung nicht sieht (Hund schert im Leinenradius aus und
/// kommt zurück, ohne dass sich das Gerät bewegt).
/// </summary>
public class GpsWalkStop : Entity
{
    public Guid WalkRunId { get; set; }
    public GpsWalkRun? WalkRun { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int DurationSeconds { get; set; }
    public WalkStopKind Kind { get; set; }

    /// <summary>Label des erklärenden Markers, falls vorhanden (für die Anzeige).</summary>
    public string? MarkerLabel { get; set; }
}

/// <summary>
/// Ein einzelner GPS-Punkt eines Ablauf-Versuchs (siehe <see cref="GpsWalkRun"/>).
/// </summary>
public class GpsWalkPoint : Entity, IGeoPoint
{
    public Guid WalkRunId { get; set; }
    public GpsWalkRun? WalkRun { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double? Accuracy { get; set; }

    /// <summary>
    /// Senkrechter Abstand dieses Punkts zur Linie der gelegten Fährte in
    /// Metern (siehe GpsTrackEvaluator). Persistiert, damit die Karte die
    /// Ablauf-Linie ohne Neuberechnung abschnittsweise einfärben kann.
    /// Null = noch nicht ausgewertet.
    /// </summary>
    public double? DeviationMeters { get; set; }
}
