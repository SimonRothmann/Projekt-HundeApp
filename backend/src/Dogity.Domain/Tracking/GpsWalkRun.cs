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

    public ICollection<GpsWalkPoint> Points { get; set; } = new List<GpsWalkPoint>();
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
}
