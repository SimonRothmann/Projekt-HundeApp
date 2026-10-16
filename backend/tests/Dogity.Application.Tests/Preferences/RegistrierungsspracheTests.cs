using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Preferences;

/// <summary>
/// Die Sprache, in der jemand das Konto anlegt, wird gemerkt und gilt für Mails und
/// Push - aber nur, wenn der Server sie kennt. Alles andere ist kein Fehler, es wird
/// nur nichts gespeichert.
/// </summary>
public class RegistrierungsspracheTests
{
    private static PreferenceService Service(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new PreferenceService(db);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("de", "de")]
    [InlineData(" EN ", "en")]
    public async Task ErlaubteSprache_WirdGespeichert(string eingabe, string erwartet)
    {
        var service = Service(out var db);
        var nutzer = Guid.NewGuid();

        var gespeichert = await service.SetRegistrationLocaleAsync(nutzer, eingabe);

        Assert.Equal(erwartet, gespeichert);
        Assert.Equal(erwartet, (await service.GetAsync(nutzer)).Value!.Locale);
        Assert.Single(await db.UserPreferences.Where(p => p.UserId == nutzer).ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fr")]
    [InlineData("english")]
    [InlineData("en-US")]
    public async Task OhneOderUngueltigeSprache_SpeichertNichts_UndLegtKeineZeileAn(string? eingabe)
    {
        var service = Service(out var db);
        var nutzer = Guid.NewGuid();

        var gespeichert = await service.SetRegistrationLocaleAsync(nutzer, eingabe);

        Assert.Null(gespeichert);
        Assert.Empty(await db.UserPreferences.ToListAsync());
        Assert.Null((await service.GetAsync(nutzer)).Value!.Locale);
    }

    [Fact]
    public async Task UngueltigeSprache_LaesstBestehendeEinstellungenUnberuehrt()
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();
        await service.UpdateEmailNotificationsAsync(nutzer, new UpdateEmailNotificationsRequest(false));
        await service.SetRegistrationLocaleAsync(nutzer, "en");

        await service.SetRegistrationLocaleAsync(nutzer, "fr");

        var gelesen = (await service.GetAsync(nutzer)).Value!;
        Assert.Equal("en", gelesen.Locale);
        Assert.False(gelesen.EmailNotifications);
    }

    [Fact]
    public void Sprachen_KennenNurDeutschUndEnglisch()
    {
        Assert.Equal(["de", "en"], Sprachen.Erlaubt.Order());
        Assert.True(Sprachen.IstEnglisch("en"));
        Assert.False(Sprachen.IstEnglisch("de"));
        Assert.False(Sprachen.IstEnglisch(null));
        Assert.False(Sprachen.IstEnglisch("fr"));
    }

    [Fact]
    public void RegisterRequest_OhneLocale_GiltAlsNichtGesetzt()
    {
        var optionen = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var alt = System.Text.Json.JsonSerializer.Deserialize<Dogity.Api.Contracts.RegisterRequest>(
            """{"email":"a@b.de","password":"x","firstName":"A","lastName":"B"}""", optionen)!;
        var neu = System.Text.Json.JsonSerializer.Deserialize<Dogity.Api.Contracts.RegisterRequest>(
            """{"email":"a@b.de","password":"x","firstName":"A","lastName":"B","locale":"en"}""", optionen)!;

        Assert.Null(alt.Locale);
        Assert.Equal("en", neu.Locale);
    }
}
