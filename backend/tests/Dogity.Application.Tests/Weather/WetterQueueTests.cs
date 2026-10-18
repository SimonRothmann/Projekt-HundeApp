using Dogity.Api.Hosting;
using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Weather;
using Dogity.Infrastructure.Persistence;
using Dogity.Infrastructure.Weather;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogity.Application.Tests.Weather;

/// <summary>
/// Die Warteschlange für Wetteraufträge und der Dienst, der sie abarbeitet:
/// begrenzte Größe, Reihenfolge, und ein Fehler bei einem Auftrag hält die
/// folgenden nicht auf.
/// </summary>
public class WetterQueueTests
{
    private static WetterAuftrag Auftrag(int minute) =>
        new(WetterZiel.Training, Guid.NewGuid(), 48.9, 8.4, new DateTimeOffset(2026, 10, 18, 9, minute, 0, TimeSpan.Zero));

    [Fact]
    public void Enqueue_NimmtBisZurKapazitaetAnUndLiefertDanachFalse_InReihenfolge()
    {
        var queue = new WetterQueue(NullLogger<WetterQueue>.Instance);
        var erster = Auftrag(0);

        Assert.True(queue.Enqueue(erster));
        for (var i = 1; i < 500; i++) Assert.True(queue.Enqueue(Auftrag(i % 60)));

        // Voll: der Auftrag wird verworfen, nichts wirft.
        Assert.False(queue.Enqueue(Auftrag(1)));

        Assert.True(queue.Reader.TryRead(out var gelesen));
        Assert.Equal(erster, gelesen);
        // Wieder Platz.
        Assert.True(queue.Enqueue(Auftrag(2)));
    }

    private sealed class ZaehlenderWetterdienst : IWeatherProvider
    {
        private int _aufrufe;
        public int Aufrufe => Volatile.Read(ref _aufrufe);

        public Task<WeatherReading?> GetAtAsync(double latitude, double longitude, DateTimeOffset instant, CancellationToken ct = default)
        {
            // Der erste Auftrag scheitert, der zweite nicht.
            if (Interlocked.Increment(ref _aufrufe) == 1) throw new HttpRequestException("Open-Meteo hängt.");
            return Task.FromResult<WeatherReading?>(null);
        }
    }

    [Fact]
    public async Task HintergrundDienst_ArbeitetBeideAuftraegeAb_AuchWennDerErsteScheitert()
    {
        var queue = new WetterQueue(NullLogger<WetterQueue>.Instance);
        var wetterdienst = new ZaehlenderWetterdienst();
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(_ => InMemoryDbContext.Create());
        services.AddSingleton<IWeatherProvider>(wetterdienst);
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddScoped<WetterNachtrag>();
        await using var provider = services.BuildServiceProvider();

        var dienst = new WetterNachtragBackgroundService(
            queue, provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<WetterNachtragBackgroundService>.Instance);

        queue.Enqueue(Auftrag(0));
        queue.Enqueue(Auftrag(1));
        await dienst.StartAsync(CancellationToken.None);
        try
        {
            var frist = DateTime.UtcNow.AddSeconds(10);
            while (wetterdienst.Aufrufe < 2 && DateTime.UtcNow < frist) await Task.Delay(20);
            Assert.Equal(2, wetterdienst.Aufrufe);
        }
        finally
        {
            await dienst.StopAsync(CancellationToken.None);
        }
    }
}
