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

    // ---- Automatisch ermitteltes Wetter (siehe IWeatherProvider) ----
    // Zwei Zeitpunkte, weil bei der Fährte gerade die VERÄNDERUNG zwischen
    // Legen und Suchen zählt: sie bestimmt maßgeblich, wie sich die
    // Geruchsspur hält. Die vorhandenen Freitextfelder Weather/Wind bleiben
    // davon unberührt - das sind die eigenen Beobachtungen des Nutzers.
    public double? LaidTemperatureC { get; set; }
    public int? LaidRelativeHumidity { get; set; }
    public double? LaidWindSpeedKmh { get; set; }
    public int? LaidWeatherCode { get; set; }

    public double? SearchTemperatureC { get; set; }
    public int? SearchRelativeHumidity { get; set; }
    public double? SearchWindSpeedKmh { get; set; }
    public int? SearchWeatherCode { get; set; }

    public DateTimeOffset? WeatherFetchedAt { get; set; }

    /// <summary>
    /// Temperaturänderung zwischen Legen und Suchen in Kelvin/°C
    /// (positiv = wärmer geworden). Nur gesetzt, wenn beide Werte vorliegen.
    /// </summary>
    public double? TemperatureDeltaC =>
        LaidTemperatureC is { } laid && SearchTemperatureC is { } search ? Math.Round(search - laid, 1) : null;

    public ICollection<GpsPoint> Points { get; set; } = new List<GpsPoint>();

    /// <summary>
    /// Aufzeichnungen, bei denen die gelegte Fährte mit dem Hund abgelaufen
    /// wurde (siehe <see cref="GpsWalkRun"/>). Mehrere Abläufe pro gelegter
    /// Fährte sind möglich (z.B. Wiederholungsversuche).
    /// </summary>
    public ICollection<GpsWalkRun> WalkRuns { get; set; } = new List<GpsWalkRun>();
}
