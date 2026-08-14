namespace Dogity.Application.Abstractions;

/// <summary>Wetterwerte zu einem konkreten Zeitpunkt an einem Ort.</summary>
public record WeatherReading(
    double TemperatureC,
    int? RelativeHumidity,
    double? WindSpeedKmh,
    /// <summary>WMO-Wettercode (siehe Open-Meteo weather_code).</summary>
    int? WeatherCode);

/// <summary>
/// Liefert Wetterdaten zu Ort und Zeitpunkt - für die Fährte der springende
/// Punkt: die Temperaturänderung zwischen Legen und Suchen bestimmt
/// maßgeblich, wie sich die Geruchsspur hält.
///
/// Bewusst als Abstraktion in der Application-Schicht: die konkrete Anbindung
/// (Open-Meteo, kostenlos und ohne API-Key) liegt in der Infrastructure.
/// Implementierungen geben bei Nichterreichbarkeit <c>null</c> zurück statt zu
/// werfen - Wetter ist eine Anreicherung, kein Grund, das Speichern eines
/// Trainings scheitern zu lassen.
/// </summary>
public interface IWeatherProvider
{
    Task<WeatherReading?> GetAtAsync(double latitude, double longitude, DateTimeOffset instant, CancellationToken ct = default);
}
