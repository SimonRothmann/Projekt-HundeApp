using Dogity.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

/// <summary>
/// Ortssuche für die Trainings-Erfassung: erlaubt es, den Trainingsort per
/// Name/PLZ zu setzen, statt Koordinaten eintippen zu müssen. Der eigentliche
/// Wetterabruf passiert serverseitig beim Speichern (siehe
/// WeatherEnrichmentService) - das Frontend braucht davon nichts zu wissen.
/// </summary>
[Route("api/weather")]
public class WeatherController(IWeatherProvider weather) : ApiControllerBase
{
    [HttpGet("locations")]
    public async Task<ActionResult<IReadOnlyList<GeocodeResult>>> SearchLocations([FromQuery] string query, CancellationToken ct)
        => Ok(await weather.SearchLocationAsync(query, ct));
}
