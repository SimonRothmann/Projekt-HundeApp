using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// <see cref="GroupTrainingSessionDto.KannVerwalten"/>: Die Terminliste zeigt auch Termine
/// fremder Gruppen, in die jemand als Termin-Trainer:in eingetragen ist; ändern, absagen und
/// löschen darf nur, wer im Verein Trainer:in ist UND die Gruppe verwaltet. Das Feld folgt
/// derselben Regel wie die Aktionen - die Knöpfe erscheinen genau dort, wo der Server mitmacht.
/// </summary>
public class TerminKannVerwaltenTests
{
    private sealed record Welt(
        ApplicationDbContext Db,
        GroupTrainingScheduleService Termine,
        Club Verein,
        Guid LeitungA,
        Guid LeitungB,
        GroupTrainingSession TerminA,
        GroupTrainingSession TerminB);

    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.UtcNow);

    // Zwei Gruppen im Verein, je mit eigener Leitung (Rolle Training). Im Termin der Gruppe B
    // ist Leitung A als Termin-Trainer:in eingetragen - so sieht sie ihn in ihrer Liste.
    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var verein = new Club { Name = "Hundesportverein Nord" };
        db.Clubs.Add(verein);
        var leitungA = Guid.NewGuid();
        var leitungB = Guid.NewGuid();
        db.ClubTrainers.AddRange(
            new ClubTrainer { ClubId = verein.Id, UserId = leitungA, Role = ClubRole.Training },
            new ClubTrainer { ClubId = verein.Id, UserId = leitungB, Role = ClubRole.Training });
        var gruppeA = new Group { ClubId = verein.Id, TrainerId = leitungA, Name = "Gruppe A" };
        var gruppeB = new Group { ClubId = verein.Id, TrainerId = leitungB, Name = "Gruppe B" };
        db.Groups.AddRange(gruppeA, gruppeB);
        var terminA = new GroupTrainingSession { ClubId = verein.Id, GroupId = gruppeA.Id, StartsAt = DateTimeOffset.UtcNow.AddDays(2), DurationMinutes = 60 };
        var terminB = new GroupTrainingSession { ClubId = verein.Id, GroupId = gruppeB.Id, StartsAt = DateTimeOffset.UtcNow.AddDays(3), DurationMinutes = 60 };
        db.GroupTrainingSessions.AddRange(terminA, terminB);
        db.GroupTrainingSessionTrainers.Add(new GroupTrainingSessionTrainer { GroupTrainingSessionId = terminB.Id, UserId = leitungA });
        await db.SaveChangesAsync();
        return new Welt(db, new GroupTrainingScheduleService(db, new FakeUserLookupService(), new FakeNotificationService()),
            verein, leitungA, leitungB, terminA, terminB);
    }

    private static async Task<IReadOnlyList<GroupTrainingSessionDto>> VereinslisteAsync(Welt w, Guid nutzer) =>
        (await w.Termine.GetClubScheduleAsync(nutzer, w.Verein.Id, Heute, null, null, null, false)).Value!;

    [Fact]
    public async Task TrainerinMitNurTraining_SiehtFremdenTermin_AberOhneVerwalten()
    {
        var w = await AufbauenAsync();

        var liste = await VereinslisteAsync(w, w.LeitungA);

        Assert.True(liste.Single(t => t.Id == w.TerminA.Id).KannVerwalten);
        Assert.False(liste.Single(t => t.Id == w.TerminB.Id).KannVerwalten);
    }

    [Fact]
    public async Task KannVerwalten_FolgtDerRegelDerAktionen()
    {
        var w = await AufbauenAsync();

        // Der Server lehnt Ändern, Absagen und Löschen für den fremden Termin mit 404 ab ...
        var aendern = new UpdateSessionRequest(GroupTrainingCategory.Puppy, w.TerminB.StartsAt, 60, null, null, [], []);
        Assert.True((await w.Termine.UpdateSessionAsync(w.LeitungA, w.TerminB.Id, aendern)).IsNotFound);
        Assert.True((await w.Termine.CancelSessionAsync(w.LeitungA, w.TerminB.Id)).IsNotFound);
        Assert.True((await w.Termine.DeleteSessionAsync(w.LeitungA, w.TerminB.Id)).IsNotFound);

        // ... und beim eigenen geht es durch: genau da steht das Feld auf true.
        Assert.True((await w.Termine.CancelSessionAsync(w.LeitungA, w.TerminA.Id)).Succeeded);
        Assert.True((await VereinslisteAsync(w, w.LeitungA)).Single(t => t.Id == w.TerminA.Id).KannVerwalten);
    }

    [Theory]
    [InlineData(ClubRole.Ausbildungswart)]
    [InlineData(ClubRole.Verwaltung)]
    public async Task WerAlleGruppenVerwaltet_DarfJedenTerminVerwalten(ClubRole rolle)
    {
        var w = await AufbauenAsync();
        var person = Guid.NewGuid();
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = person, Role = rolle });
        await w.Db.SaveChangesAsync();

        var liste = await VereinslisteAsync(w, person);

        Assert.Equal(2, liste.Count);
        Assert.All(liste, t => Assert.True(t.KannVerwalten));
    }

    [Fact]
    public async Task Mitglied_UndMitgliedDerFremdenGruppe_BekommenFalse()
    {
        var w = await AufbauenAsync();
        var mitglied = Guid.NewGuid();
        w.Db.GroupMembers.AddRange(
            new GroupMember { GroupId = w.TerminA.GroupId, UserId = mitglied, Status = GroupMemberStatus.Active },
            // Leitung A ist zugleich Mitglied in Gruppe B: als Mitglied darf sie dort nichts verwalten.
            new GroupMember { GroupId = w.TerminB.GroupId, UserId = w.LeitungA, Status = GroupMemberStatus.Active });
        await w.Db.SaveChangesAsync();

        var terminDesMitglieds = (await w.Termine.GetMemberScheduleAsync(mitglied, Heute)).Value!;
        var terminListeDerLeitung = (await w.Termine.GetMemberScheduleAsync(w.LeitungA, Heute)).Value!;

        Assert.False(Assert.Single(terminDesMitglieds).KannVerwalten);
        Assert.False(terminListeDerLeitung.Single(t => t.Id == w.TerminB.Id).KannVerwalten);
    }
}
