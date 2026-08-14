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
        "weather_code": [0, 1, 3]
      }
    }
    """;

    private static OpenMeteoWeatherProvider Make(HttpStatusCode status, string body, Action<string>? onRequest = null)
    {
        var handler = new StubHandler(status, body, onRequest);
        return new OpenMeteoWeatherProvider(new HttpClient(handler), NullLogger<OpenMeteoWeatherProvider>.Instance);
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
        Assert.Empty(await broken.SearchLocationAsync("Karlsruhe"));
    }

    [Fact]
    public async Task SearchLocationAsync_ParsesResults_AndHandlesNoMatch()
    {
        const string json = """
        {"results":[{"name":"Karlsruhe","latitude":49.00937,"longitude":8.40444,"country":"Deutschland","admin1":"Baden-Württemberg"}]}
        """;
        var provider = Make(HttpStatusCode.OK, json);

        var results = await provider.SearchLocationAsync("Karlsruhe");

        var hit = Assert.Single(results);
        Assert.Equal("Karlsruhe", hit.Name);
        Assert.Equal("Baden-Württemberg", hit.Region);
        Assert.Equal(49.00937, hit.Latitude);

        // Ohne Treffer fehlt "results" komplett - darf nicht knallen.
        var empty = Make(HttpStatusCode.OK, "{\"generationtime_ms\":0.1}");
        Assert.Empty(await empty.SearchLocationAsync("gibtesnicht"));
    }

    [Fact]
    public async Task SearchLocationAsync_BlankQuery_SkipsRequest()
    {
        var called = false;
        var provider = Make(HttpStatusCode.OK, "{}", _ => called = true);

        Assert.Empty(await provider.SearchLocationAsync("   "));
        Assert.False(called);
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
