using Dogity.Application.Notifications;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Geräte an- und abmelden: Upsert über die Adresse, nur eigene Geräte
/// abmeldbar, Eingaben geprüft.
/// </summary>
public class PushSubscriptionServiceTests
{
    private const string Apple = "https://web.push.apple.com/QAbc123";
    private const string Google = "https://fcm.googleapis.com/fcm/send/xyz";
    private const string Schluessel = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM";
    private const string Auth = "tBHItJI5svbpez7KI4CCXg";

    private static (PushSubscriptionService Service, Dogity.Infrastructure.Persistence.ApplicationDbContext Db) Aufbauen()
    {
        var db = InMemoryDbContext.Create();
        return (new PushSubscriptionService(db), db);
    }

    private static SubscribePushRequest Anfrage(string endpoint = Apple) => new(endpoint, new PushKeysDto(Schluessel, Auth));

    [Fact]
    public async Task Anmelden_LegtGeraetMitKurzbezeichnungAn()
    {
        var (service, db) = Aufbauen();
        var nutzer = Guid.NewGuid();

        var ergebnis = await service.SubscribeAsync(nutzer, Anfrage(),
            "Mozilla/5.0 (iPhone; CPU iPhone OS 18_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.4 Mobile/15E148 Safari/604.1");

        Assert.True(ergebnis.Succeeded);
        var abo = await db.PushSubscriptions.SingleAsync();
        Assert.Equal(nutzer, abo.UserId);
        Assert.Equal(Apple, abo.Endpoint);
        // Nicht der rohe User-Agent - nur Gerät und Browser.
        Assert.Equal("iPhone (Safari)", abo.UserAgent);
    }

    [Fact]
    public async Task Anmelden_DerselbeEndpointNochmal_AktualisiertStattZuDoppeln()
    {
        var (service, db) = Aufbauen();
        var nutzer = Guid.NewGuid();
        await service.SubscribeAsync(nutzer, Anfrage(), null);

        await service.SubscribeAsync(nutzer, new SubscribePushRequest(Apple, new PushKeysDto("NEUER-Schluessel_1", "NEUER-auth_2")), null);

        var abo = await db.PushSubscriptions.SingleAsync();
        Assert.Equal("NEUER-Schluessel_1", abo.P256dh);
        Assert.Equal("NEUER-auth_2", abo.Auth);
    }

    [Fact]
    public async Task Anmelden_AufEinemGeraetMitFremdemAbo_UebernimmtDasGeraet()
    {
        var (service, db) = Aufbauen();
        var vorher = Guid.NewGuid();
        var nachher = Guid.NewGuid();
        await service.SubscribeAsync(vorher, Anfrage(), null);

        await service.SubscribeAsync(nachher, Anfrage(), null);

        // Eine Zeile, und sie gehört jetzt der zweiten Person.
        var abo = await db.PushSubscriptions.SingleAsync();
        Assert.Equal(nachher, abo.UserId);
    }

