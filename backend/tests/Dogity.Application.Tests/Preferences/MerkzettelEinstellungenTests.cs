using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;

namespace Dogity.Application.Tests.Preferences;

/// <summary>Der Schalter "Merkzettel fürs Training" bei den Erinnerungen (Standard an).</summary>
public class MerkzettelEinstellungenTests
{
    private static PreferenceService Service() => new(InMemoryDbContext.Create());

    [Fact]
    public async Task OhneZeileUndBeiEinerNeuenZeile_IstDieErinnerungAn()
    {
        var service = Service();
        var nutzer = Guid.NewGuid();

        Assert.True((await service.GetAsync(nutzer)).Value!.ReminderTrainingNotes);

        await service.UpdateLocaleAsync(nutzer, new UpdateLocaleRequest("en"));
        Assert.True((await service.GetAsync(nutzer)).Value!.ReminderTrainingNotes);
    }

    [Fact]
    public async Task Speichern_SchaltetAusUndWiederAn_UnabhaengigVonDenAnderen()
    {
        var service = Service();
        var nutzer = Guid.NewGuid();

        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, true, true, 64, 18, TrainingNotes: false));
        var aus = (await service.GetAsync(nutzer)).Value!;
        Assert.False(aus.ReminderTrainingNotes);
        Assert.True(aus.ReminderSessions && aus.ReminderTracks && aus.ReminderTrainerSummary && aus.ReminderHealth);

        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, true, true, 64, 18, TrainingNotes: true));
        Assert.True((await service.GetAsync(nutzer)).Value!.ReminderTrainingNotes);
    }

    [Fact]
    public async Task EineAeltereAppOhneDasFeld_LaesstDenSchalterStehen()
    {
        var service = Service();
        var nutzer = Guid.NewGuid();
        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, true, true, 64, 18, TrainingNotes: false));

        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(false, true, true, 64, 18));

        var gelesen = (await service.GetAsync(nutzer)).Value!;
        Assert.False(gelesen.ReminderSessions);
        Assert.False(gelesen.ReminderTrainingNotes);
    }
}
