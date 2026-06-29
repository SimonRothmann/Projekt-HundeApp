using CanisTrack.Domain.Common;

namespace CanisTrack.Domain.Tracking;

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
}
