using Dogity.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Ortssuche für die Trainings-Erfassung: erlaubt es, den Trainingsort per
/// Name zu setzen, statt Koordinaten eintippen zu müssen. Der eigentliche
/// Wetterabruf passiert serverseitig beim Speichern (siehe
/// WeatherEnrichmentService) - das Frontend braucht davon nichts zu wissen.
/// </summary>
[Route("api/weather")]
public class WeatherController(IGeocodingProvider geocoding) : ApiControllerBase
{
    /// <param name="lat">Optionale Position des Nutzers - gewichtet die Treffer auf die Umgebung.</param>
    /// <param name="lon">Siehe <paramref name="lat"/>.</param>
    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyList<GeocodeResult>>> SearchLocations(
        [FromQuery] string query,
        [FromQuery] double? lat,
        [FromQuery] double? lon,
        CancellationToken ct)
        => Ok(await geocoding.SearchAsync(query, lat, lon, ct));
}
