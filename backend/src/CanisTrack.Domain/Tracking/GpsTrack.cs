using CanisTrack.Domain.Common;

namespace CanisTrack.Domain.Tracking;

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
}
