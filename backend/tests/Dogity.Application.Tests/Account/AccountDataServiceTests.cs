using Dogity.Application.Account;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Learning;
using Dogity.Domain.Notifications;
using Dogity.Domain.Preferences;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Account;

/// <summary>
/// Auskunft und Löschung (Art. 15, 17, 20 DSGVO).
///
/// Die Löschtests prüfen absichtlich mit <c>IgnoreQueryFilters()</c> nach:
/// Ein Soft-Delete würde jeden Test bestehen lassen, der nur die normale
/// Abfrage benutzt - die Zeile ist dann ausgeblendet, aber eben noch da.
/// Genau das wäre der Fehler, den diese Tests verhindern sollen.
/// </summary>
public class AccountDataServiceTests
{
    private static (AccountDataService Dienst, ApplicationDbContext Db, FakeUserLookupService Lookup) Aufsetzen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        return (new AccountDataService(db, lookup), db, lookup);
    }

    /// <summary>Hund mit Training, Fährte und einem GPS-Punkt - der typische Datenbestand.</summary>
    private static Guid HundMitAllem(ApplicationDbContext db, Guid nutzer, string name)
    {
        var hund = new Dog { Name = name };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer });

        var einheit = new TrainingSession
        {
            UserId = nutzer,
            DogId = hund.Id,
            Date = new DateOnly(2026, 9, 1),
            DurationMinutes = 45,
            LocationName = "Hundeplatz",
        };
        db.TrainingSessions.Add(einheit);
        db.TrainingExercises.Add(new TrainingExercise
        {
            TrainingSessionId = einheit.Id,
            FreeTextLabel = "Fußarbeit",
            Rating = 4,
            Success = true,
        });

        var faehrte = new GpsTrack { TrainingSessionId = einheit.Id, LengthMeters = 300 };
        db.GpsTracks.Add(faehrte);
        db.GpsPoints.Add(new GpsPoint
        {
            TrackId = faehrte.Id,
            Latitude = 48.9,
            Longitude = 8.5,
            Timestamp = DateTimeOffset.UtcNow,
        });

        return hund.Id;
    }

    [Fact]
    public async Task Export_LiefertEigeneHundeUndTrainings()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        lookup.Register(nutzer, "ich@test.de", "Max", "Muster");
        HundMitAllem(db, nutzer, "Bello");
        await db.SaveChangesAsync();

        var ergebnis = await dienst.ExportAsync(nutzer);

        Assert.True(ergebnis.Succeeded);
        var export = ergebnis.Value!;
        Assert.Equal("ich@test.de", export.Konto.EMail);
        Assert.Equal("Bello", Assert.Single(export.Hunde).Name);
        var training = Assert.Single(export.Trainings);
        Assert.Equal("Hundeplatz", training.Ort);
        Assert.Equal("Fußarbeit", Assert.Single(training.Uebungen).Uebung);
        // Die Fährtenpunkte müssen mit - sie sind der Kern der Auskunft.
        Assert.Single(Assert.Single(export.Faehrten).Punkte);
    }

    [Fact]
    public async Task Export_EnthaeltKeineFremdenDaten()
    {
        var (dienst, db, lookup) = Aufsetzen();
        var ich = Guid.NewGuid();
        var andere = Guid.NewGuid();
        lookup.Register(ich, "ich@test.de");
        HundMitAllem(db, ich, "Bello");
        HundMitAllem(db, andere, "Fremder Hund");
        await db.SaveChangesAsync();

        var export = (await dienst.ExportAsync(ich)).Value!;

        Assert.Equal("Bello", Assert.Single(export.Hunde).Name);
        Assert.Single(export.Trainings);
    }

    [Fact]
    public async Task Export_UnbekanntesKonto_MeldetNichtGefunden()
    {
        var (dienst, _, _) = Aufsetzen();

        var ergebnis = await dienst.ExportAsync(Guid.NewGuid());

        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Purge_EntferntHundTrainingUndFaehrteWirklich()
    {
        var (dienst, db, _) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        HundMitAllem(db, nutzer, "Bello");
        db.UserPreferences.Add(new UserPreference { UserId = nutzer, Locale = "de" });
        db.Notifications.Add(new Notification { UserId = nutzer, Message = "Hallo" });
        db.QuizMasteries.Add(new QuizMastery { UserId = nutzer, QuestionId = Guid.NewGuid(), Box = 2 });
        await db.SaveChangesAsync();

        var ergebnis = await dienst.PurgeAsync(nutzer);

        Assert.True(ergebnis.Succeeded);
        // IgnoreQueryFilters: Ein Soft-Delete würde hier durchrutschen, ist
        // für Art. 17 DSGVO aber keine Löschung - der Name stünde weiter da.
        Assert.Empty(db.Dogs.IgnoreQueryFilters());
        Assert.Empty(db.DogOwners.IgnoreQueryFilters());
        Assert.Empty(db.TrainingSessions.IgnoreQueryFilters());
        Assert.Empty(db.TrainingExercises.IgnoreQueryFilters());
        Assert.Empty(db.GpsTracks.IgnoreQueryFilters());
        Assert.Empty(db.GpsPoints.IgnoreQueryFilters());
        Assert.Empty(db.UserPreferences.IgnoreQueryFilters());
        Assert.Empty(db.Notifications.IgnoreQueryFilters());
        Assert.Empty(db.QuizMasteries.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Purge_NimmtAuchWeichGeloeschteZeilenMit()
    {
        var (dienst, db, _) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        var hundId = HundMitAllem(db, nutzer, "Bello");
        await db.SaveChangesAsync();

        // Der Hund wurde schon einmal "gelöscht" - er ist nur ausgeblendet.
        // Ausgerechnet diese Zeilen dürfen beim Kontolöschen nicht liegen
        // bleiben; jemand wollte sie bereits los sein.
        var hund = await db.Dogs.IgnoreQueryFilters().FirstAsync(d => d.Id == hundId);
        hund.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(nutzer);

        Assert.Empty(db.Dogs.IgnoreQueryFilters());
        Assert.Empty(db.TrainingSessions.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Purge_GeteilterHundBleibtBeiDerAnderenPerson()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        var hundId = HundMitAllem(db, ich, "Bello");
        db.DogOwners.Add(new DogOwner { DogId = hundId, UserId = mitbesitzer });
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        // Der Hund gehört auch jemand anderem - er verschwindet nicht, nur
        // meine Verknüpfung damit.
        Assert.Single(db.Dogs.IgnoreQueryFilters().Where(d => d.Id == hundId));
        Assert.Empty(db.DogOwners.IgnoreQueryFilters().Where(o => o.UserId == ich));
        Assert.Single(db.DogOwners.IgnoreQueryFilters().Where(o => o.UserId == mitbesitzer));
        // Meine eigenen Einheiten sind trotzdem meine Daten und gehen mit.
        Assert.Empty(db.TrainingSessions.IgnoreQueryFilters().Where(s => s.UserId == ich));
    }

    [Fact]
    public async Task Purge_LoestAusVereinUndGruppe()
    {
        var (dienst, db, _) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        var aufbau = new Aufbau(db);
        aufbau.Vereinsmitglied(nutzer, ClubMembershipStatus.Pending);
        aufbau.Vereinstrainer(nutzer);
        aufbau.Gruppenmitglied(nutzer, GroupMemberStatus.Active);
        await aufbau.Speichern();

        await dienst.PurgeAsync(nutzer);

        // Sonst steht die offene Beitrittsanfrage für immer als "(unbekannt)"
        // in der Liste einer Trainerin, die sie nicht mehr auflösen kann.
        Assert.Empty(db.ClubMemberships.IgnoreQueryFilters().Where(m => m.UserId == nutzer));
        Assert.Empty(db.ClubTrainers.IgnoreQueryFilters().Where(t => t.UserId == nutzer));
        Assert.Empty(db.GroupMembers.IgnoreQueryFilters().Where(m => m.UserId == nutzer));
    }

    [Fact]
    public async Task Purge_UebergibtGruppeAnZweiteTrainerin()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var kollegin = Guid.NewGuid();
        var gruppe = new Group { Name = "Fährtengruppe", TrainerId = ich };
        db.Groups.Add(gruppe);
        db.GroupTrainers.Add(new GroupTrainer { GroupId = gruppe.Id, UserId = kollegin });
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        var danach = await db.Groups.IgnoreQueryFilters().FirstAsync(g => g.Id == gruppe.Id);
        Assert.Equal(kollegin, danach.TrainerId);
        Assert.Null(danach.DeletedAt);
    }

    [Fact]
    public async Task Purge_OhneNachfolgeWirdDieGruppeGeschlossen()
    {
        var (dienst, db, _) = Aufsetzen();
        var ich = Guid.NewGuid();
        var gruppe = new Group { Name = "Fährtengruppe", TrainerId = ich };
        db.Groups.Add(gruppe);
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(ich);

        // Ohne Trainer:in führt die Gruppe niemand mehr. Bewusst weich
        // gelöscht und nicht entfernt: An ihr hängen Termine und Übungen des
        // Vereins, die anderen gehören.
        var danach = await db.Groups.IgnoreQueryFilters().FirstAsync(g => g.Id == gruppe.Id);
        Assert.NotNull(danach.DeletedAt);
    }

    [Fact]
    public async Task Purge_LaeuftEinZweitesMalOhneFehler()
    {
        var (dienst, db, _) = Aufsetzen();
        var nutzer = Guid.NewGuid();
        HundMitAllem(db, nutzer, "Bello");
        await db.SaveChangesAsync();

        await dienst.PurgeAsync(nutzer);
        var zweiterLauf = await dienst.PurgeAsync(nutzer);

        Assert.True(zweiterLauf.Succeeded);
    }
}
