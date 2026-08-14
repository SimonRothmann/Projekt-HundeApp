using System.Globalization;
using System.Text.Json;
using Dogity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Weather;

/// <summary>
/// Wetteranbindung über Open-Meteo: kostenlos, ohne API-Key, ohne Registrierung
/// (siehe COST STRATEGY.md "Start mit 0-10€ monatlich"). Zwei Endpunkte:
///
/// - Forecast-API mit <c>past_days</c> (max. 92) für alles der letzten ~3 Monate:
///   verzögerungsfrei, deckt also auch das Training von heute Morgen ab.
/// - Archive-API (ERA5) für alles Ältere - reicht bis 1940 zurück, hat aber
///   rund 5 Tage Rückstand; deshalb NICHT für frische Daten.
///
/// Fehler werden geschluckt (null zurück) und protokolliert: Wetter ist eine
/// Anreicherung. Ein Ausfall des Wetterdiensts darf niemals verhindern, dass
/// ein Training oder eine Fährte gespeichert wird.
/// </summary>
public class OpenMeteoWeatherProvider(HttpClient http, ILogger<OpenMeteoWeatherProvider> logger) : IWeatherProvider
{
    // Ab dieser Grenze ist die Forecast-API (past_days) nicht mehr zuständig.
    private const int ForecastPastDaysLimit = 90;

    private const string HourlyVariables = "temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code";

    public async Task<WeatherReading?> GetAtAsync(double latitude, double longitude, DateTimeOffset instant, CancellationToken ct = default)
    {
        var utc = instant.ToUniversalTime();
        var date = DateOnly.FromDateTime(utc.UtcDateTime);
        var ageDays = (DateTime.UtcNow.Date - utc.UtcDateTime.Date).TotalDays;

        // Alles in UTC anfragen und vergleichen - erspart Zeitzonen-Fallstricke
        // (Open-Meteo liefert ohne timezone-Parameter UTC-Zeitstempel).
        var url = ageDays <= ForecastPastDaysLimit
            ? $"https://api.open-meteo.com/v1/forecast?latitude={Fmt(latitude)}&longitude={Fmt(longitude)}&hourly={HourlyVariables}&past_days={Math.Max(1, (int)Math.Ceiling(ageDays))}&forecast_days=1&timezone=UTC"
            : $"https://archive-api.open-meteo.com/v1/archive?latitude={Fmt(latitude)}&longitude={Fmt(longitude)}&hourly={HourlyVariables}&start_date={date:yyyy-MM-dd}&end_date={date:yyyy-MM-dd}&timezone=UTC";

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Wetterabruf fehlgeschlagen: {Status}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("hourly", out var hourly) ||
                !hourly.TryGetProperty("time", out var times))
                return null;

            var index = NearestHourIndex(times, utc);
            if (index < 0) return null;

            var temperature = ReadDouble(hourly, "temperature_2m", index);
            if (temperature is null) return null; // ohne Temperatur ist der Datensatz wertlos

            return new WeatherReading(
                Math.Round(temperature.Value, 1),
                (int?)ReadDouble(hourly, "relative_humidity_2m", index),
                ReadDouble(hourly, "wind_speed_10m", index) is { } wind ? Math.Round(wind, 1) : null,
                (int?)ReadDouble(hourly, "weather_code", index));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Bewusst nicht weiterwerfen - siehe Klassenkommentar.
            logger.LogWarning(ex, "Wetterabruf nicht möglich");
            return null;
        }
    }

    public async Task<IReadOnlyList<GeocodeResult>> SearchLocationAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=5&language=de&format=json";
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // "results" fehlt komplett, wenn nichts gefunden wurde.
            if (!doc.RootElement.TryGetProperty("results", out var results)) return [];

            var list = new List<GeocodeResult>();
            foreach (var item in results.EnumerateArray())
            {
                if (!item.TryGetProperty("latitude", out var lat) || !item.TryGetProperty("longitude", out var lon))
                    continue;

                list.Add(new GeocodeResult(
                    item.TryGetProperty("name", out var name) ? name.GetString() ?? query : query,
                    item.TryGetProperty("admin1", out var admin) ? admin.GetString() : null,
                    item.TryGetProperty("country", out var country) ? country.GetString() : null,
                    lat.GetDouble(),
                    lon.GetDouble()));
            }

            return list;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Ortssuche nicht möglich");
            return [];
        }
    }

    /// <summary>Index des Stundenwerts, der dem gesuchten Zeitpunkt am nächsten liegt.</summary>
    private static int NearestHourIndex(JsonElement times, DateTimeOffset utc)
    {
        var best = -1;
        var bestDistance = TimeSpan.MaxValue;
        var i = 0;
        foreach (var entry in times.EnumerateArray())
        {
            // Format "2026-08-13T14:00" (ohne Zonenangabe, da timezone=UTC).
            if (DateTime.TryParse(entry.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                var distance = (parsed - utc.UtcDateTime).Duration();
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            i++;
        }

        // Mehr als 3 Stunden daneben ist kein sinnvoller Messwert mehr.
        return bestDistance <= TimeSpan.FromHours(3) ? best : -1;
    }

    private static double? ReadDouble(JsonElement hourly, string property, int index)
    {
        if (!hourly.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return null;
        if (index >= array.GetArrayLength()) return null;
        var value = array[index];
        return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }

    private static string Fmt(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
