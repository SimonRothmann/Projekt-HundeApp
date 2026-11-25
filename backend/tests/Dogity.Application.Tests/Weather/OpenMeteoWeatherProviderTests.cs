using System.Net;
using System.Text;
using Dogity.Infrastructure.Weather;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Weather;

/// <summary>
/// Testet die Auswertung echter Open-Meteo-Antworten (Format am 2026-08-14
/// gegen die Live-API verifiziert) ohne Netzzugriff: Stundenauswahl,
/// Parsen der Werte und das Verhalten bei Ausfällen.
/// </summary>
public class OpenMeteoWeatherProviderTests
{
    private const string HourlyJson = """
    {
      "latitude": 48.9, "longitude": 8.5, "timezone": "GMT",
      "hourly": {
        "time": ["2026-08-13T10:00","2026-08-13T11:00","2026-08-13T12:00"],
        "temperature_2m": [17.4, 19.1, 21.8],
        "relative_humidity_2m": [61, 55, 48],
        "wind_speed_10m": [6.5, 8.2, 9.9],
        "wind_direction_10m": [270, 245.4, 359.6],
        "weather_code": [0, 1, 3]
      }
    }
    """;

    private static OpenMeteoWeatherProvider Make(HttpStatusCode status, string body, Action<string>? onRequest = null)
    {
        var handler = new StubHandler(status, body, onRequest);
        return new OpenMeteoWeatherProvider(new HttpClient(handler), NullLogger<OpenMeteoWeatherProvider>.Instance);
    }

    private const string PeriodJson = """
    {
      "hourly": {
        "time": ["2026-08-13T09:00","2026-08-13T10:00","2026-08-13T11:00","2026-08-13T12:00","2026-08-13T13:00"],
        "temperature_2m": [2.0, 4.5, 3.0, 9.0, 12.0],
        "precipitation": [1.0, 0.0, 1.2, 3.0, 5.0]
      }
    }
    """;

    [Fact]
    public async Task GetPeriodAsync_SumsRainAndTakesMinMax_OnlyForHoursInTheWindow()
    {
        string? url = null;
        var provider = Make(HttpStatusCode.OK, PeriodJson, u => url = u);

        // 10:20 bis 12:40. Temperatur: Stundenwerte 10, 11, 12 (die Stunde des Starts zählt mit, 13:00 liegt
        // nach dem Ende). Regen gilt für die Stunde vor dem Zeitstempel: 11:00 (10-11), 12:00 (11-12) und
        // 13:00 (12-13, überschneidet das Ende) zählen, 10:00 (9-10, vor dem Start) nicht.
        var period = await provider.GetPeriodAsync(48.9, 8.5,
            new DateTimeOffset(2026, 8, 13, 10, 20, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 13, 12, 40, 0, TimeSpan.Zero));

        Assert.Contains("precipitation", url);
        Assert.NotNull(period);
        Assert.Equal(9.2, period!.RainMm);
        Assert.Equal(3.0, period.TemperatureMinC);
        Assert.Equal(9.0, period.TemperatureMaxC);
    }

    [Fact]
    public async Task GetPeriodAsync_WithoutHoursInWindow_OnError_OrOldDate_BehavesLikeGetAt()
    {
        var leer = await Make(HttpStatusCode.OK, PeriodJson).GetPeriodAsync(48.9, 8.5,
            new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 14, 5, 0, 0, TimeSpan.Zero));
        Assert.Null(leer);

        var fehler = await Make(HttpStatusCode.InternalServerError, "").GetPeriodAsync(48.9, 8.5,
            DateTimeOffset.UtcNow.AddHours(-5), DateTimeOffset.UtcNow);
        Assert.Null(fehler);

