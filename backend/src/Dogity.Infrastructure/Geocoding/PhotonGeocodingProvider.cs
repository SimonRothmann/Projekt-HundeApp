using System.Globalization;
using System.Text.Json;
using Dogity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Geocoding;

/// <summary>
/// Ortssuche über Photon (komoot) auf OpenStreetMap-Daten: kostenlos, ohne
/// API-Key, ohne Registrierung.
///
/// Warum Photon und nicht Nominatim, obwohl beide dieselben OSM-Daten nutzen:
/// Nominatims Nutzungsbedingungen untersagen Tipp-Suche ausdrücklich (max. eine
/// Anfrage pro Sekunde, kein Autocomplete). Photon ist genau dafür gebaut - es
/// findet auch bei angefangenen Wörtern etwas und kann Treffer auf die Umgebung
/// gewichten.
///
/// Entscheidend für diese App: OSM kennt Hundeplätze als eigene Kategorie
/// (<c>amenity=animal_training</c>, oft auch <c>leisure=pitch</c> mit dem Namen
/// "Hundeplatz"), Vereinsheime und Adressen. Ein Ortsverzeichnis kann das nicht.
/// </summary>
public class PhotonGeocodingProvider(HttpClient http, ILogger<PhotonGeocodingProvider> logger) : IGeocodingProvider
{
    private const int ResultLimit = 6;

    /// <summary>
    /// Gleichnamige Treffer innerhalb dieses Umkreises gelten als derselbe Ort.
    /// Zwei wirklich verschiedene Hundeplätze gleichen Namens in 150 m Abstand
    /// gibt es nicht.
    /// </summary>
    private const double DuplicateRadiusMeters = 150;

    public async Task<IReadOnlyList<GeocodeResult>> SearchAsync(
        string query, double? nearLatitude = null, double? nearLongitude = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var url = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(query.Trim())}&limit={ResultLimit}&lang=de";
        if (nearLatitude is { } lat && nearLongitude is { } lon)
            url += $"&lat={Fmt(lat)}&lon={Fmt(lon)}";

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ortssuche fehlgeschlagen: {Status}", response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
                return [];

