using Dogity.Domain.Common;

namespace Dogity.Domain.Tracking;

/// <summary>
/// Unterscheidet automatisch per GPS-Ortung aufgezeichnete Punkte von
/// Punkten, die der Hundeführer während der Aufnahme manuell für einen
/// gelegten Gegenstand (Schussstelle, Apportel etc.) gesetzt hat.
/// </summary>
public enum GpsPointType
{
    Automatic,
    Manual
}

/// <summary>
/// Fachliche Bedeutung eines manuell gesetzten Markers. Entscheidend für die
/// Auswertung: ein Halt am Gegenstand ist ein <em>erwünschtes</em> Verweisen,
/// ein Halt am Leckerlipot/an einer Verleitung ist erklärt und neutral - nur
/// ein Halt fernab jedes Markers ist ein Warnsignal (siehe GpsTrackEvaluator).
/// Bestandsdaten sind ausnahmslos Gegenstände (die UI kannte bisher nur
/// "Gegenstand markieren"), daher ist <see cref="Article"/> der Default-Wert 0.
/// </summary>
public enum GpsMarkerType
{
    Article,
    TreatPot,
    Distraction,
    Other
}

/// <summary>
/// Ein einzelner GPS-Punkt einer Fährte (siehe DATABASE.md "gps_points").
/// </summary>
public class GpsPoint : Entity, IGeoPoint
{
    public Guid TrackId { get; set; }
    public GpsTrack? Track { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double? Accuracy { get; set; }
    public GpsPointType PointType { get; set; } = GpsPointType.Automatic;
    public string? Label { get; set; }

    /// <summary>
    /// Nur für manuelle Marker relevant (siehe <see cref="GpsMarkerType"/>);
    /// bei automatischen Punkten bedeutungslos.
    /// </summary>
    public GpsMarkerType MarkerType { get; set; } = GpsMarkerType.Article;
}
