using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;

namespace Dogity.Application.Tests.Preferences;

/// <summary>
/// Der Schalter "Gesundheit" bei den Erinnerungen (Standard an) und das abschaltbare Modul "gesundheit".
/// </summary>
public class GesundheitEinstellungenTests
{
    private static PreferenceService Service() => new(InMemoryDbContext.Create());

    [Fact]
    public async Task OhneZeileUndBeiEinerNeuenZeile_IstDieErinnerungAn()
    {
        var service = Service();
        var nutzer = Guid.NewGuid();

        Assert.True((await service.GetAsync(nutzer)).Value!.ReminderHealth);

        await service.UpdateLocaleAsync(nutzer, new UpdateLocaleRequest("en"));
        Assert.True((await service.GetAsync(nutzer)).Value!.ReminderHealth);
    }

    [Fact]
    public async Task Speichern_SchaltetDieGesundheitserinnerungAusUndWiederAn_UnabhaengigVonDenAnderen()
    {
        var service = Service();
        var nutzer = Guid.NewGuid();

        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, true, true, 64, 18, Health: false));
        var aus = (await service.GetAsync(nutzer)).Value!;
        Assert.False(aus.ReminderHealth);
        Assert.True(aus.ReminderSessions && aus.ReminderTracks && aus.ReminderTrainerSummary);

        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, true, true, 64, 18, Health: true));
        Assert.True((await service.GetAsync(nutzer)).Value!.ReminderHealth);
    }

    [Fact]
    public async Task EineAeltereAppOhneGesundheitsFeld_LaesstDenSchalterStehen()
    {
        var service = Service();
        var nutzer = Guid.NewGuid();
        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, true, true, 64, 18, Health: false));

        // Ein Gerät mit der Fassung von vorher kennt das Feld nicht und schickt es nicht mit.
        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(false, true, true, 64, 18));

        var gelesen = (await service.GetAsync(nutzer)).Value!;
        Assert.False(gelesen.ReminderSessions);
        Assert.False(gelesen.ReminderHealth);
    }

    [Fact]
    public async Task ModulGesundheit_LaesstSichAbschaltenUndBleibtGespeichert()
    {
        var service = Service();
        var nutzer = Guid.NewGuid();

        Assert.Contains(Modules.Gesundheit, Modules.Bekannt);
        await service.UpdateModulesAsync(nutzer, new UpdateModulesRequest([Modules.Gesundheit, "unbekannt"]));

        Assert.Equal([Modules.Gesundheit], (await service.GetAsync(nutzer)).Value!.DisabledModules);

        await service.UpdateModulesAsync(nutzer, new UpdateModulesRequest([]));
        Assert.Empty((await service.GetAsync(nutzer)).Value!.DisabledModules);
    }
}
