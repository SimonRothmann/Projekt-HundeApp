using System.Text.Json;
using Dogity.Application.Account;
using Dogity.Application.Admin;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Account;

/// <summary>
/// Push-Geräte und die Mail-Einstellung in Auskunft und Löschung (Art. 15, 17, 20
/// DSGVO). Wie bei AccountDataServiceTests wird die Löschung mit
/// IgnoreQueryFilters nachgeprüft: Ausgeblendet ist nicht gelöscht.
/// </summary>
public class AccountDataPushTests
{
    private const string GeheimerSchluessel = "GEHEIMER-SCHLUESSEL-p256dh";
    private const string GeheimesAuth = "GEHEIMES-auth";
    private const string GeheimerEndpoint = "https://web.push.apple.com/GEHEIMER-ENDPOINT";

    private static (AccountDataService Dienst, Dogity.Infrastructure.Persistence.ApplicationDbContext Db, FakeUserLookupService Lookup) Aufsetzen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        return (new AccountDataService(db, lookup, new FakeNotificationService()), db, lookup);
    }

    private static PushSubscription Geraet(Guid nutzer, string bezeichnung = "iPhone (Safari)") => new()
    {
        UserId = nutzer,
        Endpoint = GeheimerEndpoint + Guid.NewGuid(),
        P256dh = GeheimerSchluessel,
        Auth = GeheimesAuth,
        UserAgent = bezeichnung,
        LastUsedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public async Task Export_ListetDieGeraete_OhneSchluesselUndOhneAdresse()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        lookup.Register(nutzer, "ich@test.de", "Max", "Muster");
        db.PushSubscriptions.Add(Geraet(nutzer));
        db.PushSubscriptions.Add(Geraet(Guid.NewGuid(), "Fremdes Gerät"));
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(nutzer)).Value!;

        var geraet = Assert.Single(export.PushGeraete);
        Assert.Equal("iPhone (Safari)", geraet.Geraet);
        Assert.NotNull(geraet.ZuletztZugestellt);

        // Die Zugangsdaten des Geräts stehen NIRGENDS in der Auskunft - weder
        // als Feld noch als Text.
        var json = JsonSerializer.Serialize(export);
        Assert.DoesNotContain(GeheimerSchluessel, json);
        Assert.DoesNotContain(GeheimesAuth, json);
        Assert.DoesNotContain("GEHEIMER-ENDPOINT", json);
        Assert.DoesNotContain("Fremdes Gerät", json);
    }

    [Fact]
    public async Task Export_NennteDieMailEinstellung()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        lookup.Register(nutzer, "ich@test.de", "Max", "Muster");
        db.UserPreferences.Add(new UserPreference { UserId = nutzer, EmailNotifications = false });
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(nutzer)).Value!;

        Assert.False(export.Einstellungen!.WichtigesPerEMail);
    }

    [Fact]
    public async Task Purge_LoeschtDieGeraeteEndgueltig_UndNurDieEigenen()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var anderer = Guid.NewGuid();
        db.PushSubscriptions.Add(Geraet(ich));
        // Auch weich Gelöschtes muss weg - ausgeblendet ist nicht gelöscht.
        var weich = Geraet(ich);
        weich.DeletedAt = DateTimeOffset.UtcNow;
        db.PushSubscriptions.Add(weich);
        db.PushSubscriptions.Add(Geraet(anderer));
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        Assert.Empty(db.PushSubscriptions.IgnoreQueryFilters().Where(s => s.UserId == ich));
        Assert.Single(db.PushSubscriptions.IgnoreQueryFilters().Where(s => s.UserId == anderer));
    }

    [Fact]
    public async Task Bereinigung_ErkenntKontenAnDenenNurEinGeraetHaengt()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var admin = Guid.NewGuid();
        lookup.Register(admin, "admin@test.de");
        var weg = Guid.NewGuid();
        db.PushSubscriptions.Add(Geraet(weg));
        await db.SaveChangesAsync();
        var dienst = new AdminService(db, lookup, new FakeRefreshTokenService(), new AccountDataService(db, lookup, new FakeNotificationService()));

        var ergebnis = await dienst.PurgeOrphanedDataAsync(admin);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(1, ergebnis.Value!.Konten);
        Assert.Empty(db.PushSubscriptions.IgnoreQueryFilters());
    }
}