    [Fact]
    public async Task Anmelden_Uebernahme_SetztDasAnmeldedatumNeu_DerselbeNutzerBehaeltEs()
    {
        var (service, db) = Aufbauen();
        var vorher = Guid.NewGuid();
        var nachher = Guid.NewGuid();
        var alt = DateTimeOffset.UtcNow.AddDays(-40);
        db.PushSubscriptions.Add(new PushSubscription
        {
            UserId = vorher, Endpoint = Apple, P256dh = "alt", Auth = "alt", CreatedAt = alt, LastUsedAt = alt,
        });
        await db.SaveChangesAsync();

        // Dieselbe Person meldet neue Schlüssel: Datum bleibt.
        await service.SubscribeAsync(vorher, Anfrage(), null);
        Assert.Equal(alt, (await db.PushSubscriptions.SingleAsync()).CreatedAt);

        // Eine andere Person übernimmt das Gerät: Datum und letzte Zustellung gehören ihr.
        await service.SubscribeAsync(nachher, Anfrage(), null);
        var abo = await db.PushSubscriptions.SingleAsync();
        Assert.True(abo.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Null(abo.LastUsedAt);
    }

    [Fact]
    public async Task Anmelden_BelebtEineWeichGeloeschteZeileWieder()
    {
        var (service, db) = Aufbauen();
        var nutzer = Guid.NewGuid();
        db.PushSubscriptions.Add(new PushSubscription
        {
            UserId = nutzer, Endpoint = Apple, P256dh = "alt", Auth = "alt", DeletedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var ergebnis = await service.SubscribeAsync(nutzer, Anfrage(), null);

        Assert.True(ergebnis.Succeeded);
        Assert.Single(await db.PushSubscriptions.IgnoreQueryFilters().ToListAsync());
        Assert.Single(await db.PushSubscriptions.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("kein-url")]
    [InlineData("http://web.push.apple.com/abc")]
    [InlineData("https://intern.local/abc")]
    [InlineData("https://127.0.0.1/abc")]
    [InlineData("https://fcm.googleapis.com.evil.example/abc")]
    [InlineData("https://evilfcm.googleapis.com.example/abc")]
    [InlineData("https://user:pw@fcm.googleapis.com/abc")]
    [InlineData("https://fcm.googleapis.com:8443/abc")]
    public async Task Anmelden_LehntUnbekannteAdressenAb(string endpoint)
    {
        var (service, db) = Aufbauen();

        var ergebnis = await service.SubscribeAsync(Guid.NewGuid(), Anfrage(endpoint), null);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(await db.PushSubscriptions.ToListAsync());
    }

    [Theory]
    [InlineData("", Auth)]
    [InlineData(Schluessel, "")]
    [InlineData("hat leerzeichen", Auth)]
    [InlineData(Schluessel, "<script>")]
    public async Task Anmelden_LehntUngueltigeSchluesselAb(string p256dh, string auth)
    {
        var (service, db) = Aufbauen();

        var ergebnis = await service.SubscribeAsync(Guid.NewGuid(), new SubscribePushRequest(Apple, new PushKeysDto(p256dh, auth)), null);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(await db.PushSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task Anmelden_LehntZuLangeSchluesselAb()
    {
        var (service, _) = Aufbauen();

        var ergebnis = await service.SubscribeAsync(Guid.NewGuid(),
            new SubscribePushRequest(Apple, new PushKeysDto(new string('a', 201), Auth)), null);

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Anmelden_OhneSchluessel_WirdAbgelehnt_NichtAlsFehlerGeworfen()
    {
        var (service, _) = Aufbauen();

        var ergebnis = await service.SubscribeAsync(Guid.NewGuid(), new SubscribePushRequest(Apple, null!), null);

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Anmelden_BegrenztDieGeraeteJePerson_UndWerfeDieAeltestenWeg()
    {
        var (service, db) = Aufbauen();
        var nutzer = Guid.NewGuid();
        for (var i = 0; i < PushSubscriptionService.MaxGeraete; i++)
            db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = nutzer,
                Endpoint = $"https://fcm.googleapis.com/fcm/send/{i}",
                P256dh = "a",
                Auth = "b",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-30 + i),
            });
        await db.SaveChangesAsync();

        await service.SubscribeAsync(nutzer, Anfrage(Apple), null);

        var uebrig = await db.PushSubscriptions.Where(s => s.UserId == nutzer).ToListAsync();
        Assert.Equal(PushSubscriptionService.MaxGeraete, uebrig.Count);
        Assert.Contains(uebrig, s => s.Endpoint == Apple);
        Assert.DoesNotContain(uebrig, s => s.Endpoint.EndsWith("/0"));
    }

    [Fact]
    public async Task Abmelden_EntferntDasEigeneGeraet_Endgueltig()
    {
        var (service, db) = Aufbauen();
        var nutzer = Guid.NewGuid();
        await service.SubscribeAsync(nutzer, Anfrage(), null);

        var ergebnis = await service.UnsubscribeAsync(nutzer, new UnsubscribePushRequest(Apple));

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(await db.PushSubscriptions.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Abmelden_FremdesGeraet_GehtNicht_UndBleibtBestehen()
    {
        var (service, db) = Aufbauen();
        var besitzer = Guid.NewGuid();
        await service.SubscribeAsync(besitzer, Anfrage(), null);

        var ergebnis = await service.UnsubscribeAsync(Guid.NewGuid(), new UnsubscribePushRequest(Apple));

        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
        var abo = await db.PushSubscriptions.SingleAsync();
        Assert.Equal(besitzer, abo.UserId);
    }

    [Fact]
    public async Task Abmelden_UnbekanntesGeraet_IstNichtGefunden()
    {
        var (service, _) = Aufbauen();

        var ergebnis = await service.UnsubscribeAsync(Guid.NewGuid(), new UnsubscribePushRequest(Google));

        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Abmelden_OhneEndpoint_WirdAbgelehnt()
    {
        var (service, _) = Aufbauen();

        Assert.False((await service.UnsubscribeAsync(Guid.NewGuid(), new UnsubscribePushRequest(""))).Succeeded);
        Assert.False((await service.UnsubscribeAsync(Guid.NewGuid(), new UnsubscribePushRequest(new string('x', 3000)))).Succeeded);
    }
}
