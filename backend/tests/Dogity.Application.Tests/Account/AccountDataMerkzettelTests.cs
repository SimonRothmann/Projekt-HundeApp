using Dogity.Application.Account;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Account;

/// <summary>
/// Der Merkzettel in Auskunft und Löschung (Art. 15, 17, 20 DSGVO): Der Export nennt die eigenen Einträge an meinen
/// Hunden und den Rhythmus meiner Hunde (nie fremde), die Löschung entfernt sie endgültig - auch die weich
/// gelöschten - und lässt an geteilten Hunden die Einträge der übrigen Besitzer:innen stehen.
/// </summary>
public class AccountDataMerkzettelTests
{
    private static (AccountDataService Dienst, ApplicationDbContext Db, FakeUserLookupService Lookup) Aufsetzen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        return (new AccountDataService(db, lookup, new FakeNotificationService()), db, lookup);
    }

    private static Dog Hund(ApplicationDbContext db, Guid besitzer, string name)
    {
        var hund = new Dog { Name = name };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        return hund;
    }

    private static TrainingNote Notiz(
        Guid hund, string text, Guid? von, TrainingNoteGoal ziel = TrainingNoteGoal.NextTraining, DateOnly? datum = null,
        Guid? uebung = null, bool geloescht = false, DateTimeOffset? erledigt = null) => new()
    {
        DogId = hund, UserId = von, Text = text, Goal = ziel, TargetDate = datum, ExerciseId = uebung,
        DoneAt = erledigt, DeletedAt = geloescht ? DateTimeOffset.UtcNow : null,
    };

    [Fact]
    public async Task Export_EnthaeltMeineEintraegeAnMeinenHunden_NichtDieFremderOderGeloeschte()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        lookup.Register(ich, "ich@dogity.test", "Ich", "Selbst");
        var sport = new Sport { Code = "T", Name = "T" };
        var uebung = new Exercise { SportId = sport.Id, Name = "Sitz" };
        db.Sports.Add(sport);
        db.Exercises.Add(uebung);
        var mein = Hund(db, ich, "Bello");
        var geteilt = Hund(db, andere, "Emma");
        db.DogOwners.Add(new DogOwner { DogId = geteilt.Id, UserId = ich });
        var fremd = Hund(db, andere, "Fremder");
        var erledigt = new DateTimeOffset(2026, 11, 3, 18, 0, 0, TimeSpan.Zero);
        db.TrainingNotes.AddRange(
            Notiz(mein.Id, "Fuß rückwärts", ich, uebung: uebung.Id),
            Notiz(mein.Id, "Am Montag", ich, TrainingNoteGoal.Date, new DateOnly(2026, 11, 9), erledigt: erledigt),
            Notiz(mein.Id, "Gelöscht", ich, geloescht: true),
            Notiz(mein.Id, "Von früherer Mitbesitzerin", null),
            Notiz(geteilt.Id, "Von ihr", andere),
            Notiz(geteilt.Id, "Von mir geteilt", ich, TrainingNoteGoal.NextWeek, new DateOnly(2026, 11, 9)),
            Notiz(fremd.Id, "Fremd", andere));
        db.DogTrainingRhythms.AddRange(
            new DogTrainingRhythm { DogId = mein.Id, Days = 4 | 32, Hour = 18 },
            new DogTrainingRhythm { DogId = geteilt.Id, Days = 1, Hour = null },
            new DogTrainingRhythm { DogId = fremd.Id, Days = 64, Hour = 9 });
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(ich)).Value!;

        var merkzettel = export.Merkzettel!;
        Assert.Equal(
            ["Am Montag", "Fuß rückwärts", "Von früherer Mitbesitzerin", "Von mir geteilt"],
            merkzettel.Select(n => n.Text).Order());
        var fuss = merkzettel.Single(n => n.Text == "Fuß rückwärts");
        Assert.Equal((mein.Id, "Sitz", "Nächstes Training", (DateOnly?)null), (fuss.HundId, fuss.Uebung, fuss.Ziel, fuss.Datum));
        var montag = merkzettel.Single(n => n.Text == "Am Montag");
        Assert.Equal(("Datum", new DateOnly(2026, 11, 9), erledigt), (montag.Ziel, montag.Datum!.Value, montag.ErledigtAm!.Value));
        Assert.Equal("Nächste Woche", merkzettel.Single(n => n.Text == "Von mir geteilt").Ziel);

        var rhythmen = export.Trainingsrhythmen!;
        Assert.Equal(2, rhythmen.Count);
        var meiner = rhythmen.Single(r => r.HundId == mein.Id);
        Assert.Equal(("Mittwoch", "Samstag", 18), (meiner.Wochentage[0], meiner.Wochentage[1], meiner.Uhrzeit!.Value));
    }

    [Fact]
    public async Task Export_NenntSchalterUndDieVersendetenMerkzettelErinnerungen()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        lookup.Register(ich, "ich@dogity.test", "Ich", "Selbst");
        db.UserPreferences.Add(new UserPreference { UserId = ich, ReminderTrainingNotes = false });
        var hundId = Guid.NewGuid();
        db.SentReminders.Add(new SentReminder { UserId = ich, Type = ReminderType.TrainingNote, ReferenceId = hundId, DayKey = new DateOnly(2026, 11, 4) });
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(ich)).Value!;

        Assert.False(export.Einstellungen!.ErinnerungMerkzettel);
        var erinnerung = Assert.Single(export.ErinnerungenVersendet);
        Assert.Equal(("Merkzettel vor dem Training", hundId, new DateOnly(2026, 11, 4)), (erinnerung.Art, erinnerung.Bezug!.Value, erinnerung.Tag!.Value));
    }

    [Fact]
    public async Task Export_OhneEintraege_LeereListen()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        lookup.Register(ich, "ich@dogity.test", "Ich", "Selbst");
        Hund(db, ich, "Bello");
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(ich)).Value!;

        Assert.Empty(export.Merkzettel!);
        Assert.Empty(export.Trainingsrhythmen!);
    }

    [Fact]
    public async Task Purge_EntferntDieMerkzettelUndRhythmenEigenerHundeWirklich_AuchWeichGeloeschte()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var fremd = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        var fremderHund = Hund(db, fremd, "Fremder");
        db.TrainingNotes.AddRange(
            Notiz(hund.Id, "Offen", ich),
            Notiz(hund.Id, "Gelöscht", ich, geloescht: true),
            Notiz(fremderHund.Id, "Fremd", fremd));
        db.DogTrainingRhythms.AddRange(
            new DogTrainingRhythm { DogId = hund.Id, Days = 4 },
            new DogTrainingRhythm { DogId = fremderHund.Id, Days = 4 });
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        Assert.Equal("Fremd", Assert.Single(db.TrainingNotes.IgnoreQueryFilters()).Text);
        Assert.Equal(fremderHund.Id, Assert.Single(db.DogTrainingRhythms.IgnoreQueryFilters()).DogId);
        Assert.Empty(db.Dogs.IgnoreQueryFilters().Where(d => d.Id == hund.Id));
    }

    [Fact]
    public async Task Purge_EintragAnGeteiltemHund_BleibtBeiDenAnderen_NurDerVerweisAufMichGeht_RhythmusBleibt()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = mitbesitzer });
        db.TrainingNotes.AddRange(Notiz(hund.Id, "Von mir", ich), Notiz(hund.Id, "Von ihm", mitbesitzer));
        db.DogTrainingRhythms.Add(new DogTrainingRhythm { DogId = hund.Id, Days = 4, Hour = 18 });
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        var zeilen = await db.TrainingNotes.IgnoreQueryFilters().ToDictionaryAsync(n => n.Text);
        Assert.Null(zeilen["Von mir"].UserId);
        Assert.Equal(mitbesitzer, zeilen["Von ihm"].UserId);
        Assert.Single(db.DogTrainingRhythms.IgnoreQueryFilters());
        Assert.NotNull(await db.Dogs.IgnoreQueryFilters().SingleOrDefaultAsync(d => d.Id == hund.Id));
    }

    [Fact]
    public async Task Purge_EintragDerMitbesitzerinAnMeinemAlleinigenHund_GehtMitDemHundWeg()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var ehemalig = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        db.TrainingNotes.Add(Notiz(hund.Id, "Von früher", ehemalig));
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        Assert.Empty(db.TrainingNotes.IgnoreQueryFilters());
    }
}
