using Dogity.Application.Account;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Account;

/// <summary>
/// Die Gesundheitseinträge der Hunde in Auskunft und Löschung (Art. 15, 17, 20 DSGVO): Der Export nennt
/// alle Einträge meiner Hunde (auch der geteilten, nie fremder), die Löschung entfernt sie endgültig -
/// auch die weich gelöschten - und lässt an geteilten Hunden die Einträge der übrigen Besitzer:innen stehen.
/// </summary>
public class AccountDataGesundheitTests
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

    private static DogHealthRecord Eintrag(Guid hund, DogHealthKind art, string titel, Guid? von = null, bool geloescht = false) => new()
    {
        DogId = hund, Kind = art, Title = titel, Date = new DateOnly(2026, 10, 1),
        DueDate = art == DogHealthKind.VetAppointment ? null : new DateOnly(2029, 10, 1),
        Time = art == DogHealthKind.VetAppointment ? new TimeOnly(10, 30) : null,
        Note = "Privat", CreatedByUserId = von, DeletedAt = geloescht ? DateTimeOffset.UtcNow : null,
    };

    [Fact]
    public async Task Export_EnthaeltDieEintraegeMeinerHunde_MitGeteilten_NichtDieFremder()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        lookup.Register(ich, "ich@dogity.test", "Ich", "Selbst");
        var mein = Hund(db, ich, "Bello");
        var geteilt = Hund(db, andere, "Emma");
        db.DogOwners.Add(new DogOwner { DogId = geteilt.Id, UserId = ich });
        var fremd = Hund(db, andere, "Fremder");
        db.DogHealthRecords.AddRange(
            Eintrag(mein.Id, DogHealthKind.Vaccination, "Tollwut", ich),
            Eintrag(mein.Id, DogHealthKind.VetAppointment, "Kontrolle", ich),
            Eintrag(geteilt.Id, DogHealthKind.Deworming, "Entwurmung", andere),
            Eintrag(fremd.Id, DogHealthKind.Vaccination, "Fremd", andere),
            Eintrag(mein.Id, DogHealthKind.Other, "Gelöscht", ich, geloescht: true));
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(ich)).Value!;

        var gesundheit = export.Gesundheit!;
        Assert.Equal(3, gesundheit.Count);
        Assert.DoesNotContain(gesundheit, e => e.Titel is "Fremd" or "Gelöscht");
        var impfung = gesundheit.Single(e => e.Titel == "Tollwut");
        Assert.Equal((mein.Id, "Impfung", new DateOnly(2029, 10, 1), "Privat"), (impfung.HundId, impfung.Art, impfung.GueltigBis!.Value, impfung.Notiz));
        var termin = gesundheit.Single(e => e.Titel == "Kontrolle");
        Assert.Equal(("Tierarzttermin", new TimeOnly(10, 30)), (termin.Art, termin.Uhrzeit!.Value));
        Assert.Null(termin.GueltigBis);
        Assert.Equal("Entwurmung", gesundheit.Single(e => e.Titel == "Entwurmung").Art);
    }

    [Fact]
    public async Task Export_NenntSchalterUndDieVersendetenGesundheitserinnerungen()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        lookup.Register(ich, "ich@dogity.test", "Ich", "Selbst");
        db.UserPreferences.Add(new UserPreference { UserId = ich, ReminderHealth = false });
        db.SentReminders.AddRange(
            new SentReminder { UserId = ich, Type = ReminderType.HealthDueAhead, ReferenceId = Guid.NewGuid(), DayKey = new DateOnly(2026, 11, 18) },
            new SentReminder { UserId = ich, Type = ReminderType.HealthDueToday, ReferenceId = Guid.NewGuid(), DayKey = new DateOnly(2026, 11, 18) },
            new SentReminder { UserId = ich, Type = ReminderType.HealthVetDayBefore, ReferenceId = Guid.NewGuid(), DayKey = new DateOnly(2026, 11, 5) });
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(ich)).Value!;

        Assert.False(export.Einstellungen!.ErinnerungGesundheit);
        Assert.Equal(
            ["Gesundheit: Frist heute", "Gesundheit: Frist in 14 Tagen", "Tierarzttermin morgen"],
            export.ErinnerungenVersendet.Select(e => e.Art).Order());
    }

    [Fact]
    public async Task Export_OhneEintraege_LeereListe()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        lookup.Register(ich, "ich@dogity.test", "Ich", "Selbst");
        Hund(db, ich, "Bello");
        await db.SaveChangesAsync();

        Assert.Empty((await dienst.ExportAsync(ich)).Value!.Gesundheit!);
    }

    [Fact]
    public async Task Purge_EntferntDieEintraegeEigenerHundeWirklich_AuchWeichGeloeschte()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var fremd = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        var fremderHund = Hund(db, fremd, "Fremder");
        db.DogHealthRecords.AddRange(
            Eintrag(hund.Id, DogHealthKind.Vaccination, "Tollwut", ich),
            Eintrag(hund.Id, DogHealthKind.Vaccination, "Alt", ich, geloescht: true),
            Eintrag(fremderHund.Id, DogHealthKind.Vaccination, "Fremd", fremd));
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        var uebrig = Assert.Single(db.DogHealthRecords.IgnoreQueryFilters());
        Assert.Equal("Fremd", uebrig.Title);
        Assert.Empty(db.Dogs.IgnoreQueryFilters().Where(d => d.Id == hund.Id));
    }

    [Fact]
    public async Task Purge_EintragEinesGeteiltenHundes_BleibtBeiDenAnderen_NurDerVerweisAufMichGeht()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = mitbesitzer });
        db.DogHealthRecords.Add(Eintrag(hund.Id, DogHealthKind.Vaccination, "Tollwut", ich));
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        var eintrag = Assert.Single(db.DogHealthRecords.IgnoreQueryFilters());
        Assert.Equal(hund.Id, eintrag.DogId);
        Assert.Null(eintrag.CreatedByUserId);
        Assert.NotNull(await db.Dogs.IgnoreQueryFilters().SingleOrDefaultAsync(d => d.Id == hund.Id));
    }

    [Fact]
    public async Task Purge_EintragDesMitbesitzersAnMeinemAlleinigenHund_GehtMitDemHundWeg()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var ehemalig = Guid.NewGuid();
        var hund = Hund(db, ich, "Bello");
        // Eine frühere Mitbesitzerin hat den Eintrag gemacht; der Hund gehört jetzt nur noch mir.
        db.DogHealthRecords.Add(Eintrag(hund.Id, DogHealthKind.Vaccination, "Tollwut", ehemalig));
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        Assert.Empty(db.DogHealthRecords.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Purge_EntferntDieMerkzettelDerGesundheitserinnerungen()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        db.SentReminders.AddRange(
            new SentReminder { UserId = ich, Type = ReminderType.HealthDueAhead, ReferenceId = Guid.NewGuid(), DayKey = new DateOnly(2026, 11, 18) },
            new SentReminder { UserId = andere, Type = ReminderType.HealthDueAhead, ReferenceId = Guid.NewGuid(), DayKey = new DateOnly(2026, 11, 18) });
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        Assert.Equal(andere, Assert.Single(db.SentReminders.IgnoreQueryFilters()).UserId);
    }
}
