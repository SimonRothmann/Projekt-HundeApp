using Dogity.Application.Community;
using Dogity.Application.Planning;
using Dogity.Application.Stats;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// "Heute" ist der Kalendertag der Vereinszeit, nicht der UTC-Tag. Nachgebaut
/// mit einer Uhr, die im Sommer 23:30 UTC zeigt - das ist 01:30 Uhr deutscher
/// Zeit und damit schon der nächste Tag. Bis dahin hingen Zielstatus, Fristen,
/// Wochenübersicht und der Beginn einer Betreuung bis zu zwei Stunden nach.
/// </summary>
public class HeuteInOrtszeitTests
{
    // Dienstag 14.07.2026, 23:30 UTC = Mittwoch 15.07.2026, 01:30 Uhr deutscher Zeit.
    private static readonly DateTimeOffset NachtUtc = new(2026, 7, 14, 23, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly HeuteOrtszeit = new(2026, 7, 15);

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    [Fact]
    public async Task Ziel_DasHeutigeOrtszeitDatumIstKeineZukunft()
    {
        var db = InMemoryDbContext.Create();
        var dienst = new GoalService(db, new FesteUhr(NachtUtc), new FakeNotificationService(), new ExerciseMasteryService(db));
        var halter = Guid.NewGuid();
        var sport = new Sport { Code = "UPR", Name = "Unterordnung" };
        var hund = new Dog { Name = "Bello" };
        db.Sports.Add(sport);
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = halter, Role = DogOwnerRole.Owner });
        db.Exercises.Add(new Exercise { Name = "Freifolge", SportId = sport.Id, Difficulty = ExerciseDifficulty.Beginner });
        await db.SaveChangesAsync();

        // Nach UTC wäre erst der 14.: Der 15. läge in der Zukunft und würde angenommen.
        var heute = await dienst.CreateAsync(halter, new CreateGoalRequest(hund.Id, sport.Id, null, HeuteOrtszeit, null));
        var morgen = await dienst.CreateAsync(halter, new CreateGoalRequest(hund.Id, sport.Id, null, HeuteOrtszeit.AddDays(1), null));

        Assert.False(heute.Succeeded);
        Assert.True(morgen.Succeeded);
    }

    [Fact]
    public async Task Betreuung_BeginntAmOrtszeitTag()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var gruppen = new GroupService(db, lookup, new TrainerRoleService(db, lookup), new FakeNotificationService(), new FesteUhr(NachtUtc));

        var besitzer = Guid.NewGuid();
        var trainer = Guid.NewGuid();
        lookup.Register(besitzer, "anna@dogity.test", "Anna", "Anfang");
        lookup.Register(trainer, "tina@dogity.test", "Tina", "Trainer");
        var gruppe = new Group { TrainerId = trainer, Name = "Dienstagsgruppe" };
        var hund = new Dog { Name = "Bello" };
        db.Groups.Add(gruppe);
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer, Role = DogOwnerRole.Owner });
        db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Id, UserId = besitzer });
        await db.SaveChangesAsync();

        var ergebnis = await gruppen.AssignTrainerToDogAsync(trainer, gruppe.Id, new AssignTrainerRequest(besitzer, hund.Id));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(HeuteOrtszeit, db.TrainerAssignments.Single().StartDate);
    }

    [Fact]
    public async Task Statistik_ZaehltDieWocheNachOrtszeit()
    {
        // Sonntag 12.07.2026, 23:30 UTC = Montag 13.07., 01:30 Uhr deutscher Zeit:
        // nach UTC wäre es noch KW 28, nach Ortszeit schon KW 29.
        var montagNachts = new DateTimeOffset(2026, 7, 12, 23, 30, 0, TimeSpan.Zero);
        var db = InMemoryDbContext.Create();
        var dienst = new StatsService(db, new FesteUhr(montagNachts));
        var nutzer = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer });
        db.TrainingSessions.Add(new TrainingSession { UserId = nutzer, DogId = hund.Id, Date = new DateOnly(2026, 7, 13), DurationMinutes = 20 });
        await db.SaveChangesAsync();

        var woche = (await dienst.GetDashboardAsync(nutzer)).Value!.WeeklyActivity;

        Assert.Equal("2026-KW29", woche[^1].Week);
        Assert.Equal(1, woche[^1].Count);
    }

    [Fact]
    public async Task Statistik_OhneHund_ZeigtDieAktuelleOrtszeitWoche()
    {
        var montagNachts = new DateTimeOffset(2026, 7, 12, 23, 30, 0, TimeSpan.Zero);
        var dienst = new StatsService(InMemoryDbContext.Create(), new FesteUhr(montagNachts));

        var woche = (await dienst.GetDashboardAsync(Guid.NewGuid())).Value!.WeeklyActivity;

        Assert.Equal("2026-KW29", woche[^1].Week);
    }
}
