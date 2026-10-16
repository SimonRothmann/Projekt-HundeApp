using Dogity.Application.Notifications;
using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Notifications;

/// <summary>Abmelden und Wiederanmelden über den Link - nur mit gültigem Token und bestehendem Konto.</summary>
public class EmailAbmeldungServiceTests
{
    private sealed record Umgebung(
        EmailAbmeldungService Service,
        IEmailAbmeldeToken Tokens,
        ApplicationDbContext Db,
        FakeUserLookupService Lookup,
        Guid UserId);

    private static Umgebung Aufbauen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var userId = Guid.NewGuid();
        lookup.Register(userId, "max@example.com", "Max", "Muster");
        var tokens = TestAbmeldeToken.Neu();
        var service = new EmailAbmeldungService(tokens, lookup, new PreferenceService(db));
        return new Umgebung(service, tokens, db, lookup, userId);
    }

    private static Task<bool?> Gespeichert(ApplicationDbContext db, Guid userId) =>
        db.UserPreferences.Where(p => p.UserId == userId).Select(p => (bool?)p.EmailNotifications).SingleOrDefaultAsync();

    [Fact]
    public async Task Abmelden_LegtDieEinstellungAnAuf_False_AuchOhneVorhandeneZeile()
    {
        var u = Aufbauen();
        Assert.Null(await Gespeichert(u.Db, u.UserId));

        var ergebnis = await u.Service.SetzeAsync(u.Tokens.Erzeuge(u.UserId), aktiv: false);

        Assert.True(ergebnis.Succeeded);
        Assert.False(await Gespeichert(u.Db, u.UserId));
    }

    [Fact]
    public async Task Abmelden_AendertEineVorhandeneZeile()
    {
        var u = Aufbauen();
        u.Db.UserPreferences.Add(new Dogity.Domain.Preferences.UserPreference { UserId = u.UserId, EmailNotifications = true, Locale = "en" });
        await u.Db.SaveChangesAsync();

        await u.Service.SetzeAsync(u.Tokens.Erzeuge(u.UserId), false);

        var zeile = await u.Db.UserPreferences.SingleAsync(p => p.UserId == u.UserId);
        Assert.False(zeile.EmailNotifications);
        Assert.Equal("en", zeile.Locale);
    }

    [Fact]
    public async Task WiederAnmelden_SetztTrue()
    {
        var u = Aufbauen();
        var token = u.Tokens.Erzeuge(u.UserId);
        await u.Service.SetzeAsync(token, false);

        var ergebnis = await u.Service.SetzeAsync(token, true);

        Assert.True(ergebnis.Succeeded);
        Assert.True(await Gespeichert(u.Db, u.UserId));
    }

    [Fact]
    public async Task Idempotent_ZweimalAbmelden_EineZeile_WeiterhinFalse()
    {
        var u = Aufbauen();
        var token = u.Tokens.Erzeuge(u.UserId);

        Assert.True((await u.Service.SetzeAsync(token, false)).Succeeded);
        Assert.True((await u.Service.SetzeAsync(token, false)).Succeeded);

        Assert.Equal(1, await u.Db.UserPreferences.CountAsync(p => p.UserId == u.UserId));
        Assert.False(await Gespeichert(u.Db, u.UserId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein-token")]
    public async Task UngueltigesToken_IstFehler_OhneAenderung(string? token)
    {
        var u = Aufbauen();

        var ergebnis = await u.Service.SetzeAsync(token, false);

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Equal("Der Link ist ungültig oder abgelaufen.", Assert.Single(ergebnis.Errors));
        Assert.Empty(u.Db.UserPreferences);
    }

    [Fact]
    public async Task GeloeschtesKonto_IstUngueltig_UndLegtKeineZeileAn()
    {
        var u = Aufbauen();
        var unbekannt = Guid.NewGuid();

        var ergebnis = await u.Service.SetzeAsync(u.Tokens.Erzeuge(unbekannt), false);

        // Dieselbe Meldung wie bei einem ungültigen Token - verrät nicht, ob es das Konto gibt.
        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Der Link ist ungültig oder abgelaufen.", Assert.Single(ergebnis.Errors));
        Assert.Empty(u.Db.UserPreferences);
    }

    [Fact]
    public async Task WirktNurAufDasKontoImToken()
    {
        var u = Aufbauen();
        var anderer = Guid.NewGuid();
        u.Lookup.Register(anderer, "anna@example.com", "Anna", "Muster");

        await u.Service.SetzeAsync(u.Tokens.Erzeuge(u.UserId), false);

        Assert.False(await Gespeichert(u.Db, u.UserId));
        Assert.Null(await Gespeichert(u.Db, anderer));
    }
}
