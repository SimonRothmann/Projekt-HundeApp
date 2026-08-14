using Dogity.Application.Weather;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// No-Op-Wetteranreicherung für Tests: die echte Implementierung ruft einen
/// externen Dienst (Open-Meteo) auf - Unit-Tests dürfen weder Netz brauchen
/// noch dadurch langsam/flaky werden. Das Verhalten "ohne Wetterdaten" ist
/// ohnehin der Normalfall, den der Produktivcode aushalten muss.
/// </summary>
public class FakeWeatherEnrichmentService : IWeatherEnrichmentService
{
    public Task EnrichTrackAsync(GpsTrack track, CancellationToken ct = default) => Task.CompletedTask;
    public Task EnrichSessionAsync(TrainingSession session, CancellationToken ct = default) => Task.CompletedTask;
}
