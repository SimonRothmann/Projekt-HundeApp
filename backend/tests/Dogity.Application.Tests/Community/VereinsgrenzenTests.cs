using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Wächter für die Trennung zwischen Vereinen. Ein zweiter, fremder Verein
/// nutzt die App: Seine Trainer:in (TB, Verwaltung von Verein B) darf von
/// Verein A, dessen Gruppe, Mitgliedern, Hunden und Tagebüchern nichts sehen
/// und nichts ändern - und kann niemanden ohne Zustimmung in Verein B holen.
///
/// Jeder Dienst läuft gegen den echten DbContext mit Soft-Delete-Filtern. Wer
/// eine neue Abfrage oder Aktion rund um Vereine, Gruppen oder Hunde einführt,
/// ergänzt sie hier - und bricht dieser Test, wurde eine Grenze verschoben.
/// Das Rechtemodell steht in docs/BERECHTIGUNGEN.md.
/// </summary>
public class VereinsgrenzenTests
{
    private const string MailTa = "ta@verein-a.example";
    private const string MailTb = "tb@verein-b.example";
    private const string MailMa = "ma@verein-a.example";

    private sealed record Welt(
        ApplicationDbContext Db,
        FakeUserLookupService Nutzer,
        FakeNotificationService Meldungen,
        ClubService Vereine,
        ClubInviteService Einladungslink,
        GroupService Gruppen,
        GroupTrainingScheduleService Termine,
        GroupTrainingService Bibliothek,
        GroupRegistrationService Anmeldungen,
        DogService Hunde,
        TrainingService Training,
        GoalService Ziele,
        GpsTrackService Faehrten,
        Club A, Club B, Group GruppeA,
        Guid TA, Guid TB, Guid MA, Guid HundA, Guid TrainingA, Guid Interessent, Guid AnfrageId);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var rollen = new TrainerRoleService(db, nutzer);
        var mastery = new ExerciseMasteryService(db);

        var ta = Guid.NewGuid();
        var tb = Guid.NewGuid();
        var ma = Guid.NewGuid();
        var interessent = Guid.NewGuid();
        nutzer.Register(ta, MailTa, "Tina", "Alt");
        nutzer.Register(tb, MailTb, "Timo", "Bauer");
        nutzer.Register(ma, MailMa, "Marta", "Anders");
        nutzer.Register(interessent, "int@example.org", "Ida", "Neugier");

