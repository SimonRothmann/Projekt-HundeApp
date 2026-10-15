using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Preferences;

/// <summary>"Wichtiges auch per E-Mail": Vorgabe ja, ein- und ausschaltbar, je Person.</summary>
public class EmailNotificationsTests
{
    private static PreferenceService Service(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new PreferenceService(db);
    }

    [Fact]
    public async Task OhneEinstellung_GiltJa()
    {
        var service = Service(out _);

        var gelesen = await service.GetAsync(Guid.NewGuid());

        Assert.True(gelesen.Value!.EmailNotifications);
    }

    [Fact]
    public async Task Ausschalten_UndWiederEinschalten()
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();

        Assert.True((await service.UpdateEmailNotificationsAsync(nutzer, new UpdateEmailNotificationsRequest(false))).Succeeded);
        Assert.False((await service.GetAsync(nutzer)).Value!.EmailNotifications);

        await service.UpdateEmailNotificationsAsync(nutzer, new UpdateEmailNotificationsRequest(true));
        Assert.True((await service.GetAsync(nutzer)).Value!.EmailNotifications);
    }

    [Fact]
    public async Task GiltJePerson_UndLaesstAndereEinstellungenUnberuehrt()
    {
        var service = Service(out var db);
        var ich = Guid.NewGuid();
        var anderer = Guid.NewGuid();
        await service.UpdateLocaleAsync(ich, new UpdateLocaleRequest("en"));

        await service.UpdateEmailNotificationsAsync(ich, new UpdateEmailNotificationsRequest(false));

        Assert.Equal("en", (await service.GetAsync(ich)).Value!.Locale);
        Assert.True((await service.GetAsync(anderer)).Value!.EmailNotifications);
        Assert.Single(await db.UserPreferences.Where(p => p.UserId == ich).ToListAsync());
    }

    [Fact]
    public async Task AnderesSpeichern_SchaltetDieMailsNichtWiederAn()
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();
        await service.UpdateEmailNotificationsAsync(nutzer, new UpdateEmailNotificationsRequest(false));

        await service.UpdateFontScaleAsync(nutzer, new UpdateFontScaleRequest(FontScales.Gross));

        Assert.False((await service.GetAsync(nutzer)).Value!.EmailNotifications);
    }
}
