using System.Text.Json;
using Dogity.Application.Account;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Account;

/// <summary>
/// Die Einstellungen der Erinnerungen und der Merkzettel "Erinnerung versendet" in Auskunft
/// und Löschung (Art. 15, 17, 20 DSGVO).
/// </summary>
public class AccountDataErinnerungenTests
{
    private static (AccountDataService Dienst, Dogity.Infrastructure.Persistence.ApplicationDbContext Db, FakeUserLookupService Lookup) Aufsetzen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        return (new AccountDataService(db, lookup, new FakeNotificationService()), db, lookup);
    }

    private static SentReminder Erinnerung(Guid nutzer, ReminderType art, Guid? bezug = null, DateOnly? tag = null) => new()
    {
        UserId = nutzer,
        Type = art,
        ReferenceId = bezug ?? Guid.NewGuid(),
        DayKey = tag ?? SentReminder.KeinTag,
    };

    [Fact]
    public async Task Export_NenntDieEinstellungen_MitWochentagenAlsNamen()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        lookup.Register(nutzer, "ich@test.de", "Max", "Muster");
        db.UserPreferences.Add(new UserPreference
        {
            UserId = nutzer, ReminderSessions = false, ReminderTracks = true, ReminderTrainerSummary = true,
            TrainerSummaryDays = 1 | 4 | 64, TrainerSummaryHour = 9,
        });
        await db.SaveChangesAsync();

        var einstellungen = (await dienst.ExportAsync(nutzer)).Value!.Einstellungen!;

        Assert.False(einstellungen.ErinnerungTermine);
        Assert.True(einstellungen.ErinnerungFaehrten);
        Assert.True(einstellungen.ErinnerungRueckmeldungen);
        Assert.Equal(["Montag", "Mittwoch", "Sonntag"], einstellungen.RueckmeldungenWochentage);
        Assert.Equal(9, einstellungen.RueckmeldungenUhrzeit);
    }

    [Fact]
    public async Task Export_ListetDieVersendetenErinnerungen_NurDieEigenen()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        var anderer = Guid.NewGuid();
        lookup.Register(ich, "ich@test.de", "Max", "Muster");
        var termin = Guid.NewGuid();
        db.SentReminders.AddRange(
            Erinnerung(ich, ReminderType.SessionDayBefore, termin),
            Erinnerung(ich, ReminderType.TrackNotWalked),
            Erinnerung(ich, ReminderType.TrainerFeedbackSummary, Guid.Empty, new DateOnly(2026, 10, 25)),
            Erinnerung(anderer, ReminderType.SessionDayBefore));
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(ich)).Value!;

        Assert.Equal(3, export.ErinnerungenVersendet.Count);
        var terminZeile = Assert.Single(export.ErinnerungenVersendet, e => e.Art == "Termin am Vortag");
        Assert.Equal(termin, terminZeile.Bezug);
        Assert.Null(terminZeile.Tag);
        var zusammenfassung = Assert.Single(export.ErinnerungenVersendet, e => e.Art == "Offene Rückmeldungen");
        Assert.Null(zusammenfassung.Bezug);
        Assert.Equal(new DateOnly(2026, 10, 25), zusammenfassung.Tag);
        Assert.Contains(export.ErinnerungenVersendet, e => e.Art == "Fährte nicht abgelaufen");
        // Die Auskunft enthält nichts, was einer anderen Person gehört.
        Assert.DoesNotContain(anderer.ToString(), JsonSerializer.Serialize(export));
    }

    [Fact]
    public async Task Purge_LoeschtDieMerkzettelEndgueltig_UndNurDieEigenen()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var anderer = Guid.NewGuid();
        db.SentReminders.Add(Erinnerung(ich, ReminderType.SessionDayBefore));
        // Auch weich Gelöschtes muss weg - ausgeblendet ist nicht gelöscht.
        var weich = Erinnerung(ich, ReminderType.TrackNotWalked);
        weich.DeletedAt = DateTimeOffset.UtcNow;
        db.SentReminders.Add(weich);
        db.SentReminders.Add(Erinnerung(anderer, ReminderType.SessionDayBefore));
        db.UserPreferences.Add(new UserPreference { UserId = ich, TrainerSummaryHour = 10 });
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        Assert.Empty(db.SentReminders.IgnoreQueryFilters().Where(r => r.UserId == ich));
        Assert.Single(db.SentReminders.IgnoreQueryFilters().Where(r => r.UserId == anderer));
        // Die Einstellungen samt Erinnerungs-Feldern gehen mit der Zeile.
        Assert.Empty(db.UserPreferences.IgnoreQueryFilters().Where(p => p.UserId == ich));
    }
}
