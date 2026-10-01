using Dogity.Application.Account;
using Dogity.Application.Admin;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Notifications;
using Dogity.Domain.Planning;
using Dogity.Domain.Preferences;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Admin;

/// <summary>
/// "Daten gelöschter Konten": Konten, die es nicht mehr gibt, aber noch Hunde,
/// Trainings und Verweise in der Datenbank hinterlassen haben (Altlast aus der
/// Zeit vor <c>AccountDataService.PurgeAsync</c>).
///
/// Geprüft wird nach der Bereinigung immer mit IgnoreQueryFilters - ein
/// bloßes Ausblenden bestünde jeden Test, der die gewöhnliche Abfrage benutzt.
/// Die Tests sichern vor allem die Gegenrichtung: Was einem vorhandenen Konto
/// gehört, bleibt unberührt, und ein Fehler beim Lesen der Konten löscht nichts.
/// </summary>
public class OrphanedDataTests
{
    private static (AdminService Dienst, ApplicationDbContext Db, FakeUserLookupService Lookup, Guid Admin) Aufsetzen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var admin = Guid.NewGuid();
        lookup.Register(admin, "admin@test.de");
        return (new AdminService(db, lookup, new FakeRefreshTokenService(), new AccountDataService(db, lookup)), db, lookup, admin);
    }

    /// <summary>Hund mit einem Besitzer, einem Training, einem Ziel und einer Benachrichtigung.</summary>
    private static Guid HundMitAllem(ApplicationDbContext db, Guid besitzer, bool hundGeloescht = false)
    {
        var hund = new Dog { Name = "Bello", DeletedAt = hundGeloescht ? DateTimeOffset.UtcNow : null };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        var einheit = new TrainingSession
        {
            UserId = besitzer, DogId = hund.Id, Date = new DateOnly(2026, 9, 1), DurationMinutes = 30,
        };
        db.TrainingSessions.Add(einheit);
        db.GpsTracks.Add(new GpsTrack { TrainingSessionId = einheit.Id });
        db.Goals.Add(new Goal { DogId = hund.Id, SportId = Guid.NewGuid(), TargetDate = new DateOnly(2026, 12, 1) });
        db.Notifications.Add(new Notification { UserId = besitzer, Message = "Hallo" });
        return hund.Id;
    }

    [Fact]
    public async Task Zaehlung_ErkenntVerwaisteDaten_OhnePersonenIds()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        var weg1 = Guid.NewGuid();
        var weg2 = Guid.NewGuid();
        HundMitAllem(db, vorhanden);
        HundMitAllem(db, weg1);
        HundMitAllem(db, weg2, hundGeloescht: true); // Hund schon weich gelöscht, Ziel steht noch
        db.UserPreferences.Add(new UserPreference { UserId = weg1, Locale = "de" });
        await db.SaveChangesAsync();

        var z = (await dienst.GetOrphanedDataAsync(admin)).Value!;

        Assert.Equal(2, z.Konten);
        Assert.Equal(2, z.Trainings);
        Assert.Equal(2, z.Hunde);
        Assert.Equal(2, z.Ziele);
        Assert.Equal(2, z.Benachrichtigungen);
        // Zwei Besitzzeilen und eine Einstellung.
        Assert.Equal(3, z.Sonstige);
    }

    [Fact]
    public async Task Zaehlung_OhneAltdaten_AllesNull()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        HundMitAllem(db, vorhanden);
        await db.SaveChangesAsync();

        var z = (await dienst.GetOrphanedDataAsync(admin)).Value!;

        Assert.Equal(new OrphanedDataDto(0, 0, 0, 0, 0, 0), z);
    }

    [Fact]
    public async Task Bereinigung_LoeschtVerwaisteDaten_EndgueltigUndLaesstVorhandeneKontenUnberuehrt()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        var weg = Guid.NewGuid();
        var meinHund = HundMitAllem(db, vorhanden);
        var seinHund = HundMitAllem(db, weg);
        // Alle Arten von Verweisen, nicht nur die Hundedaten.
        var verein = new Club { Name = "Verein" };
        db.Clubs.Add(verein);
        db.ClubMemberships.Add(new ClubMembership { ClubId = verein.Id, UserId = weg });
        db.ClubMemberships.Add(new ClubMembership { ClubId = verein.Id, UserId = vorhanden });
        db.UserPreferences.Add(new UserPreference { UserId = weg, Locale = "de" });
        db.UserPreferences.Add(new UserPreference { UserId = vorhanden, Locale = "de" });
        await db.SaveChangesAsync();

        var ergebnis = await dienst.PurgeOrphanedDataAsync(admin);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(1, ergebnis.Value!.Konten);

        // Das verwaiste Konto ist restlos weg ...
        Assert.DoesNotContain(db.Dogs.IgnoreQueryFilters(), d => d.Id == seinHund);
        Assert.Empty(db.DogOwners.IgnoreQueryFilters().Where(o => o.UserId == weg));
        Assert.Empty(db.TrainingSessions.IgnoreQueryFilters().Where(s => s.UserId == weg));
        Assert.Empty(db.Goals.IgnoreQueryFilters().Where(g => g.DogId == seinHund));
        Assert.Empty(db.Notifications.IgnoreQueryFilters().Where(n => n.UserId == weg));
        Assert.Empty(db.ClubMemberships.IgnoreQueryFilters().Where(m => m.UserId == weg));
        Assert.Empty(db.UserPreferences.IgnoreQueryFilters().Where(p => p.UserId == weg));
        Assert.Single(db.GpsTracks.IgnoreQueryFilters()); // nur die des vorhandenen Kontos

        // ... und das vorhandene Konto vollständig da.
        Assert.Single(db.Dogs.IgnoreQueryFilters().Where(d => d.Id == meinHund));
        Assert.Single(db.DogOwners.IgnoreQueryFilters().Where(o => o.UserId == vorhanden));
        Assert.Single(db.TrainingSessions.IgnoreQueryFilters().Where(s => s.UserId == vorhanden));
        Assert.Single(db.Goals.IgnoreQueryFilters().Where(g => g.DogId == meinHund));
        Assert.Single(db.Notifications.IgnoreQueryFilters().Where(n => n.UserId == vorhanden));
        Assert.Single(db.ClubMemberships.IgnoreQueryFilters().Where(m => m.UserId == vorhanden));
        Assert.Single(db.UserPreferences.IgnoreQueryFilters().Where(p => p.UserId == vorhanden));
    }

    [Fact]
    public async Task Bereinigung_HundMitVerwaistemUndVorhandenemBesitzer_BleibtErhalten()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        var weg = Guid.NewGuid();
        var hundId = HundMitAllem(db, vorhanden);
        db.DogOwners.Add(new DogOwner { DogId = hundId, UserId = weg });
        await db.SaveChangesAsync();

        var z = (await dienst.GetOrphanedDataAsync(admin)).Value!;
        await dienst.PurgeOrphanedDataAsync(admin);

        // Der Hund gehört auch jemandem, den es gibt - er geht nicht mit.
        Assert.Equal(0, z.Hunde);
        Assert.Single(db.Dogs.IgnoreQueryFilters().Where(d => d.Id == hundId));
        Assert.Single(db.DogOwners.IgnoreQueryFilters().Where(o => o.DogId == hundId && o.UserId == vorhanden));
        Assert.Empty(db.DogOwners.IgnoreQueryFilters().Where(o => o.UserId == weg));
        Assert.Single(db.TrainingSessions.IgnoreQueryFilters().Where(s => s.UserId == vorhanden));
    }

    [Fact]
    public async Task Bereinigung_HundZweierVerwaisterBesitzer_GehtKomplettMit()
    {
        var (dienst, db, _, admin) = Aufsetzen();
        var weg1 = Guid.NewGuid();
        var weg2 = Guid.NewGuid();
        var hundId = HundMitAllem(db, weg1);
        db.DogOwners.Add(new DogOwner { DogId = hundId, UserId = weg2 });
        await db.SaveChangesAsync();

        var z = (await dienst.GetOrphanedDataAsync(admin)).Value!;
        var ergebnis = await dienst.PurgeOrphanedDataAsync(admin);

        Assert.Equal(1, z.Hunde);
        Assert.Equal(2, ergebnis.Value!.Konten);
        Assert.Empty(db.Dogs.IgnoreQueryFilters());
        Assert.Empty(db.DogOwners.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Bereinigung_IstIdempotent()
    {
        var (dienst, db, _, admin) = Aufsetzen();
        HundMitAllem(db, Guid.NewGuid());
        await db.SaveChangesAsync();

        Assert.Equal(1, (await dienst.PurgeOrphanedDataAsync(admin)).Value!.Konten);
        var zweiter = await dienst.PurgeOrphanedDataAsync(admin);

        Assert.True(zweiter.Succeeded);
        Assert.Equal(0, zweiter.Value!.Konten);
    }

    [Fact]
    public async Task LeereKontoListe_MeldetUndLoeschtNichts()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService(); // kein einziges Konto
        var dienst = new AdminService(db, lookup, new FakeRefreshTokenService(), new AccountDataService(db, lookup));
        HundMitAllem(db, Guid.NewGuid());
        await db.SaveChangesAsync();

        var zaehlung = await dienst.GetOrphanedDataAsync(Guid.NewGuid());
        var bereinigung = await dienst.PurgeOrphanedDataAsync(Guid.NewGuid());

        // Sonst löschte ein Fehler beim Lesen der Konten die ganze Datenbank.
        Assert.False(zaehlung.Succeeded);
        Assert.False(bereinigung.Succeeded);
        Assert.Single(db.Dogs.IgnoreQueryFilters());
        Assert.Single(db.DogOwners.IgnoreQueryFilters());
        Assert.Single(db.TrainingSessions.IgnoreQueryFilters());
    }

    [Fact]
    public async Task KontoListeOhneDieAufrufendePerson_GiltAlsUnbrauchbar()
    {
        var (dienst, db, lookup, _) = Aufsetzen();
        var anderes = Guid.NewGuid();
        lookup.Register(anderes, "x@test.de");
        HundMitAllem(db, Guid.NewGuid());
        await db.SaveChangesAsync();

        // Die aufrufende Person ist angemeldet, muss also in der Liste stehen.
        var ergebnis = await dienst.PurgeOrphanedDataAsync(Guid.NewGuid());

        Assert.False(ergebnis.Succeeded);
        Assert.Single(db.Dogs.IgnoreQueryFilters());
    }

    [Fact]
    public async Task FehlerBeimLesenDerKonten_LoeschtNichts()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        HundMitAllem(db, Guid.NewGuid());
        await db.SaveChangesAsync();
        lookup.AuflistenSchlaegtFehl = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.PurgeOrphanedDataAsync(admin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.GetOrphanedDataAsync(admin));

        Assert.Single(db.Dogs.IgnoreQueryFilters());
        Assert.Single(db.TrainingSessions.IgnoreQueryFilters());
    }

    [Fact]
    public async Task DieAufrufendeAdminPerson_IstNieVerwaist()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        // Der Admin hat selbst Daten - er ist ein Konto, das es gibt.
        HundMitAllem(db, admin);
        await db.SaveChangesAsync();

        var z = (await dienst.GetOrphanedDataAsync(admin)).Value!;
        var ergebnis = await dienst.PurgeOrphanedDataAsync(admin);

        Assert.Equal(0, z.Konten);
        Assert.Equal(0, ergebnis.Value!.Konten);
        Assert.Single(db.Dogs.IgnoreQueryFilters());
        Assert.Single(db.TrainingSessions.IgnoreQueryFilters().Where(s => s.UserId == admin));
    }

    [Fact]
    public async Task Bereinigung_HaeltKontenZurueck_DerenHundTrainingsEinesVorhandenenKontosTraegt()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        var weg = Guid.NewGuid();
        var wegOhneFremdes = Guid.NewGuid();
        var hundMitFremdem = HundMitAllem(db, weg);
        HundMitAllem(db, wegOhneFremdes);
        // Ehemalige:r Mitbesitzer:in: Das Training steht noch am Hund, das Konto gibt es.
        db.TrainingSessions.Add(new TrainingSession
        {
            UserId = vorhanden, DogId = hundMitFremdem, Date = new DateOnly(2026, 8, 1), DurationMinutes = 20,
        });
        await db.SaveChangesAsync();

        var z = (await dienst.GetOrphanedDataAsync(admin)).Value!;
        var ergebnis = await dienst.PurgeOrphanedDataAsync(admin);

        // Nur das Konto ohne fremde Trainings wird gezählt und bereinigt.
        Assert.Equal(1, z.Konten);
        Assert.Equal(1, z.Hunde);
        Assert.Equal(1, ergebnis.Value!.Konten);
        Assert.Single(db.Dogs.IgnoreQueryFilters().Where(d => d.Id == hundMitFremdem));
        Assert.Single(db.TrainingSessions.IgnoreQueryFilters().Where(s => s.UserId == vorhanden));
        Assert.Single(db.DogOwners.IgnoreQueryFilters().Where(o => o.UserId == weg));
        Assert.Empty(db.DogOwners.IgnoreQueryFilters().Where(o => o.UserId == wegOhneFremdes));
    }

    [Fact]
    public async Task Bereinigung_HaeltAuchDenZweitenVerwaistenBesitzerZurueck_WennDerHundFremdeTrainingsTraegt()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        var weg1 = Guid.NewGuid();
        var weg2 = Guid.NewGuid();
        var hund = HundMitAllem(db, weg1);
        db.DogOwners.Add(new DogOwner { DogId = hund, UserId = weg2 });
        db.TrainingSessions.Add(new TrainingSession
        {
            UserId = vorhanden, DogId = hund, Date = new DateOnly(2026, 8, 1), DurationMinutes = 20,
        });
        await db.SaveChangesAsync();

        var ergebnis = await dienst.PurgeOrphanedDataAsync(admin);

        Assert.Equal(0, ergebnis.Value!.Konten);
        Assert.Single(db.Dogs.IgnoreQueryFilters());
        Assert.Single(db.TrainingSessions.IgnoreQueryFilters().Where(s => s.UserId == vorhanden));
    }

    [Fact]
    public async Task Bereinigung_ErkenntUndLoeschtGruppenTrainerUndZuweisungen()
    {
        var (dienst, db, lookup, admin) = Aufsetzen();
        var vorhanden = Guid.NewGuid();
        lookup.Register(vorhanden, "da@test.de");
        var weg = Guid.NewGuid();
        var gruppeWeg = new Group { Name = "Alt", TrainerId = weg };
        var gruppeDa = new Group { Name = "Neu", TrainerId = vorhanden };
        db.Groups.AddRange(gruppeWeg, gruppeDa);
        db.GroupMembers.Add(new GroupMember { GroupId = gruppeDa.Id, UserId = weg });
        db.GroupMembers.Add(new GroupMember { GroupId = gruppeDa.Id, UserId = vorhanden });
        db.GroupTrainers.Add(new GroupTrainer { GroupId = gruppeDa.Id, UserId = weg });
        db.TrainerAssignments.Add(new TrainerAssignment
        {
            TrainerId = weg, MemberId = vorhanden, DogId = Guid.NewGuid(), StartDate = new DateOnly(2026, 8, 1),
        });
        await db.SaveChangesAsync();

        var z = (await dienst.GetOrphanedDataAsync(admin)).Value!;
        await dienst.PurgeOrphanedDataAsync(admin);

        Assert.Equal(1, z.Konten);
        // Mitglied, Trainer-Zeile, eigene Gruppe, Zuweisung.
        Assert.Equal(4, z.Sonstige);
        // Ohne zweite Trainer:in wird die Gruppe geschlossen (wie bei jeder Kontolöschung).
        Assert.NotNull((await db.Groups.IgnoreQueryFilters().SingleAsync(g => g.Id == gruppeWeg.Id)).DeletedAt);
        // Die geschlossene Gruppe hält das Konto nicht länger "verwaist".
        Assert.Equal(0, (await dienst.GetOrphanedDataAsync(admin)).Value!.Konten);
        Assert.Empty(db.GroupMembers.IgnoreQueryFilters().Where(m => m.UserId == weg));
        Assert.Empty(db.GroupTrainers.IgnoreQueryFilters().Where(t => t.UserId == weg));
        Assert.Empty(db.TrainerAssignments.IgnoreQueryFilters());
        // Das vorhandene Konto behält Gruppe und Mitgliedschaft.
        Assert.Single(db.Groups.IgnoreQueryFilters().Where(g => g.Id == gruppeDa.Id));
        Assert.Single(db.GroupMembers.IgnoreQueryFilters().Where(m => m.UserId == vorhanden));
    }
}
