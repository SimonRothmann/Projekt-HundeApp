using System.Text.Json;
using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Sports;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Die Vereinsübersicht für Vorstand und Verwaltung (Paket V2, siehe
/// docs/BERECHTIGUNGEN.md): wer sie lesen darf, dass die Zahlen stimmen, dass
/// die Mitgliederliste keine E-Mail-Adresse enthält - und dass der Vorstand
/// allein weiterhin nichts ändern darf.
/// </summary>
public class ClubOverviewTests
{
    private sealed record Welt(
        ApplicationDbContext Db,
        FakeUserLookupService Nutzer,
        ClubService Vereine,
        ClubOverviewService Uebersicht,
        GroupService Gruppen,
        GroupTrainingScheduleService Termine,
        Club Verein,
        Guid Leitung);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var rollen = new TrainerRoleService(db, nutzer);
        var verein = new Club { Name = "Hundesportverein Nord" };
        db.Clubs.Add(verein);
        var leitung = Guid.NewGuid();
        nutzer.Register(leitung, "leitung@example.org", "Lena", "Leitung");
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = leitung, Role = ClubRole.Verwaltung });
        await db.SaveChangesAsync();
        return new Welt(
            db, nutzer,
            new ClubService(db, nutzer, meldungen, rollen),
            new ClubOverviewService(db, nutzer, TimeProvider.System),
            new GroupService(db, nutzer, rollen, meldungen, TimeProvider.System),
            new GroupTrainingScheduleService(db, nutzer, meldungen),
            verein, leitung);
    }

    /// <summary>Eine Person mit genau diesen Rollen im Verein (und, wenn gewünscht, als freigegebenes Mitglied).</summary>
    private static async Task<Guid> PersonAsync(Welt w, ClubRole rollen, bool mitglied = true, string vorname = "Person", string nachname = "Test")
    {
        var id = Guid.NewGuid();
        w.Nutzer.Register(id, $"p-{id:N}@example.org", vorname, nachname);
        if (rollen != 0) w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = id, Role = rollen });
        if (mitglied) w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = id, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();
        return id;
    }

    private static string Mail(Welt w, Guid id) => w.Nutzer.FindByIdsAsync([id]).Result[id].Email;

    // ------------------------------------------------------------ Zugriff

    public static TheoryData<ClubRole, bool> Zugriff => new()
    {
        { 0, false },
        { ClubRole.Training, false },
        { ClubRole.Ausbildungswart, false },
        { ClubRole.Mitgliederverwaltung, false },
        { ClubRole.Training | ClubRole.Ausbildungswart | ClubRole.Mitgliederverwaltung, false },
        { ClubRole.Vorstand, true },
        { ClubRole.Verwaltung, true },
        { ClubRole.Training | ClubRole.Vorstand, true },
    };

    [Theory]
    [MemberData(nameof(Zugriff))]
    public async Task UebersichtUndListe_NurFuerVorstandUndVerwaltung(ClubRole rollen, bool erlaubt)
    {
        var w = await AufbauenAsync();
        var id = await PersonAsync(w, rollen);

        var uebersicht = await w.Uebersicht.GetOverviewAsync(id, false, w.Verein.Id);
        var liste = await w.Uebersicht.GetMembersAsync(id, false, w.Verein.Id);

        Assert.Equal(erlaubt, uebersicht.Succeeded);
        Assert.Equal(erlaubt, liste.Succeeded);
        // Ohne Recht ist der Verein "nicht gefunden" - wie bei den übrigen Vereinsendpunkten.
        Assert.Equal(!erlaubt, uebersicht.IsNotFound);
        Assert.Equal(!erlaubt, liste.IsNotFound);
    }

    [Fact]
    public async Task FremderVerein_IstNichtGefunden_AuchMitVorstandImEigenen()
    {
        var w = await AufbauenAsync();
        var anderer = new Club { Name = "Hundesportverein Süd" };
        w.Db.Clubs.Add(anderer);
        var vorstand = Guid.NewGuid();
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = anderer.Id, UserId = vorstand, Role = ClubRole.Vorstand });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Uebersicht.GetOverviewAsync(vorstand, false, anderer.Id)).Succeeded);
        Assert.True((await w.Uebersicht.GetOverviewAsync(vorstand, false, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Uebersicht.GetMembersAsync(vorstand, false, w.Verein.Id)).IsNotFound);
    }

    [Fact]
    public async Task Admin_DarfLesen_UnbekannterVereinIstNichtGefunden()
    {
        var w = await AufbauenAsync();
        var admin = Guid.NewGuid();

        Assert.True((await w.Uebersicht.GetOverviewAsync(admin, true, w.Verein.Id)).Succeeded);
        Assert.True((await w.Uebersicht.GetMembersAsync(admin, true, w.Verein.Id)).Succeeded);
        Assert.True((await w.Uebersicht.GetOverviewAsync(admin, true, Guid.NewGuid())).IsNotFound);
    }

    [Fact]
    public async Task GeloeschterVerein_IstNichtGefunden_AuchFuerDieVerwaltung()
    {
        var w = await AufbauenAsync();
        w.Verein.DeletedAt = DateTimeOffset.UtcNow;
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Uebersicht.GetOverviewAsync(w.Leitung, false, w.Verein.Id)).IsNotFound);
    }

    [Fact]
    public async Task MeineVereine_MeldenDasRechtZurUebersicht()
    {
        var w = await AufbauenAsync();
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);
        var mitgliederverwaltung = await PersonAsync(w, ClubRole.Mitgliederverwaltung);

        Assert.True(Assert.Single((await w.Vereine.GetMyClubRolesAsync(vorstand)).Value!).CanViewOverview);
        Assert.True(Assert.Single((await w.Vereine.GetMyClubRolesAsync(w.Leitung)).Value!).CanViewOverview);
        Assert.False(Assert.Single((await w.Vereine.GetMyClubRolesAsync(mitgliederverwaltung)).Value!).CanViewOverview);
    }

    // ------------------------------------------------------------ Zahlen

    private static DateTimeOffset MonatsMitte(int monateZurueck) =>
        Vereinszeit.TagesBeginn(new DateOnly(Vereinszeit.Heute().Year, Vereinszeit.Heute().Month, 1).AddMonths(-monateZurueck)).AddHours(12);

    private static GroupTrainingSession Termin(Guid verein, Guid gruppe, DateTimeOffset beginn, GroupTrainingSessionStatus status = GroupTrainingSessionStatus.Planned) =>
        new() { ClubId = verein, GroupId = gruppe, StartsAt = beginn, Status = status };

    /// <summary>
    /// Ein kleiner Datensatz mit allem, was NICHT zählen darf: gelöschte
    /// Mitgliedschaft und Gruppe, offene Mitgliedschaft in der Gruppe, nicht
    /// angenommene Mitbetreuung, ein Termin eines fremden Vereins und einer
    /// weit zurück liegenden Monats.
    /// </summary>
    private static async Task<(Welt W, Guid Anna, Guid Ben, Guid Cora, Group Welpen, Group Alpha)> DatensatzAsync()
    {
        var w = await AufbauenAsync();
        var jetzt = DateTimeOffset.UtcNow;

        // Freigegeben: Anna (vor 10 Tagen), Ben (vor 100 Tagen), Cora (vor 50 Tagen, ohne Gruppe).
        var anna = await PersonAsync(w, 0, vorname: "Anna", nachname: "Ahorn");
        var ben = await PersonAsync(w, 0, vorname: "Ben", nachname: "Birke");
        var cora = await PersonAsync(w, 0, vorname: "Cora", nachname: "Zeder");
        foreach (var (id, tage) in new[] { (anna, 10), (ben, 100), (cora, 50) })
        {
            var m = await w.Db.ClubMemberships.SingleAsync(x => x.UserId == id);
            m.RequestedAt = jetzt.AddDays(-tage - 1);
            m.DecidedAt = jetzt.AddDays(-tage);
        }

        // Nicht gezählt: gelöscht, abgelehnt - dazu je eine offene Anfrage und Einladung, die einzeln zählen.
        w.Db.ClubMemberships.AddRange(
            new ClubMembership { ClubId = w.Verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Approved, DeletedAt = jetzt },
            new ClubMembership { ClubId = w.Verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Rejected },
            new ClubMembership { ClubId = w.Verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Pending },
            new ClubMembership { ClubId = w.Verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Invited, InvitedEmail = "eingeladen@example.org" });

        // Rollen: Verwaltung (Lena), Training (Tim, nicht Mitglied), Vorstand (Vera, nicht Mitglied).
        var tim = await PersonAsync(w, ClubRole.Training, mitglied: false, vorname: "Tim", nachname: "Trainer");
        await PersonAsync(w, ClubRole.Vorstand | ClubRole.Mitgliederverwaltung, mitglied: false, vorname: "Vera", nachname: "Vorstand");

        var welpen = new Group { ClubId = w.Verein.Id, TrainerId = w.Leitung, Name = "Welpen" };
        var alpha = new Group { ClubId = w.Verein.Id, TrainerId = Guid.NewGuid(), Name = "Alpha" };
        var geloescht = new Group { ClubId = w.Verein.Id, TrainerId = Guid.NewGuid(), Name = "Gelöscht", DeletedAt = jetzt };
        w.Db.Groups.AddRange(welpen, alpha, geloescht);
        w.Db.GroupMembers.AddRange(
            new GroupMember { GroupId = welpen.Id, UserId = anna, Status = GroupMemberStatus.Active },
            new GroupMember { GroupId = welpen.Id, UserId = ben, Status = GroupMemberStatus.Pending },
            new GroupMember { GroupId = alpha.Id, UserId = ben, Status = GroupMemberStatus.Active },
            // Nur in einer gelöschten Gruppe: Cora bleibt "ohne Gruppe".
            new GroupMember { GroupId = geloescht.Id, UserId = cora, Status = GroupMemberStatus.Active });
        w.Db.GroupTrainers.AddRange(
            new GroupTrainer { GroupId = welpen.Id, UserId = tim, Status = GroupTrainerStatus.Active },
            new GroupTrainer { GroupId = welpen.Id, UserId = Guid.NewGuid(), Status = GroupTrainerStatus.Invited });

        // Termine: zwei Monate zurück 2 geplant + 1 abgesagt; laufender Monat 1 geplant.
        var s1 = Termin(w.Verein.Id, welpen.Id, MonatsMitte(2));
        var s2 = Termin(w.Verein.Id, welpen.Id, MonatsMitte(2).AddDays(1));
        var s3 = Termin(w.Verein.Id, welpen.Id, MonatsMitte(2).AddDays(2), GroupTrainingSessionStatus.Cancelled);
        var s4 = Termin(w.Verein.Id, alpha.Id, MonatsMitte(0));
        // Zählt nicht: zu alt, gelöschte Gruppe, gelöschter Termin, anderer Verein.
        var andererVerein = new Club { Name = "Hundesportverein Süd" };
        var fremdeGruppe = new Group { ClubId = andererVerein.Id, TrainerId = Guid.NewGuid(), Name = "Fremd" };
        w.Db.Clubs.Add(andererVerein);
        w.Db.Groups.Add(fremdeGruppe);
        w.Db.GroupTrainingSessions.AddRange(
            s1, s2, s3, s4,
            Termin(w.Verein.Id, welpen.Id, MonatsMitte(7)),
            Termin(w.Verein.Id, geloescht.Id, MonatsMitte(0)),
            new GroupTrainingSession { ClubId = w.Verein.Id, GroupId = welpen.Id, StartsAt = MonatsMitte(1), DeletedAt = jetzt },
            Termin(andererVerein.Id, fremdeGruppe.Id, MonatsMitte(0)));
        await w.Db.SaveChangesAsync();

        // Zusagen: nur Annas Zusage zu s1 und Bens Zusage zu s4 zählen.
        w.Db.GroupTrainingSessionResponses.AddRange(
            new GroupTrainingSessionResponse { GroupTrainingSessionId = s1.Id, UserId = anna, IsAttending = true },
            new GroupTrainingSessionResponse { GroupTrainingSessionId = s1.Id, UserId = Guid.NewGuid(), IsAttending = true },   // kein Gruppenmitglied (mehr)
            new GroupTrainingSessionResponse { GroupTrainingSessionId = s2.Id, UserId = anna, IsAttending = false },            // Absage
            new GroupTrainingSessionResponse { GroupTrainingSessionId = s3.Id, UserId = anna, IsAttending = true },             // abgesagter Termin
            new GroupTrainingSessionResponse { GroupTrainingSessionId = s4.Id, UserId = ben, IsAttending = true });
        await w.Db.SaveChangesAsync();
        return (w, anna, ben, cora, welpen, alpha);
    }

    [Fact]
    public async Task Zahlen_StimmenAufEinemKleinenDatensatz()
    {
        var (w, _, _, _, welpen, alpha) = await DatensatzAsync();

        var u = (await w.Uebersicht.GetOverviewAsync(w.Leitung, false, w.Verein.Id)).Value!;

        Assert.Equal(3, u.MemberCount);
        Assert.Equal(1, u.NewMembersLast30Days);
        Assert.Equal(1, u.PendingRequests);
        Assert.Equal(1, u.OpenInvitations);
        Assert.Equal(1, u.MembersWithoutGroup);

        // Alle fünf Rollen, auch mit 0: Die Verwaltung kommt von Lena allein (nicht von den Rollen darunter).
        Assert.Equal(
            new[] { (ClubRole.Training, 1), (ClubRole.Ausbildungswart, 0), (ClubRole.Mitgliederverwaltung, 1), (ClubRole.Vorstand, 1), (ClubRole.Verwaltung, 1) },
            u.Roles.Select(r => (r.Role, r.Count)).ToArray());

        // Alphabetisch; gelöschte Gruppe fehlt; offene Mitgliedschaft und nicht angenommene Mitbetreuung zählen nicht.
        Assert.Equal(new[] { "Alpha", "Welpen" }, u.Groups.Select(g => g.Name).ToArray());
        var a = u.Groups.Single(g => g.Id == alpha.Id);
        var wl = u.Groups.Single(g => g.Id == welpen.Id);
        Assert.Equal((1, 1), (a.MemberCount, a.TrainerCount));
        Assert.Equal((1, 2), (wl.MemberCount, wl.TrainerCount));

        Assert.Equal(6, u.Months.Count);
        var heute = Vereinszeit.Heute();
        Assert.Equal((heute.Year, heute.Month), (u.Months[^1].Year, u.Months[^1].Month));
        var vorZwei = u.Months[^3];
        Assert.Equal((2, 1, 1), (vorZwei.Planned, vorZwei.Cancelled, vorZwei.Attendances));
        var laufend = u.Months[^1];
        Assert.Equal((1, 0, 1), (laufend.Planned, laufend.Cancelled, laufend.Attendances));
        // Alles andere ist leer: der zu alte Termin, der gelöschte, der der gelöschten Gruppe und der des fremden Vereins zählen nirgends.
        Assert.Equal(4, u.Months.Sum(m => m.Planned + m.Cancelled));
    }

    [Fact]
    public async Task LeererVerein_LiefertNullenUndSechsMonate()
    {
        var w = await AufbauenAsync();

        var u = (await w.Uebersicht.GetOverviewAsync(w.Leitung, false, w.Verein.Id)).Value!;

        Assert.Equal((0, 0, 0, 0, 0), (u.MemberCount, u.NewMembersLast30Days, u.PendingRequests, u.OpenInvitations, u.MembersWithoutGroup));
        Assert.Empty(u.Groups);
        Assert.Equal(6, u.Months.Count);
        Assert.All(u.Months, m => Assert.Equal((0, 0, 0), (m.Planned, m.Cancelled, m.Attendances)));
        Assert.Equal(1, u.Roles.Single(r => r.Role == ClubRole.Verwaltung).Count);
    }

    // ------------------------------------------------------------ Mitgliederliste

    [Fact]
    public async Task Mitgliederliste_NamenGruppenRollen_SortiertNachNachnameVorname_OhneGeloeschte()
    {
        var (w, anna, _, _, _, _) = await DatensatzAsync();
        // Anna ist zusätzlich Vorstand: Rollen erscheinen in der Liste.
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = anna, Role = ClubRole.Vorstand });
        await w.Db.SaveChangesAsync();

        var liste = (await w.Uebersicht.GetMembersAsync(w.Leitung, false, w.Verein.Id)).Value!;

        Assert.Equal(new[] { "Ahorn", "Birke", "Zeder" }, liste.Select(m => m.LastName).ToArray());
        Assert.Equal(new[] { "Welpen" }, liste[0].Groups);
        Assert.Equal(ClubRole.Vorstand, liste[0].Roles);
        Assert.Equal(new[] { "Alpha" }, liste[1].Groups);   // Die offene Mitgliedschaft in "Welpen" zeigt nichts an.
        Assert.Empty(liste[2].Groups);                      // Nur in einer gelöschten Gruppe.
        Assert.Equal((ClubRole)0, liste[2].Roles);
        Assert.True(liste[0].MemberSince < DateTimeOffset.UtcNow.AddDays(-9));
    }

    [Fact]
    public async Task Mitgliederliste_TrainerinNenntIhreGruppen_MitgliederOhneKontoBleibenOhneNamen()
    {
        var w = await AufbauenAsync();
        var tim = await PersonAsync(w, ClubRole.Training, vorname: "Tim", nachname: "Trainer");
        var gruppe = new Group { ClubId = w.Verein.Id, TrainerId = tim, Name = "Tims Gruppe" };
        w.Db.Groups.Add(gruppe);
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();

        var liste = (await w.Uebersicht.GetMembersAsync(w.Leitung, false, w.Verein.Id)).Value!;

        var tims = liste.Single(m => m.LastName == "Trainer");
        Assert.Equal(new[] { "Tims Gruppe" }, tims.Groups);
        var ohneKonto = liste.Single(m => m.LastName == "");
        Assert.Equal("", ohneKonto.FirstName);
        // Ein Mitglied, das eine Gruppe leitet, ist nicht "ohne Gruppe".
        Assert.Equal(1, (await w.Uebersicht.GetOverviewAsync(w.Leitung, false, w.Verein.Id)).Value!.MembersWithoutGroup);
    }

    [Fact]
    public async Task Mitgliederliste_EnthaeltKeineEmailAdresse()
    {
        var (w, _, _, _, _, _) = await DatensatzAsync();

        var liste = (await w.Uebersicht.GetMembersAsync(w.Leitung, false, w.Verein.Id)).Value!;
        var uebersicht = (await w.Uebersicht.GetOverviewAsync(w.Leitung, false, w.Verein.Id)).Value!;

        Assert.NotEmpty(liste);
        // Weder als Feld noch irgendwo im Wortlaut der Antwort.
        var json = JsonSerializer.Serialize(liste) + JsonSerializer.Serialize(uebersicht);
        Assert.DoesNotContain("@", json);
        Assert.DoesNotContain("mail", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(typeof(ClubOverviewMemberDto).GetProperties(), p => p.Name.Contains("Mail", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------ Nur lesen

    [Fact]
    public async Task Vorstand_AendertNichts_JedeSchreibendeVereinsaktionWirdAbgelehnt()
    {
        var w = await AufbauenAsync();
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);
        var mitglied = await PersonAsync(w, 0);
        var trainerin = await PersonAsync(w, ClubRole.Training);
        var gruppe = new Group { ClubId = w.Verein.Id, TrainerId = w.Leitung, Name = "Gruppe" };
        var anfrage = new ClubMembership { ClubId = w.Verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Pending };
        var einladung = new ClubMembership { ClubId = w.Verein.Id, UserId = Guid.NewGuid(), Status = ClubMembershipStatus.Invited, InvitedEmail = "x@example.org" };
        w.Db.Groups.Add(gruppe);
        w.Db.ClubMemberships.AddRange(anfrage, einladung);
        await w.Db.SaveChangesAsync();
        var vorher = (w.Db.ClubTrainers.Count(), w.Db.ClubMemberships.Count(), w.Db.Groups.Count(), w.Db.GroupTrainingSessions.Count());

        var v = w.Verein.Id;
        var einladungen = new ClubInviteService(w.Db, w.Vereine);
        var bibliothek = new GroupTrainingService(w.Db);
        var katalog = new SportCatalogService(w.Db, TimeProvider.System);
        var ergebnisse = new Dictionary<string, bool>
        {
            ["Stammdaten"] = (await w.Vereine.UpdateClubAsync(vorstand, false, v, new UpdateClubRequest("Neu", null))).Succeeded,
            ["Rolle vergeben"] = (await w.Vereine.AssignTrainerAsync(vorstand, false, v, new AssignClubTrainerRequest(Mail(w, mitglied), ClubRole.Vorstand))).Succeeded,
            ["Rollen ändern"] = (await w.Vereine.UpdateTrainerRoleAsync(vorstand, false, v, trainerin, ClubRole.Training | ClubRole.Vorstand)).Succeeded,
            ["Rollen entziehen"] = (await w.Vereine.RemoveTrainerAsync(vorstand, false, v, trainerin)).Succeeded,
            ["Zum Trainer machen"] = (await w.Vereine.PromoteMemberToTrainerAsync(vorstand, v, mitglied)).Succeeded,
            ["Einladen"] = (await w.Vereine.AddMemberAsync(vorstand, false, v, new AssignClubMemberRequest(Mail(w, mitglied)))).Succeeded,
            ["Einladung zurückziehen"] = (await w.Vereine.WithdrawInvitationAsync(vorstand, v, einladung.Id)).Succeeded,
            ["Anfrage annehmen"] = (await w.Vereine.DecideJoinRequestAsync(vorstand, v, anfrage.Id, approve: true)).Succeeded,
            ["Anfrage ablehnen"] = (await w.Vereine.DecideJoinRequestAsync(vorstand, v, anfrage.Id, approve: false)).Succeeded,
            ["Mitglied entfernen"] = (await w.Vereine.RemoveClubMemberAsync(vorstand, v, mitglied)).Succeeded,
            ["Einladungslink erzeugen"] = (await einladungen.RegenerateAsync(vorstand, v)).Succeeded,
            ["Einladungslink abschalten"] = (await einladungen.DisableAsync(vorstand, v)).Succeeded,
            ["Gruppe anlegen"] = (await w.Gruppen.CreateAsync(vorstand, new CreateGroupRequest("Neue Gruppe", null, v))).Succeeded,
            ["Gruppe ändern"] = (await w.Gruppen.UpdateGroupAsync(vorstand, gruppe.Id, new UpdateGroupRequest("Neu", null))).Succeeded,
            ["Gruppe löschen"] = (await w.Gruppen.DeleteGroupAsync(vorstand, gruppe.Id)).Succeeded,
            ["Termin anlegen"] = (await w.Termine.CreateSessionAsync(vorstand, v, new CreateSessionRequest(gruppe.Id, GroupTrainingCategory.Puppy, DateTimeOffset.UtcNow.AddDays(2), 60, null, null, [], []))).Succeeded,
            ["Baustein anlegen"] = (await bibliothek.CreateExerciseAsync(vorstand, v, new UpsertExerciseRequest(GroupTrainingCategory.Puppy, "Sitz", null, 5, null))).Succeeded,
            ["Katalog: Sportart"] = (await katalog.CreateSportAsync(vorstand, false, new CreateSportRequest("X", "Eigene Sportart", null, v))).Succeeded,
        };

        Assert.All(ergebnisse, e => Assert.False(e.Value, $"Der Vorstand durfte: {e.Key}"));
        Assert.Equal(vorher, (w.Db.ClubTrainers.Count(), w.Db.ClubMemberships.Count(), w.Db.Groups.Count(), w.Db.GroupTrainingSessions.Count()));
        Assert.Equal("Hundesportverein Nord", (await w.Db.Clubs.SingleAsync()).Name);
        Assert.Equal(ClubRole.Vorstand, await w.Db.GetClubRolesAsync(vorstand, v));
        Assert.Equal(ClubRole.Training, await w.Db.GetClubRolesAsync(trainerin, v));
        Assert.Equal(ClubMembershipStatus.Pending, (await w.Db.ClubMemberships.SingleAsync(m => m.Id == anfrage.Id)).Status);
        // Auch die lesenden Verwaltungsansichten sind zu: Die Übersicht ersetzt sie nicht.
        Assert.True((await w.Vereine.GetMembersAsync(vorstand, v)).IsNotFound);
        Assert.True((await w.Vereine.GetJoinRequestsAsync(vorstand, v)).IsNotFound);
        Assert.True((await w.Vereine.GetInvitationsAsync(vorstand, v)).IsNotFound);
        Assert.True((await einladungen.GetAsync(vorstand, v)).IsNotFound);
        Assert.True((await w.Vereine.GetRoleHoldersAsync(vorstand, false, v)).IsNotFound);
    }

    // ------------------------------------------------------------ Datenminimierung

    [Fact]
    public async Task Rollenliste_EmailNurFuerMitgliederverwaltungVerwaltungUndAdmin()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart, vorname: "Willi", nachname: "Wart");
        var wartUndMitglieder = await PersonAsync(w, ClubRole.Ausbildungswart | ClubRole.Mitgliederverwaltung);

        var ohne = (await w.Vereine.GetRoleHoldersAsync(wart, false, w.Verein.Id)).Value!;
        var mitMitgliederverwaltung = (await w.Vereine.GetRoleHoldersAsync(wartUndMitglieder, false, w.Verein.Id)).Value!;
        var verwaltung = (await w.Vereine.GetRoleHoldersAsync(w.Leitung, false, w.Verein.Id)).Value!;
        var admin = (await w.Vereine.GetRoleHoldersAsync(Guid.NewGuid(), true, w.Verein.Id)).Value!;

        // Namen und Rollen bleiben; die Adresse fehlt vollständig.
        Assert.Equal(3, ohne.Count);
        Assert.All(ohne, p => Assert.Null(p.Email));
        Assert.Contains(ohne, p => p.FirstName == "Willi" && p.LastName == "Wart" && p.Roles == ClubRole.Ausbildungswart);
        Assert.DoesNotContain("@", JsonSerializer.Serialize(ohne));
        foreach (var liste in new[] { mitMitgliederverwaltung, verwaltung, admin })
            Assert.All(liste, p => Assert.Contains("@example.org", p.Email));
    }

    [Fact]
    public async Task Gruppentrainer_AuswahlzeigtDemAusbildungswartNurNamen_DerVerwaltungAuchDieAdresse()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        await PersonAsync(w, ClubRole.Training, vorname: "Tim", nachname: "Trainer");
        var gruppe = new Group { ClubId = w.Verein.Id, TrainerId = w.Leitung, Name = "Gruppe" };
        w.Db.Groups.Add(gruppe);
        await w.Db.SaveChangesAsync();

        var fuerWart = (await w.Gruppen.GetAssignableTrainersAsync(wart, gruppe.Id)).Value!;
        var fuerVerwaltung = (await w.Gruppen.GetAssignableTrainersAsync(w.Leitung, gruppe.Id)).Value!;

        Assert.Contains(fuerWart, o => o.FirstName == "Tim" && o.LastName == "Trainer");
        Assert.All(fuerWart, o => Assert.Null(o.Email));
        Assert.All(fuerVerwaltung, o => Assert.Contains("@example.org", o.Email));
    }
}
