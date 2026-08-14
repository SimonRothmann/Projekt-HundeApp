using System.Net;
using System.Text;
using Dogity.Infrastructure.Geocoding;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Weather;

/// <summary>
/// Testet die Auswertung echter Photon-Antworten (Format am 2026-08-14 gegen
/// die Live-API verifiziert) ohne Netzzugriff. Der wichtigste Fall ist der
/// Hundeplatz: genau dafür wurde von einem reinen Ortsverzeichnis auf
/// OpenStreetMap-Daten gewechselt.
/// </summary>
public class PhotonGeocodingProviderTests
{
    /// <summary>Echte Antwort für "Hundesportverein Ettlingen".</summary>
    private const string ClubJson = """
    {"type":"FeatureCollection","features":[{"type":"Feature",
      "properties":{"osm_key":"building","osm_value":"retail","name":"Hundesportverein",
        "housenumber":"78","street":"Pforzheimer Straße","city":"Ettlingen",
        "county":"Landkreis Karlsruhe","state":"Baden-Württemberg","country":"Deutschland","postcode":"76275"},
      "geometry":{"type":"Point","coordinates":[8.4220939,48.9355571]}}]}
    """;

    private static PhotonGeocodingProvider Make(HttpStatusCode status, string body, Action<string>? onRequest = null)
        => new(new HttpClient(new StubHandler(status, body, onRequest)), NullLogger<PhotonGeocodingProvider>.Instance);

    [Fact]
    public async Task SearchAsync_Hundeplatz_ParsesNameAndAddress()
    {
        var provider = Make(HttpStatusCode.OK, ClubJson);

        var results = await provider.SearchAsync("Hundesportverein Ettlingen");

        var hit = Assert.Single(results);
        Assert.Equal("Hundesportverein", hit.Name);
        Assert.Equal("Pforzheimer Straße 78 · 76275 Ettlingen", hit.Detail);
        // GeoJSON liefert [Länge, Breite] - die Reihenfolge darf nicht kippen.
        Assert.Equal(48.9355571, hit.Latitude);
        Assert.Equal(8.4220939, hit.Longitude);
    }

    [Fact]
    public async Task SearchAsync_WithPosition_BiasesToSurroundings()
    {
        string? url = null;
        var provider = Make(HttpStatusCode.OK, ClubJson, u => url = u);

        await provider.SearchAsync("Hundeplatz", 48.94, 8.41);

        // "Hundeplatz" gibt es hundertfach - ohne Umkreis ist das Ergebnis Zufall.
        Assert.Contains("lat=48.94", url);
        Assert.Contains("lon=8.41", url);

        string? plain = null;
        await Make(HttpStatusCode.OK, ClubJson, u => plain = u).SearchAsync("Hundeplatz");
        Assert.DoesNotContain("lat=", plain);
    }

    /// <summary>Bei einer Stadt steht der Ortsname schon in der ersten Zeile.</summary>
    [Fact]
    public async Task SearchAsync_Town_DoesNotRepeatNameInDetail()
    {
        const string json = """
        {"features":[{"properties":{"osm_key":"place","osm_value":"town","name":"Ettlingen",
          "city":"Ettlingen","county":"Landkreis Karlsruhe","state":"Baden-Württemberg","postcode":"76275"},
          "geometry":{"coordinates":[8.4066,48.9403]}}]}
        """;

        var hit = Assert.Single(await Make(HttpStatusCode.OK, json).SearchAsync("Ettlingen"));

        Assert.Equal("Ettlingen", hit.Name);
        Assert.Equal("76275", hit.Detail);
    }

    /// <summary>Unbenannte Adresspunkte tragen den Straßennamen als Bezeichnung.</summary>
    [Fact]
    public async Task SearchAsync_UnnamedPlace_FallsBackToStreet()
    {
        const string json = """
        {"features":[{"properties":{"street":"Bahnhofstraße","city":"Ettlingen","postcode":"76275"},
          "geometry":{"coordinates":[8.4,48.9]}}]}
        """;

        var hit = Assert.Single(await Make(HttpStatusCode.OK, json).SearchAsync("Bahnhofstraße"));

        Assert.Equal("Bahnhofstraße", hit.Name);
        Assert.Equal("Bahnhofstraße · 76275 Ettlingen", hit.Detail);
    }

    /// <summary>
    /// OSM führt denselben Platz oft zweimal (Punkt und Fläche). Live kam
    /// "SV OG Pfinztal" so doppelt zurück, wenige Meter versetzt.
    /// </summary>
    [Fact]
    public async Task SearchAsync_SamePlaceTwiceInOsm_IsListedOnce()
    {
        const string json = """
        {"features":[
          {"properties":{"name":"SV OG Pfinztal","street":"Kapellenstraße","postcode":"76327","city":"Söllingen"},
           "geometry":{"coordinates":[8.5524,48.9773]}},
          {"properties":{"name":"SV OG Pfinztal","street":"Kapellenstraße","postcode":"76327","city":"Söllingen"},
           "geometry":{"coordinates":[8.5527,48.9776]}},
          {"properties":{"name":"SV OG Pfinztal","street":"Am Sportplatz","postcode":"75181","city":"Pforzheim"},
           "geometry":{"coordinates":[8.7000,48.8900]}}]}
        """;

        var results = await Make(HttpStatusCode.OK, json).SearchAsync("SV OG Pfinztal");

        // Die beiden Pfinztal-Einträge fallen zusammen, der echte zweite Ort bleibt.
        Assert.Equal(2, results.Count);
        Assert.Equal("Kapellenstraße · 76327 Söllingen", results[0].Detail);
        Assert.Equal("Am Sportplatz · 75181 Pforzheim", results[1].Detail);
    }

    [Fact]
    public async Task SearchAsync_BrokenResponses_ReturnEmptyInsteadOfThrowing()
    {
        // Eine fehlgeschlagene Suche darf die Eingabe nicht blockieren - der
        // Ort lässt sich auch von Hand eintragen.
        Assert.Empty(await Make(HttpStatusCode.ServiceUnavailable, "").SearchAsync("Hundeplatz"));
        Assert.Empty(await Make(HttpStatusCode.OK, "{}").SearchAsync("Hundeplatz"));
        Assert.Empty(await Make(HttpStatusCode.OK, "{\"features\":[]}").SearchAsync("Hundeplatz"));

        // Treffer ohne Koordinaten sind wertlos und werden übersprungen.
        Assert.Empty(await Make(HttpStatusCode.OK, "{\"features\":[{\"properties\":{\"name\":\"X\"}}]}").SearchAsync("X"));

        var broken = new PhotonGeocodingProvider(
            new HttpClient(new ThrowingHandler()), NullLogger<PhotonGeocodingProvider>.Instance);
        Assert.Empty(await broken.SearchAsync("Hundeplatz"));
    }

    [Fact]
    public async Task SearchAsync_BlankQuery_SkipsRequest()
    {
        var called = false;
        var provider = Make(HttpStatusCode.OK, "{}", _ => called = true);

        Assert.Empty(await provider.SearchAsync("   "));
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