        string? alt = null;
        await Make(HttpStatusCode.OK, PeriodJson, u => alt = u).GetPeriodAsync(48.9, 8.5,
            DateTimeOffset.UtcNow.AddDays(-200), DateTimeOffset.UtcNow.AddDays(-199));
        Assert.Contains("archive-api", alt);
    }

    [Fact]
    public async Task GetAtAsync_PicksNearestHour_AndParsesAllValues()
    {
        var provider = Make(HttpStatusCode.OK, HourlyJson);

        // 11:20 UTC -> nächstgelegener Stundenwert ist 11:00.
        var reading = await provider.GetAtAsync(48.9, 8.5, new DateTimeOffset(2026, 8, 13, 11, 20, 0, TimeSpan.Zero));

        Assert.NotNull(reading);
        Assert.Equal(19.1, reading!.TemperatureC);
        Assert.Equal(55, reading.RelativeHumidity);
        Assert.Equal(8.2, reading.WindSpeedKmh);
        Assert.Equal(1, reading.WeatherCode);
        Assert.Equal(245, reading.WindDirectionDeg);
    }

    [Fact]
    public async Task GetAtAsync_RequestsWindDirection_AndWrapsFullCircleToZero()
    {
        string? url = null;
        var provider = Make(HttpStatusCode.OK, HourlyJson, u => url = u);

        // 12:00 UTC -> 359,6 Grad rundet auf 360, und das ist dieselbe Richtung wie 0.
        var reading = await provider.GetAtAsync(48.9, 8.5, new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("wind_direction_10m", url);
        Assert.Equal(0, reading!.WindDirectionDeg);
    }

    [Fact]
    public async Task GetAtAsync_WithoutWindDirectionInResponse_LeavesItEmpty()
    {
        var ohneRichtung = HourlyJson.Replace("\"wind_direction_10m\": [270, 245.4, 359.6],", "");
        var provider = Make(HttpStatusCode.OK, ohneRichtung);

        var reading = await provider.GetAtAsync(48.9, 8.5, new DateTimeOffset(2026, 8, 13, 11, 0, 0, TimeSpan.Zero));

        Assert.NotNull(reading);
        Assert.Null(reading!.WindDirectionDeg);
        Assert.Equal(8.2, reading.WindSpeedKmh);
    }

    [Fact]
    public async Task GetAtAsync_RecentDate_UsesForecastApi_OldDate_UsesArchive()
    {
        string? recentUrl = null;
        var recent = Make(HttpStatusCode.OK, HourlyJson, url => recentUrl = url);
        await recent.GetAtAsync(48.9, 8.5, DateTimeOffset.UtcNow.AddDays(-2));
        Assert.Contains("api.open-meteo.com/v1/forecast", recentUrl);
        Assert.Contains("past_days=", recentUrl);

        string? oldUrl = null;
        var old = Make(HttpStatusCode.OK, HourlyJson, url => oldUrl = url);
        await old.GetAtAsync(48.9, 8.5, DateTimeOffset.UtcNow.AddDays(-400));
        Assert.Contains("archive-api.open-meteo.com/v1/archive", oldUrl);
    }

    [Fact]
    public async Task GetAtAsync_FarFromAnyHour_ReturnsNull()
    {
        var provider = Make(HttpStatusCode.OK, HourlyJson);

        // Über 3 Stunden entfernt von jedem gelieferten Wert.
        var reading = await provider.GetAtAsync(48.9, 8.5, new DateTimeOffset(2026, 8, 13, 20, 0, 0, TimeSpan.Zero));

        Assert.Null(reading);
    }

    /// <summary>
    /// Wetter ist eine Anreicherung: fällt der Dienst aus, darf das NICHT
    /// dazu führen, dass ein Training oder eine Fährte nicht gespeichert wird.
    /// </summary>
    [Fact]
    public async Task GetAtAsync_ServiceUnavailable_ReturnsNullInsteadOfThrowing()
    {
        var provider = Make(HttpStatusCode.ServiceUnavailable, "");
        Assert.Null(await provider.GetAtAsync(48.9, 8.5, DateTimeOffset.UtcNow));

        var broken = new OpenMeteoWeatherProvider(
            new HttpClient(new ThrowingHandler()), NullLogger<OpenMeteoWeatherProvider>.Instance);
        Assert.Null(await broken.GetAtAsync(48.9, 8.5, DateTimeOffset.UtcNow));
    }

    private sealed class StubHandler(HttpStatusCode status, string body, Action<string>? onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest?.Invoke(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Netzwerk nicht erreichbar");
    }
}
