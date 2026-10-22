using Dogity.Application.Preferences;
using Dogity.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Preferences;

/// <summary>
/// Einstellungen der Erinnerungen: Vorgaben ohne Zeile, Speichern je Person, und die
/// Prüfung von Wochentags-Maske und Uhrzeit.
/// </summary>
public class ErinnerungsEinstellungenTests
{
    private static PreferenceService Service(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new PreferenceService(db);
    }

    [Fact]
    public async Task OhneZeile_GeltenDieVorgaben_UndEsWirdNichtsAngelegt()
    {
        var service = Service(out var db);

        var gelesen = (await service.GetAsync(Guid.NewGuid())).Value!;

        Assert.True(gelesen.ReminderSessions);
        Assert.True(gelesen.ReminderTracks);
        Assert.True(gelesen.ReminderTrainerSummary);
        Assert.Equal(64, gelesen.TrainerSummaryDays); // Sonntag
        Assert.Equal(18, gelesen.TrainerSummaryHour);
        Assert.Empty(db.UserPreferences);
    }

    [Fact]
    public async Task EineNeueZeile_HatDieselbenVorgaben()
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();
        // Irgendeine andere Einstellung legt die Zeile an - die Erinnerungen bleiben auf den Vorgaben.
        await service.UpdateLocaleAsync(nutzer, new UpdateLocaleRequest("en"));

        var gelesen = (await service.GetAsync(nutzer)).Value!;

        Assert.True(gelesen.ReminderSessions && gelesen.ReminderTracks && gelesen.ReminderTrainerSummary);
        Assert.Equal(64, gelesen.TrainerSummaryDays);
        Assert.Equal(18, gelesen.TrainerSummaryHour);
    }

    [Fact]
    public async Task Speichern_UndWiederLesen()
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();

        var ergebnis = await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(false, true, false, 1 | 4 | 32, 9));

        Assert.True(ergebnis.Succeeded);
        var gelesen = (await service.GetAsync(nutzer)).Value!;
        Assert.False(gelesen.ReminderSessions);
        Assert.True(gelesen.ReminderTracks);
        Assert.False(gelesen.ReminderTrainerSummary);
        Assert.Equal(37, gelesen.TrainerSummaryDays);
        Assert.Equal(9, gelesen.TrainerSummaryHour);
    }

    [Fact]
    public async Task Speichern_GiltJePerson_UndLaesstAndereEinstellungenUnberuehrt()
    {
        var service = Service(out _);
        var ich = Guid.NewGuid();
        var anderer = Guid.NewGuid();
        await service.UpdateLocaleAsync(ich, new UpdateLocaleRequest("en"));
        await service.UpdateEmailNotificationsAsync(ich, new UpdateEmailNotificationsRequest(false));

        await service.UpdateRemindersAsync(ich, new UpdateRemindersRequest(false, false, false, 2, 20));

        var meine = (await service.GetAsync(ich)).Value!;
        Assert.Equal("en", meine.Locale);
        Assert.False(meine.EmailNotifications);
        var fremde = (await service.GetAsync(anderer)).Value!;
        Assert.True(fremde.ReminderSessions);
        Assert.Equal(64, fremde.TrainerSummaryDays);
        Assert.Equal(18, fremde.TrainerSummaryHour);
    }

    [Fact]
    public async Task AndereEinstellungenAendernNichtsAnDenErinnerungen()
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();
        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(false, true, true, 3, 12));

        await service.UpdateEmailNotificationsAsync(nutzer, new UpdateEmailNotificationsRequest(false));
        await service.UpdateLocaleAsync(nutzer, new UpdateLocaleRequest("de"));

        var gelesen = (await service.GetAsync(nutzer)).Value!;
        Assert.False(gelesen.ReminderSessions);
        Assert.Equal(3, gelesen.TrainerSummaryDays);
        Assert.Equal(12, gelesen.TrainerSummaryHour);
    }

    [Theory]
    [InlineData(0)]    // kein Tag
    [InlineData(128)]  // ein achter Tag
    [InlineData(-1)]
    public async Task UngueltigeWochentage_WerdenAbgelehnt_UndNichtsGeaendert(int maske)
    {
        var service = Service(out _);
        var nutzer = Guid.NewGuid();
        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, true, true, 5, 10));

        var ergebnis = await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(false, false, false, maske, 12));

        Assert.False(ergebnis.Succeeded);
        var gelesen = (await service.GetAsync(nutzer)).Value!;
        Assert.True(gelesen.ReminderSessions);
        Assert.Equal(5, gelesen.TrainerSummaryDays);
        Assert.Equal(10, gelesen.TrainerSummaryHour);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(22)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task UngueltigeUhrzeit_WirdAbgelehnt(int stunde)
    {
        var service = Service(out var db);

        var ergebnis = await service.UpdateRemindersAsync(Guid.NewGuid(), new UpdateRemindersRequest(true, true, true, 64, stunde));

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(db.UserPreferences);
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(127, 21)]
    public async Task Grenzwerte_SindGueltig(int maske, int stunde)
    {
        var service = Service(out _);

        Assert.True((await service.UpdateRemindersAsync(Guid.NewGuid(), new UpdateRemindersRequest(true, true, true, maske, stunde))).Succeeded);
    }

    [Fact]
    public async Task Zeile_AbgeschalteteSchalterBleibenAnderenNutzernFremd()
    {
        // Wer nur die Fährten-Erinnerung abschaltet, schaltet nichts anderes ab.
        var service = Service(out var db);
        var nutzer = Guid.NewGuid();
        await service.UpdateRemindersAsync(nutzer, new UpdateRemindersRequest(true, false, true, 64, 18));

        var zeile = await db.UserPreferences.SingleAsync();
        Assert.True(zeile.ReminderSessions);
        Assert.False(zeile.ReminderTracks);
        Assert.True(zeile.ReminderTrainerSummary);
    }
}
