using CanisTrack.Domain.Common;

namespace CanisTrack.Domain.Tracking;

/// <summary>
/// Ein einzelner GPS-Punkt einer Fährte (siehe DATABASE.md "gps_points").
/// </summary>
public class GpsPoint : Entity
{
    public Guid TrackId { get; set; }
    public GpsTrack? Track { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double? Accuracy { get; set; }
}
