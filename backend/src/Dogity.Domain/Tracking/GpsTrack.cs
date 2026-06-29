using Dogity.Domain.Common;

namespace Dogity.Domain.Tracking;

/// <summary>
/// Eine aufgezeichnete Fährte (siehe DATABASE.md "Fährtenmodell").
/// Mehrere Fährten pro Training sind möglich (siehe PRODUCT_REQUIREMENTS.md
/// "Fährte: Mehrere Fährten pro Training").
/// </summary>
public class GpsTrack : Entity
{
    public Guid TrainingSessionId { get; set; }

    public double? LengthMeters { get; set; }
    public int? AgeMinutes { get; set; }
    public string? Surface { get; set; }
    public string? Weather { get; set; }
    public string? Wind { get; set; }
    public string? Comment { get; set; }

    public ICollection<GpsPoint> Points { get; set; } = new List<GpsPoint>();

    /// <summary>
    /// Aufzeichnungen, bei denen die gelegte Fährte mit dem Hund abgelaufen
    /// wurde (siehe <see cref="GpsWalkRun"/>). Mehrere Abläufe pro gelegter
    /// Fährte sind möglich (z.B. Wiederholungsversuche).
    /// </summary>
    public ICollection<GpsWalkRun> WalkRuns { get; set; } = new List<GpsWalkRun>();
}
