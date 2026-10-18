using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Notifications;

/// <summary>
/// Welche Aufrufer eine Meldung als "wichtig" (also zusätzlich per Mail) anlegen:
/// Einladungen und Entscheidungen über Beitrittsanfragen - und nichts sonst.
///
/// Die Auswahl steht an den Aufrufern, nicht im NotificationService. Dieser Test
/// hält sie fest: Eine Meldung, die plötzlich als Mail rausgeht, ist Spam; eine
/// Einladung, die es nicht mehr tut, bleibt unbeantwortet liegen.
/// </summary>
public class WichtigeMeldungenTests
{
    // ---------------------------------------------------------------- Verein

    private sealed record VereinsAufbau(
        ApplicationDbContext Db, ClubService Dienst, FakeNotificationService Meldungen,
        Club Verein, Guid Verwaltung, Guid Person);

    private static async Task<VereinsAufbau> VereinAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var dienst = new ClubService(db, nutzer, meldungen, new TrainerRoleService(db, nutzer));
        var verein = new Club { Name = "Nord" };
        var verwaltung = Guid.NewGuid();
        var person = Guid.NewGuid();
        nutzer.Register(verwaltung, "verwaltung@example.org", "Vera", "Walter");
        nutzer.Register(person, "person@example.org", "Paula", "Person");
        db.Clubs.Add(verein);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = verwaltung, Role = ClubRole.Verwaltung });
        await db.SaveChangesAsync();
        return new VereinsAufbau(db, dienst, meldungen, verein, verwaltung, person);
    }

    [Fact]
    public async Task Vereinseinladung_IstWichtig()
    {
        var a = await VereinAsync();

        await a.Dienst.AddMemberAsync(a.Verwaltung, isAdmin: false, a.Verein.Id, new AssignClubMemberRequest("person@example.org"));

        var wichtig = Assert.Single(a.Meldungen.Wichtige);
        Assert.Equal(a.Person, wichtig.UserId);
    }

    [Fact]
    public async Task DirekteAufnahmeDurchDenBetreiber_IstWichtig()
    {
        var a = await VereinAsync();

        await a.Dienst.AddMemberAsync(Guid.NewGuid(), isAdmin: true, a.Verein.Id, new AssignClubMemberRequest("person@example.org"));

        Assert.Equal(a.Person, Assert.Single(a.Meldungen.Wichtige).UserId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EntscheidungUeberDieBeitrittsanfrage_IstWichtig_AngenommenWieAbgelehnt(bool annehmen)
    {
        var a = await VereinAsync();
        var anfrage = await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id);
        a.Meldungen.Created.Clear();

        await a.Dienst.DecideJoinRequestAsync(a.Verwaltung, a.Verein.Id, anfrage.Value!.Id, annehmen);

        Assert.Equal(a.Person, Assert.Single(a.Meldungen.Wichtige).UserId);
    }

    [Fact]
    public async Task BeitrittsanfrageAnDieTrainer_IstNichtWichtig()
    {
        var a = await VereinAsync();

        await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id);

        Assert.Single(a.Meldungen.Created);
        Assert.Empty(a.Meldungen.Wichtige);
    }

    [Fact]
    public async Task ErnennungZurTrainerin_IstNichtWichtig()
    {
        var a = await VereinAsync();
        var anfrage = await a.Dienst.RequestJoinAsync(a.Person, a.Verein.Id);
        await a.Dienst.DecideJoinRequestAsync(a.Verwaltung, a.Verein.Id, anfrage.Value!.Id, approve: true);
        a.Meldungen.Created.Clear();
        a.Meldungen.Wichtige.Clear();

        await a.Dienst.PromoteMemberToTrainerAsync(a.Verwaltung, a.Verein.Id, a.Person);

        Assert.Contains(a.Meldungen.Created, m => m.Message.Contains("Trainer"));
        Assert.Empty(a.Meldungen.Wichtige);
    }

    // ---------------------------------------------------------------- Gruppe

    private sealed record GruppenAufbau(
        ApplicationDbContext Db, GroupService Dienst, FakeNotificationService Meldungen,
        Guid Trainerin, Guid GruppeId, Guid Person);

    private static async Task<GruppenAufbau> GruppeAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var dienst = new GroupService(db, nutzer, new TrainerRoleService(db, nutzer), meldungen, TimeProvider.System);
        var trainerin = Guid.NewGuid();
        var person = Guid.NewGuid();
        nutzer.Register(trainerin, "tina@example.org", "Tina", "Trainer");
        nutzer.Register(person, "person@example.org", "Paula", "Person");
        var gruppe = new Group { TrainerId = trainerin, Name = "Dienstagsgruppe" };
        db.Groups.Add(gruppe);
        await db.SaveChangesAsync();
        return new GruppenAufbau(db, dienst, meldungen, trainerin, gruppe.Id, person);
    }

    [Fact]
    public async Task Gruppeneinladung_IstWichtig()
    {
        var a = await GruppeAsync();

        var ergebnis = await a.Dienst.AddMemberAsync(a.Trainerin, a.GruppeId, new AddMemberRequest("person@example.org"));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(a.Person, Assert.Single(a.Meldungen.Wichtige).UserId);
    }

    [Fact]
    public async Task MitTrainerEinladung_IstWichtig()
    {
        var a = await GruppeAsync();

        var ergebnis = await a.Dienst.AddGroupTrainerAsync(a.Trainerin, a.GruppeId, new AddGroupTrainerRequest("person@example.org"));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(a.Person, Assert.Single(a.Meldungen.Wichtige).UserId);
    }

    [Theory]
    [InlineData(true, "angenommen")]
    [InlineData(false, "abgelehnt")]
    public async Task EntscheidungUeberDenGruppenbeitritt_IstWichtig(bool annehmen, string wort)
    {
        var a = await GruppeAsync();
        await a.Dienst.RequestJoinGroupAsync(a.Person, a.GruppeId);

        await a.Dienst.DecideGroupJoinRequestAsync(a.Trainerin, a.GruppeId, a.Person, annehmen);

        var wichtig = Assert.Single(a.Meldungen.Wichtige);
        Assert.Equal(a.Person, wichtig.UserId);
        Assert.Contains(wort, wichtig.Message);
        Assert.Contains("Dienstagsgruppe", wichtig.Message);
    }

    // ------------------------------------------------------------------ Hund

    [Fact]
    public async Task MitbesitzEinladung_IstWichtig_DasEntfernenNicht()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var dienst = new DogService(db, nutzer, meldungen);
        var besitzerin = Guid.NewGuid();
        var mitbesitzer = Guid.NewGuid();
        nutzer.Register(besitzerin, "anna@example.org", "Anna", "Anfang");
        nutzer.Register(mitbesitzer, "mit@example.org", "Maria", "Mitbesitz");
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzerin, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();

        await dienst.AddOwnerAsync(besitzerin, hund.Id, new AddDogOwnerRequest("mit@example.org"));
        Assert.Equal(mitbesitzer, Assert.Single(meldungen.Wichtige).UserId);

        await dienst.RespondToInvitationAsync(mitbesitzer, hund.Id, accept: true);
        meldungen.Created.Clear();
        meldungen.Wichtige.Clear();
        await dienst.RemoveOwnerAsync(besitzerin, hund.Id, mitbesitzer);

        Assert.Single(meldungen.Created);
        Assert.Empty(meldungen.Wichtige);
    }
}
