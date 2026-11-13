using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;

namespace Dogity.Application.Tests.Preferences;

/// <summary>
/// Schrittlänge für die Umrechnung der Fährtenlänge in Schritte (Paket F4): ohne Zeile der Standard,
/// erlaubt sind 40 bis 120 cm, null stellt den Standard wieder her.
/// </summary>
public class SchrittlaengeTests
{
    private static PreferenceService Service(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new PreferenceService(db);
    }

    [Fact]
    public async Task OhneEinstellung_IstSieLeer_UndEsWirdNichtsAngelegt()
    {
        var service = Service(out var db);

        Assert.Null((await service.GetAsync(Guid.NewGuid())).Value!.StepLengthCm);
        Assert.Empty(db.UserPreferences);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(75)]
    [InlineData(120)]
    public async Task Speichern_ImBereich_UndWiederLesen(int cm)
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();

        var ergebnis = await service.UpdateStepLengthAsync(nutzer, new UpdateStepLengthRequest(cm));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(cm, (await service.GetAsync(nutzer)).Value!.StepLengthCm);
    }

    [Theory]
    [InlineData(39)]
    [InlineData(121)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Ausserhalb40Bis120_WirdAbgelehnt_UndDerAlteWertBleibt(int cm)
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();
        await service.UpdateStepLengthAsync(nutzer, new UpdateStepLengthRequest(80));

        var ergebnis = await service.UpdateStepLengthAsync(nutzer, new UpdateStepLengthRequest(cm));

        Assert.False(ergebnis.Succeeded);
        Assert.Equal(80, (await service.GetAsync(nutzer)).Value!.StepLengthCm);
    }

    [Fact]
    public async Task Null_StelltDenStandardWiederHer()
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();
        await service.UpdateStepLengthAsync(nutzer, new UpdateStepLengthRequest(80));

        await service.UpdateStepLengthAsync(nutzer, new UpdateStepLengthRequest(null));

        Assert.Null((await service.GetAsync(nutzer)).Value!.StepLengthCm);
    }

    [Fact]
    public async Task GiltJeNutzer()
    {
        var service = Service(out _);
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();

        await service.UpdateStepLengthAsync(ich, new UpdateStepLengthRequest(90));

        Assert.Null((await service.GetAsync(andere)).Value!.StepLengthCm);
    }
}