            // Entdoppeln: OSM führt denselben Platz oft zweimal, als Punkt und
            // als Fläche ("SV OG Pfinztal" kam live doppelt zurück, 40 m
            // versetzt). Gerundete Koordinaten taugen dafür NICHT - zwei nah
            // beieinander liegende Werte können auf verschiedene Seiten einer
            // Rundungsgrenze fallen. Deshalb echter Abstand: gleicher Name und
            // näher als DuplicateRadiusMeters heißt derselbe Ort.
            var results = new List<GeocodeResult>();
            foreach (var feature in features.EnumerateArray())
            {
                if (ToResult(feature) is not { } hit) continue;
                if (results.Any(kept => IsSamePlace(kept, hit))) continue;
                results.Add(hit);
            }

            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Siehe IGeocodingProvider: leere Liste statt Ausnahme.
            logger.LogWarning(ex, "Ortssuche nicht möglich");
            return [];
        }
    }

    public async Task<GeocodeResult?> ReverseAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var url = $"https://photon.komoot.io/reverse?lat={Fmt(latitude)}&lon={Fmt(longitude)}&lang=de&limit=1";

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("features", out var features) ||
                features.ValueKind != JsonValueKind.Array ||
                features.GetArrayLength() == 0)
                return null;

            var feature = features[0];
            var props = feature.TryGetProperty("properties", out var p) ? p : default;

            // Die eigenen Koordinaten behalten - der Treffer ist nur die
            // Beschriftung, nicht der Ort. Photon liefert den Mittelpunkt des
            // nächsten Objekts, der etliche Meter danebenliegen kann.
            var name = ReverseName(props);
            return name is null ? null : new GeocodeResult(name, BuildDetail(props, name), latitude, longitude);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Rückwärtssuche nicht möglich");
            return null;
        }
    }

    /// <summary>
    /// Bezeichnung für einen Punkt, den jemand gerade betritt.
    ///
    /// Der Name des nächstgelegenen Objekts wird NUR übernommen, wenn es ein
    /// Gelände ist (Sportplatz, Hundeplatz, Wiese, Park). Bei Gebäuden und
    /// Hausadressen wäre er eine Falschaussage: wer neben einer Schule auf der
    /// Wiese trainiert, bekäme sonst den Schulnamen als Trainingsort - live
    /// genau so passiert ("Haus Frühling"). Dann lieber Straße und Ort, das
    /// stimmt und lässt sich von Hand schärfen.
    /// </summary>
    private static string? ReverseName(JsonElement props)
    {
        var key = Str(props, "osm_key");
        var isTerrain = key is "leisure" or "landuse" or "sport" or "natural" or "tourism"
            || (key == "amenity" && Str(props, "osm_value") == "animal_training");

        if (isTerrain && Str(props, "name") is { } name) return name;

        var street = Str(props, "street");
        var city = Str(props, "city") ?? Str(props, "county");

        if (street is not null && city is not null) return $"{street}, {city}";
        return street ?? city ?? Str(props, "name");
    }

    /// <summary>
    /// Gleicher Name und dicht beieinander. Der Abstand wird flach gerechnet
    /// (Äquirektangular-Näherung) - auf wenigen hundert Metern ist die
    /// Abweichung zur Kugelformel bedeutungslos.
    /// </summary>
    private static bool IsSamePlace(GeocodeResult a, GeocodeResult b)
    {
        if (!string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)) return false;

        const double metersPerDegree = 111_320;
        var dy = (a.Latitude - b.Latitude) * metersPerDegree;
        var dx = (a.Longitude - b.Longitude) * metersPerDegree * Math.Cos(a.Latitude * Math.PI / 180);

        return Math.Sqrt(dx * dx + dy * dy) < DuplicateRadiusMeters;
    }

    private static GeocodeResult? ToResult(JsonElement feature)
    {
        if (!feature.TryGetProperty("geometry", out var geometry) ||
            !geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.GetArrayLength() < 2)
            return null;

        // GeoJSON ist [Länge, Breite] - genau andersherum als überall sonst.
        var longitude = coordinates[0].GetDouble();
        var latitude = coordinates[1].GetDouble();

        var props = feature.TryGetProperty("properties", out var p) ? p : default;

        // Unbenannte Treffer (reine Adresspunkte) tragen den Straßennamen.
        var name = Str(props, "name") ?? Str(props, "street") ?? Str(props, "city");
        if (name is null) return null;

        return new GeocodeResult(name, BuildDetail(props, name), latitude, longitude);
    }

    /// <summary>
    /// Zweite Zeile: Straße/Hausnummer, PLZ und Ort - so viel, dass zwei
    /// gleichnamige Hundeplätze unterscheidbar sind, aber ohne die volle
    /// Verwaltungskette bis zum Bundesland.
    /// </summary>
    private static string? BuildDetail(JsonElement props, string name)
    {
        var street = Str(props, "street");
        var houseNumber = Str(props, "housenumber");
        var postcode = Str(props, "postcode");
        var city = Str(props, "city") ?? Str(props, "county");

        var parts = new List<string>();
        if (street is not null)
            parts.Add(houseNumber is null ? street : $"{street} {houseNumber}");

        // Bei Städten selbst steht der Ortsname schon in der ersten Zeile.
        var place = string.Join(' ', new[] { postcode, city }.Where(x => x is not null));
        if (place.Length > 0 && !string.Equals(city, name, StringComparison.OrdinalIgnoreCase))
            parts.Add(place);
        else if (postcode is not null && parts.Count == 0)
            parts.Add(postcode);

        var detail = string.Join(" · ", parts);
        return detail.Length > 0 ? detail : Str(props, "state");
    }

    private static string? Str(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static string Fmt(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
