using Dogity.Domain.Common;

namespace Dogity.Domain.Tracking;

/// <summary>In welche Richtung eine Fährte an einem Winkel abbiegt (in Laufrichtung gesehen).</summary>
public enum CornerDirection
{
    Left,
    Right
}

/// <summary>
/// Ein Schenkel der gelegten Fährte - der Abschnitt zwischen Start, Winkeln und Ende - mit dem,
/// was ein Ablauf darauf gemacht hat (siehe GpsTrackEvaluator). Wie die Halte ein reines
/// Rechenergebnis, bei jeder Neuauswertung ersetzt. Nur bei der Fährte.
/// </summary>
public class GpsWalkRunLeg : Entity
{
    public Guid WalkRunId { get; set; }
    public GpsWalkRun? WalkRun { get; set; }

    /// <summary>Laufende Nummer ab 1, in Laufrichtung.</summary>
    public int Number { get; set; }

    /// <summary>Länge entlang der vereinfachten Linie in Metern.</summary>
    public double LengthMeters { get; set; }

    /// <summary>Zeit für den Schenkel; null, wenn sein Ende nicht erreicht wurde.</summary>
    public int? DurationSeconds { get; set; }

    public double? SpeedMetersPerMinute { get; set; }

    /// <summary>Ø Abweichung der Ablaufpunkte auf diesem Schenkel; null ohne Punkte darauf.</summary>
    public double? AvgDeviationMeters { get; set; }
}

/// <summary>
/// Ein Winkel der gelegten Fährte (Richtungsänderung ab 45 Grad) und wie der Ablauf damit
/// umgegangen ist. Wie <see cref="GpsWalkRunLeg"/> ein Rechenergebnis; Lage und Maße des Winkels
/// stehen je Ablauf dabei, damit die Anzeige keine zweite Quelle braucht. Nur bei der Fährte.
/// </summary>
public class GpsWalkRunCorner : Entity
{
    public Guid WalkRunId { get; set; }
    public GpsWalkRun? WalkRun { get; set; }

    /// <summary>Laufende Nummer ab 1, in Laufrichtung.</summary>
    public int Number { get; set; }

    public CornerDirection Direction { get; set; }

    /// <summary>Spitz: Richtungsänderung über 105 Grad (Innenwinkel unter 75 Grad).</summary>
    public bool Sharp { get; set; }

    /// <summary>Betrag der Richtungsänderung in Grad (die Richtung steht in <see cref="Direction"/>).</summary>
    public double TurnDegrees { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Hat der Ablauf den Winkel erreicht?</summary>
    public bool Reached { get; set; }

    /// <summary>Größte Abweichung kurz vor bis nach dem Winkel; null, wenn nicht erreicht oder kein Punkt dort.</summary>
    public double? OvershootMeters { get; set; }

    /// <summary>Über der gelben Grenze der Fährte hinausgeschossen.</summary>
    public bool Overshot { get; set; }

    /// <summary>Summe der Halte im Umkreis des Winkels in Sekunden.</summary>
    public int HoldSeconds { get; set; }

    /// <summary>Zeit zwischen 10 m vor und 10 m hinter dem Winkel; null, wenn nicht beides erreicht wurde.</summary>
    public int? TimeAtCornerSeconds { get; set; }
}