        var a = new Club { Name = "Verein A" };
        var b = new Club { Name = "Verein B" };
        var gruppe = new Group { ClubId = a.Id, TrainerId = ta, Name = "Gruppe A" };
        db.Clubs.AddRange(a, b);
        db.Groups.Add(gruppe);
        db.ClubTrainers.AddRange(
            new ClubTrainer { ClubId = a.Id, UserId = ta, Role = ClubRole.Verwaltung },
            new ClubTrainer { ClubId = b.Id, UserId = tb, Role = ClubRole.Verwaltung });
        db.ClubMemberships.Add(new ClubMembership { ClubId = a.Id, UserId = ma, Status = ClubMembershipStatus.Approved });
        // Eine offene Anfrage in A, an der TB nichts entscheiden darf.
        var anfrage = new ClubMembership { ClubId = a.Id, UserId = interessent, Status = ClubMembershipStatus.Pending };
        db.ClubMemberships.Add(anfrage);
        db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Id, UserId = ma, Status = GroupMemberStatus.Active });

        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = ma, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = hund.Id, TrainerId = ta, MemberId = ma });

        var training = new TrainingSession
        {
            UserId = ma, DogId = hund.Id, Date = Vereinszeit.Heute().AddDays(-3), DurationMinutes = 30,
            Exercises = { new TrainingExercise { FreeTextLabel = "Sitz" } },
        };
        db.TrainingSessions.Add(training);
        db.Goals.Add(new Goal
        {
            DogId = hund.Id, SportId = Guid.NewGuid(), TargetDate = Vereinszeit.Heute().AddDays(60), Status = GoalStatus.Active,
        });
        db.GpsTracks.Add(new GpsTrack { TrainingSessionId = training.Id });
        await db.SaveChangesAsync();

        var vereine = new ClubService(db, nutzer, meldungen, rollen);
        return new Welt(
            db, nutzer, meldungen, vereine,
            new ClubInviteService(db, vereine),
            new GroupService(db, nutzer, rollen, meldungen),
            new GroupTrainingScheduleService(db, nutzer, meldungen),
            new GroupTrainingService(db),
            new GroupRegistrationService(db),
            new DogService(db, nutzer, meldungen),
            new TrainingService(db, meldungen, nutzer, mastery, new FakeWeatherEnrichmentService()),
            new GoalService(db, TimeProvider.System, meldungen, mastery),
            new GpsTrackService(db, new FakeWeatherEnrichmentService()),
            a, b, gruppe, ta, tb, ma, hund.Id, training.Id, interessent, anfrage.Id);
    }

    /// <summary>Verweigert - und zwar verdeckt: "gibt es nicht" statt "darfst du nicht".</summary>
    private static void KeinZugriff(Result ergebnis)
    {
        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound, $"Erwartet 404-artige Antwort, war: {string.Join(" | ", ergebnis.Errors)}");
    }

    /// <summary>
    /// Die Aktion scheitert, gleich in welcher Form. Einige ältere Dienste (Mitglied
    /// einer Gruppe, Termine, Bibliothek) antworten mit einem fachlichen Fehler statt
    /// mit "nicht gefunden" - verraten dabei aber nichts und ändern nichts.
    /// </summary>
    private static void Scheitert(Result ergebnis) => Assert.False(ergebnis.Succeeded);

    // ---------------------------------------------------------------- Verein

    [Fact]
    public async Task Kontrolle_DieTrainerinDesVereinsSelbstHatZugriff()
    {
        // Ohne diese Gegenprobe würden die Tests unten auch bestehen, wenn der
        // Aufbau selbst falsch wäre.
        var w = await AufbauenAsync();

        Assert.Equal(w.MA, Assert.Single((await w.Vereine.GetMembersAsync(w.TA, w.A.Id)).Value!).UserId);
        Assert.Equal(w.Interessent, Assert.Single((await w.Vereine.GetJoinRequestsAsync(w.TA, w.A.Id)).Value!).UserId);
        Assert.True((await w.Gruppen.GetDetailAsync(w.TA, w.GruppeA.Id)).Succeeded);
        Assert.True((await w.Hunde.GetByIdAsync(w.MA, w.HundA)).Succeeded);
        Assert.True((await w.Hunde.GetByIdAsync(w.TA, w.HundA)).Succeeded);
        Assert.Single((await w.Training.GetByDogAsync(w.TA, w.HundA)).Value!);
        Assert.True((await w.Einladungslink.GetAsync(w.TA, w.A.Id)).Succeeded);
    }

    [Fact]
    public async Task Kontrolle_DieTrainerinDesVereinsSchaftAuchDieAufrufeDieTbVerwehrtWerden()
    {
        // Gegenprobe zu Scheitert(...) unten: Dort zählt nur "es scheitert". Schaffen
        // dieselben Aufrufe mit TA, liegt das Scheitern bei TB an der fehlenden
        // Berechtigung und nicht an Testdaten oder Validierung.
        var w = await AufbauenAsync();

        Assert.True((await w.Termine.GetClubScheduleAsync(w.TA, w.A.Id, Vereinszeit.Heute(), null, null, null, mineOnly: false)).Succeeded);
        Assert.True((await w.Bibliothek.GetLibraryAsync(w.TA, w.A.Id)).Succeeded);
        Assert.True((await w.Anmeldungen.ListAsync(w.TA, w.GruppeA.Id)).Succeeded);
        Assert.True((await w.Gruppen.GetMemberDogsAsync(w.TA, w.GruppeA.Id, w.MA)).Succeeded);

        var einheit = await w.Termine.CreateSessionAsync(
            w.TA, w.A.Id,
            new CreateSessionRequest(w.GruppeA.Id, GroupTrainingCategory.Basis, DateTimeOffset.UtcNow.AddDays(3), 60, null, null, [], []));
        Assert.True(einheit.Succeeded, string.Join(" | ", einheit.Errors));
        Assert.True(await w.Db.GroupTrainingSessions.AnyAsync(s => s.ClubId == w.A.Id));

        // Betreuung: Die bestehende wird beendet, damit die Zuweisung nicht schon
        // an "betreust du bereits" scheitert, sondern wirklich neu eingetragen wird.
        (await w.Db.TrainerAssignments.SingleAsync(t => t.TrainerId == w.TA)).DeletedAt = DateTimeOffset.UtcNow;
        await w.Db.SaveChangesAsync();
        var betreuung = await w.Gruppen.AssignTrainerToDogAsync(w.TA, w.GruppeA.Id, new AssignTrainerRequest(w.MA, w.HundA));
        Assert.True(betreuung.Succeeded, string.Join(" | ", betreuung.Errors));
        Assert.True(await w.Db.TrainerAssignments.AnyAsync(t => t.TrainerId == w.TA && t.DogId == w.HundA));
    }

    [Fact]
    public async Task Verein_FremdeVerwaltungSiehtWederMitgliederNochAnfragenNochEinladungen()
    {
        var w = await AufbauenAsync();

        KeinZugriff(await w.Vereine.GetMembersAsync(w.TB, w.A.Id));
        KeinZugriff(await w.Vereine.GetJoinRequestsAsync(w.TB, w.A.Id));
        KeinZugriff(await w.Vereine.GetInvitationsAsync(w.TB, w.A.Id));
        KeinZugriff(await w.Einladungslink.GetAsync(w.TB, w.A.Id));
    }

    [Fact]
    public async Task Verein_FremdeVerwaltungKannNichtsAendern()
    {
        var w = await AufbauenAsync();

        KeinZugriff(await w.Vereine.DecideJoinRequestAsync(w.TB, w.A.Id, w.AnfrageId, approve: true));
        KeinZugriff(await w.Vereine.UpdateClubAsync(w.TB, isAdmin: false, w.A.Id, new UpdateClubRequest("Gekapert", null)));
        KeinZugriff(await w.Vereine.AssignTrainerAsync(w.TB, isAdmin: false, w.A.Id, new AssignClubTrainerRequest(MailMa)));
        KeinZugriff(await w.Vereine.RemoveTrainerAsync(w.TB, isAdmin: false, w.A.Id, w.TA));
        KeinZugriff(await w.Vereine.UpdateTrainerRoleAsync(w.TB, isAdmin: false, w.A.Id, w.TA, ClubRole.Training));
        KeinZugriff(await w.Vereine.PromoteMemberToTrainerAsync(w.TB, w.A.Id, w.MA));
        KeinZugriff(await w.Vereine.AddMemberAsync(w.TB, isAdmin: false, w.A.Id, new AssignClubMemberRequest(MailMa)));
        KeinZugriff(await w.Vereine.WithdrawInvitationAsync(w.TB, w.A.Id, Guid.NewGuid()));

        // Und es hat sich wirklich nichts bewegt.
        Assert.Equal("Verein A", (await w.Db.Clubs.SingleAsync(c => c.Id == w.A.Id)).Name);
        Assert.Equal(ClubMembershipStatus.Pending, (await w.Db.ClubMemberships.SingleAsync(m => m.Id == w.AnfrageId)).Status);
        Assert.True(await w.Db.ClubTrainers.AnyAsync(t => t.ClubId == w.A.Id && t.UserId == w.TA && t.Role == ClubRole.Verwaltung));
        Assert.False(await w.Db.ClubTrainers.AnyAsync(t => t.ClubId == w.A.Id && t.UserId == w.MA));
        Assert.Empty(w.Meldungen.Created);
    }

    // ---------------------------------------------------------------- Gruppe

    [Fact]
    public async Task Gruppe_FremdeVerwaltungSiehtUndAendertNichts()
    {
        var w = await AufbauenAsync();

        KeinZugriff(await w.Gruppen.GetGroupsByClubAsync(w.TB, w.A.Id));
        KeinZugriff(await w.Gruppen.GetDetailAsync(w.TB, w.GruppeA.Id));
        KeinZugriff(await w.Gruppen.UpdateGroupAsync(w.TB, w.GruppeA.Id, new UpdateGroupRequest("Gekapert", null)));
        KeinZugriff(await w.Gruppen.AddMemberAsync(w.TB, w.GruppeA.Id, new AddMemberRequest(MailMa)));
        KeinZugriff(await w.Gruppen.RemoveMemberAsync(w.TB, w.GruppeA.Id, w.MA));
        Scheitert(await w.Gruppen.GetMemberDogsAsync(w.TB, w.GruppeA.Id, w.MA));
        Scheitert(await w.Gruppen.AssignTrainerToDogAsync(w.TB, w.GruppeA.Id, new AssignTrainerRequest(w.MA, w.HundA)));
        KeinZugriff(await w.Gruppen.GetGroupJoinRequestsAsync(w.TB, w.GruppeA.Id));

        var meine = (await w.Gruppen.GetMyGroupsAsync(w.TB)).Value!;
        Assert.DoesNotContain(meine, g => g.Id == w.GruppeA.Id);

        Assert.Equal("Gruppe A", (await w.Db.Groups.SingleAsync(g => g.Id == w.GruppeA.Id)).Name);
        Assert.True(await w.Db.GroupMembers.AnyAsync(m => m.GroupId == w.GruppeA.Id && m.UserId == w.MA && m.Status == GroupMemberStatus.Active));
        // TB hat sich nicht als Betreuer:in von MAs Hund eingetragen.
        Assert.False(await w.Db.TrainerAssignments.AnyAsync(a => a.TrainerId == w.TB));
    }

    [Fact]
    public async Task Gruppentraining_FremdeVerwaltungSiehtUndPlantNichts()
    {
        var w = await AufbauenAsync();

        Scheitert(await w.Termine.GetClubScheduleAsync(w.TB, w.A.Id, Vereinszeit.Heute(), null, null, null, mineOnly: false));
        Scheitert(await w.Termine.CreateSessionAsync(
            w.TB, w.A.Id,
            new CreateSessionRequest(w.GruppeA.Id, GroupTrainingCategory.Basis, DateTimeOffset.UtcNow.AddDays(3), 60, null, null, [], [])));
        Scheitert(await w.Bibliothek.GetLibraryAsync(w.TB, w.A.Id));
        KeinZugriff(await w.Anmeldungen.ListAsync(w.TB, w.GruppeA.Id));

        Assert.False(await w.Db.GroupTrainingSessions.AnyAsync());
    }

    // ------------------------------------------------------- Hund und Tagebuch

    [Fact]
    public async Task Hund_FremdeVerwaltungKommtNichtInsTagebuch()
    {
        var w = await AufbauenAsync();

        KeinZugriff(await w.Hunde.GetByIdAsync(w.TB, w.HundA));
        KeinZugriff(await w.Training.GetByDogAsync(w.TB, w.HundA));
        KeinZugriff(await w.Training.GetByIdAsync(w.TB, w.TrainingA));
        KeinZugriff(await w.Ziele.GetByDogAsync(w.TB, w.HundA));

        // Die Fährte der Einheit: verweigert oder leer, aber nie herausgegeben.
        var faehrten = await w.Faehrten.GetByTrainingSessionAsync(w.TB, w.TrainingA);
        Assert.True(!faehrten.Succeeded || faehrten.Value!.Count == 0);

        // Auch die Listen der Trainer:in enthalten nichts von MA.
        Assert.Empty((await w.Training.GetSessionsToRateAsync(w.TB)).Value!);
        Assert.Empty((await w.Hunde.GetSupervisedDogsAsync(w.TB)).Value!);
        Assert.Empty((await w.Hunde.GetMyDogsAsync(w.TB)).Value!);
    }

    [Fact]
    public async Task Hund_FremdeVerwaltungKannKeinFeedbackGeben()
    {
        var w = await AufbauenAsync();

        Scheitert(await w.Training.SetFeedbackAsync(w.TB, w.TrainingA, new SetFeedbackRequest("Gut gemacht")));
        Scheitert(await w.Training.MarkSessionReviewedAsync(w.TB, w.TrainingA));

        var einheit = await w.Db.TrainingSessions.SingleAsync(s => s.Id == w.TrainingA);
        Assert.Null(einheit.TrainerFeedback);
        Assert.Null(einheit.TrainerReviewedAt);
    }

    // -------------------------------------------- Einladen statt Hineinziehen

    [Fact]
    public async Task Einladen_MitgliedEinesAnderenVereinsWirdNichtMitglied()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Vereine.AddMemberAsync(w.TB, isAdmin: false, w.B.Id, new AssignClubMemberRequest(MailMa));

        Assert.True(ergebnis.Succeeded);
        // MA ist (noch) nirgends in B Mitglied ...
        Assert.False(await w.Db.ClubMemberships.AnyAsync(m => m.ClubId == w.B.Id && m.UserId == w.MA && m.Status == ClubMembershipStatus.Approved));
        Assert.Empty((await w.Vereine.GetMembersAsync(w.TB, w.B.Id)).Value!);
        Assert.Empty((await w.Vereine.GetJoinRequestsAsync(w.TB, w.B.Id)).Value!);
        Assert.DoesNotContain(await w.Db.GetVisibleClubIdsAsync(w.MA), id => id == w.B.Id);
        Assert.Empty((await w.Vereine.GetDetailAsync(w.B.Id)).Value!.Members);

        // ... und B kennt nur die eingegebene Adresse, keinen Namen.
        var einladung = Assert.Single((await w.Vereine.GetInvitationsAsync(w.TB, w.B.Id)).Value!);
        Assert.Equal(MailMa, einladung.InvitedEmail);
        Assert.Equal(["MembershipId", "InvitedEmail", "RequestedAt"], typeof(ClubInvitationDto).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Marta", System.Text.Json.JsonSerializer.Serialize(einladung));
        Assert.DoesNotContain("Anders", System.Text.Json.JsonSerializer.Serialize(einladung));

        // Die Eingeladene sieht, wer sie einlädt - der Satz kommt aus dem Namen der Einladenden.
        var hinweis = Assert.Single(w.Meldungen.Created);
        Assert.Equal(w.MA, hinweis.UserId);
        Assert.Equal("Timo Bauer lädt dich in den Verein \"Verein B\" ein.", hinweis.Message);
        Assert.Equal("/clubs", hinweis.LinkPath);
    }

    [Fact]
    public async Task Einladen_EingeladeneKannNichtZurTrainerinGemachtWerden()
    {
        var w = await AufbauenAsync();
        await w.Vereine.AddMemberAsync(w.TB, isAdmin: false, w.B.Id, new AssignClubMemberRequest(MailMa));

        // Weder über die Beförderung eines Mitglieds ...
        var befoerdert = await w.Vereine.PromoteMemberToTrainerAsync(w.TB, w.B.Id, w.MA);
        Scheitert(befoerdert);
        // ... noch direkt per E-Mail-Adresse.
        var berufen = await w.Vereine.AssignTrainerAsync(w.TB, isAdmin: false, w.B.Id, new AssignClubTrainerRequest(MailMa));
        Scheitert(berufen);
        Assert.Contains("Nur Mitglieder dieses Vereins", Assert.Single(berufen.Errors));

        Assert.False(await w.Db.ClubTrainers.AnyAsync(t => t.ClubId == w.B.Id && t.UserId == w.MA));
    }

    [Fact]
    public async Task Einladen_OffeneEinladungZaehltNirgendsAlsMitgliedschaft()
    {
        // Solange die Person nicht zugesagt hat, wirkt sich die Einladung
        // nirgends als Mitgliedschaft aus: kein Zugriff auf Gruppen des
        // Vereins, keine Vereinsübungen, kein "gehört zu einem Verein".
        var w = await AufbauenAsync();
        var neu = Guid.NewGuid();
        w.Nutzer.Register(neu, "neu@example.org", "Nina", "Neu");
        w.Db.Groups.Add(new Group { ClubId = w.B.Id, TrainerId = w.TB, Name = "Gruppe B" });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Vereine.AddMemberAsync(w.TB, isAdmin: false, w.B.Id, new AssignClubMemberRequest("neu@example.org"))).Succeeded);

        KeinZugriff(await w.Gruppen.GetGroupsByClubAsync(neu, w.B.Id));
        Assert.False(await w.Db.BelongsToAnyClubAsync(neu));
        Assert.Empty(await w.Db.GetVisibleClubIdsAsync(neu));
        Assert.Empty((await w.Vereine.GetMembersAsync(w.TB, w.B.Id)).Value!);
        Assert.Empty((await w.Vereine.GetJoinRequestsAsync(w.TB, w.B.Id)).Value!);
        // Auf der eigenen Seite ist sie dagegen sichtbar - dort wird angenommen.
        Assert.Equal(ClubMembershipStatus.Invited, Assert.Single((await w.Vereine.GetMyMembershipsAsync(neu)).Value!).Status);
    }
}
