using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;

namespace Dogity.Application.Tests.Preferences;

/// <summary>
/// Testet die Schriftgröße: Sie ist die einzige Einstellung, deren falscher
/// Wert nicht auffällt, sondern nur nicht wirkt - eine unbekannte Stufe käme
/// als "gespeichert" zurück, die Oberfläche bliebe unverändert, und niemand
/// wüsste warum. Deshalb wird sie abgelehnt statt still verworfen.
/// </summary>
public class PreferenceServiceTests
{
    private static PreferenceService Service(out Guid userId)
    {
        userId = Guid.NewGuid();
        return new PreferenceService(InMemoryDbContext.Create());
    }

    [Theory]
    [InlineData(FontScales.Normal)]
    [InlineData(FontScales.Gross)]
    [InlineData(FontScales.SehrGross)]
    public async Task Speichert_jede_angebotene_Stufe(string stufe)
    {
        var service = Service(out var userId);

        var ergebnis = await service.UpdateFontScaleAsync(userId, new UpdateFontScaleRequest(stufe));

        Assert.True(ergebnis.Succeeded);
        var gelesen = await service.GetAsync(userId);
        Assert.Equal(stufe, gelesen.Value!.FontScale);
    }

    [Fact]
    public async Task Lehnt_eine_unbekannte_Stufe_ab()
    {
        var service = Service(out var userId);

        var ergebnis = await service.UpdateFontScaleAsync(userId, new UpdateFontScaleRequest("riesig"));

        Assert.False(ergebnis.Succeeded);
        // Nichts gespeichert: die abgelehnte Stufe darf nicht doch ankommen.
        var gelesen = await service.GetAsync(userId);
        Assert.Null(gelesen.Value!.FontScale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Leer_setzt_auf_die_Vorgabe_zurueck(string? leer)
    {
        var service = Service(out var userId);
        await service.UpdateFontScaleAsync(userId, new UpdateFontScaleRequest(FontScales.SehrGross));

        var ergebnis = await service.UpdateFontScaleAsync(userId, new UpdateFontScaleRequest(leer));

        Assert.True(ergebnis.Succeeded);
        var gelesen = await service.GetAsync(userId);
        Assert.Null(gelesen.Value!.FontScale);
    }

    [Fact]
    public async Task Ohne_je_etwas_eingestellt_zu_haben_gilt_die_Vorgabe()
    {
        var service = Service(out var userId);

        var gelesen = await service.GetAsync(userId);

        Assert.True(gelesen.Succeeded);
        Assert.Null(gelesen.Value!.FontScale);
    }

    [Fact]
    public async Task Schriftgroesse_laesst_Sprache_und_Land_unberuehrt()
    {
        // Drei unabhängige Einstellungen an EINER Zeile - das Setzen der einen
        // darf die anderen nicht mitnehmen.
        var service = Service(out var userId);
        await service.UpdateLocaleAsync(userId, new UpdateLocaleRequest("en"));

        await service.UpdateFontScaleAsync(userId, new UpdateFontScaleRequest(FontScales.Gross));

        var gelesen = await service.GetAsync(userId);
        Assert.Equal("en", gelesen.Value!.Locale);
        Assert.Equal(FontScales.Gross, gelesen.Value!.FontScale);
    }
}
