using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Die Vereinsrollen (siehe docs/BERECHTIGUNGEN.md): Training, Verwaltung,
/// Ausbildungswart:in, Mitgliederverwaltung, Vorstand - frei kombinierbar.
/// Geprüft wird je Recht, welche Rolle es hat, und dass jede Aktion an dem
/// Recht hängt, das sie braucht, nicht an einer Rolle.
/// </summary>
public class VereinsrollenTests
{
    private sealed record Welt(
        ApplicationDbContext Db,
        FakeUserLookupService Nutzer,
        FakeNotificationService Meldungen,
        ClubService Vereine,
        GroupService Gruppen,
        GroupTrainingScheduleService Termine,
        Club Verein,
        Group EigeneGruppe,
        Group FremdeGruppe,
        Guid Leitung);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var rollen = new TrainerRoleService(db, nutzer);
        var verein = new Club { Name = "Hundesportverein Nord" };
        db.Clubs.Add(verein);
        // Eine Leitung (Verwaltung) mit einer fremden Gruppe, die niemand sonst leitet.
        var leitung = Guid.NewGuid();
        nutzer.Register(leitung, "leitung@example.org", "Lena", "Leitung");
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = leitung, Role = ClubRole.Verwaltung });
        var fremde = new Group { ClubId = verein.Id, TrainerId = leitung, Name = "Fremde Gruppe" };
        var eigene = new Group { ClubId = verein.Id, TrainerId = Guid.NewGuid(), Name = "Eigene Gruppe" };
        db.Groups.AddRange(fremde, eigene);
        await db.SaveChangesAsync();
        return new Welt(
            db, nutzer, meldungen,
            new ClubService(db, nutzer, meldungen, rollen),
            new GroupService(db, nutzer, rollen, meldungen, TimeProvider.System),
            new GroupTrainingScheduleService(db, nutzer, meldungen),
            verein, eigene, fremde, leitung);
    }

    /// <summary>Eine Person mit genau diesen Rollen im Verein (und, wenn gewünscht, als freigegebenes Mitglied).</summary>
    private static async Task<Guid> PersonAsync(Welt w, ClubRole rollen, bool mitglied = true, string? name = null)
    {
        var id = Guid.NewGuid();
        w.Nutzer.Register(id, $"{name ?? rollen.ToString().Replace(", ", "-").ToLowerInvariant()}-{id:N}@example.org", name ?? "Person", rollen.ToString());
        if (rollen != 0) w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = id, Role = rollen });
        if (mitglied) w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = id, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();
        return id;
    }

    private static Task<Guid> MitgliedOhneRolleAsync(Welt w) => PersonAsync(w, 0);

    private static string Mail(Welt w, Guid id) => w.Nutzer.FindByIdsAsync([id]).Result[id].Email;

    // ------------------------------------------------------------ Werte

    [Fact]
    public void Rollenwerte_SindFest_DieDieMigrationDerAltwerteVoraussetzt()
    {
        // Die Migration rechnet "Training" -> 1 und "Verwaltung" -> 2 um (nicht 0 und 1, wie die
        // Aufzählung früher zählte). Ändern sich diese Werte, stimmt jede gespeicherte Zeile nicht mehr.
        Assert.Equal(1, (int)ClubRole.Training);
        Assert.Equal(2, (int)ClubRole.Verwaltung);
        Assert.Equal(4, (int)ClubRole.Ausbildungswart);
        Assert.Equal(8, (int)ClubRole.Mitgliederverwaltung);
        Assert.Equal(16, (int)ClubRole.Vorstand);
    }

    [Fact]
    public async Task NeueZeile_HatStandardmaessigTraining()
    {
        // Die Spalte hat keinen Datenbank-Standard; "Training" kommt vom Typ - nicht 0 (= keine Rolle).
        Assert.Equal(ClubRole.Training, new ClubTrainer().Role);
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------ Rechte-Matrix

    public static TheoryData<ClubRole, bool, bool, bool, bool, bool, bool> Matrix => new()
    {
        // Rollen,                                        Trainer, AlleGruppen, Mitglieder, Berufen, Übersicht, Verwalten
        { 0,                                              false,   false,       false,      false,   false,     false },
        { ClubRole.Training,                              true,    false,       false,      false,   false,     false },
        { ClubRole.Ausbildungswart,                       true,    true,        false,      true,    false,     false },
        { ClubRole.Mitgliederverwaltung,                  false,   false,       true,       false,   false,     false },
        { ClubRole.Vorstand,                              false,   false,       false,      false,   true,      false },
        { ClubRole.Verwaltung,                            true,    true,        true,       true,    true,      true },
        { ClubRole.Training | ClubRole.Vorstand,          true,    false,       false,      false,   true,      false },
        { ClubRole.Training | ClubRole.Mitgliederverwaltung, true, false,       true,       false,   false,     false },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Rechte_JeRolle(ClubRole rollen, bool trainer, bool alleGruppen, bool mitglieder, bool berufen, bool uebersicht, bool verwalten)
    {
        var w = await AufbauenAsync();
        var id = await PersonAsync(w, rollen);

        Assert.Equal(trainer, await w.Db.IsClubTrainerAsync(id, w.Verein.Id));
        Assert.Equal(alleGruppen, await w.Db.CanManageAllGroupsAsync(id, w.Verein.Id));
        Assert.Equal(mitglieder, await w.Db.CanManageMembersAsync(id, w.Verein.Id));
        Assert.Equal(berufen, await w.Db.CanAppointTrainersAsync(id, w.Verein.Id));
        Assert.Equal(uebersicht, await w.Db.CanViewClubOverviewAsync(id, w.Verein.Id));
        Assert.Equal(verwalten, await w.Db.CanManageClubAsync(id, w.Verein.Id));
        // Trainer-Perspektive und Identity-Rolle hängen an derselben Frage.
        Assert.Equal(trainer, await w.Db.IsAnyTrainerAsync(id));
        // Die Gruppe eines anderen: nur mit dem Recht für alle Gruppen.
        Assert.Equal(alleGruppen, await w.Db.GetManageableGroupAsync(id, w.FremdeGruppe.Id) is not null);
    }

    [Fact]
    public async Task FremderVerein_MitJederRolle_HatHierKeinRecht()
    {
        var w = await AufbauenAsync();
        var anderer = new Club { Name = "Hundesportverein Süd" };
        w.Db.Clubs.Add(anderer);
        var id = Guid.NewGuid();
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = anderer.Id, UserId = id, Role = ClubRollen.Alle });
        await w.Db.SaveChangesAsync();

        Assert.False(await w.Db.IsClubTrainerAsync(id, w.Verein.Id));
        Assert.False(await w.Db.CanManageAllGroupsAsync(id, w.Verein.Id));
        Assert.False(await w.Db.CanManageMembersAsync(id, w.Verein.Id));
        Assert.False(await w.Db.CanAppointTrainersAsync(id, w.Verein.Id));
        Assert.False(await w.Db.CanViewClubOverviewAsync(id, w.Verein.Id));
        Assert.False(await w.Db.CanManageClubAsync(id, w.Verein.Id));
        Assert.Null(await w.Db.GetManageableGroupAsync(id, w.FremdeGruppe.Id));
    }

    [Fact]
    public async Task Katalogsicht_NurTrainerRollenUndGruppenmitglieder()
    {
        var w = await AufbauenAsync();
        var training = await PersonAsync(w, ClubRole.Training);
        var mitgliederverwaltung = await PersonAsync(w, ClubRole.Mitgliederverwaltung);
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);

        Assert.Contains(w.Verein.Id, await w.Db.GetVisibleClubIdsAsync(training));
        Assert.DoesNotContain(w.Verein.Id, await w.Db.GetVisibleClubIdsAsync(mitgliederverwaltung));
        Assert.DoesNotContain(w.Verein.Id, await w.Db.GetVisibleClubIdsAsync(vorstand));
        // Aber sie gehören zum Verein: keine Aufforderung beizutreten.
        Assert.True(await w.Db.BelongsToAnyClubAsync(vorstand));
    }

    // ------------------------------------------------------------ Gruppen: Training nur eigene

    [Fact]
    public async Task Training_VerwaltetEigeneGruppe_AberNichtDieFremde()
    {
        var w = await AufbauenAsync();
        var training = await PersonAsync(w, ClubRole.Training);
        w.Db.Groups.Add(new Group { ClubId = w.Verein.Id, TrainerId = training, Name = "Meine" });
        await w.Db.SaveChangesAsync();
        var meine = await w.Db.Groups.SingleAsync(g => g.TrainerId == training);

        var eigene = await w.Gruppen.UpdateGroupAsync(training, meine.Id, new UpdateGroupRequest("Meine neu", null));
        var fremde = await w.Gruppen.UpdateGroupAsync(training, w.FremdeGruppe.Id, new UpdateGroupRequest("Gekapert", null));

        Assert.True(eigene.Succeeded);
        Assert.True(fremde.IsNotFound);
        Assert.Equal("Fremde Gruppe", (await w.Db.Groups.SingleAsync(g => g.Id == w.FremdeGruppe.Id)).Name);
    }

    [Fact]
    public async Task Training_SiehtInSeinenGruppenNurDieEigenen_AusbildungswartAlle()
    {
        var w = await AufbauenAsync();
        var training = await PersonAsync(w, ClubRole.Training);
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        w.Db.Groups.Add(new Group { ClubId = w.Verein.Id, TrainerId = training, Name = "Meine" });
        await w.Db.SaveChangesAsync();

        var ihre = (await w.Gruppen.GetMyGroupsAsync(training)).Value!;
        var alle = (await w.Gruppen.GetMyGroupsAsync(wart)).Value!;

        Assert.Equal(["Meine"], ihre.Select(g => g.Name));
        Assert.Equal(3, alle.Count);
        Assert.True((await w.Gruppen.GetDetailAsync(training, w.FremdeGruppe.Id)).IsNotFound);
        Assert.True((await w.Gruppen.GetDetailAsync(wart, w.FremdeGruppe.Id)).Succeeded);
    }

    [Fact]
    public async Task Gruppenliste_ImVerein_ZeigtTrainingNurDieEigenenAlsTrainerGruppe()
    {
        var w = await AufbauenAsync();
        var training = await PersonAsync(w, ClubRole.Training);
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);

        var fuerTraining = (await w.Gruppen.GetGroupsByClubAsync(training, w.Verein.Id)).Value!;
        var fuerWart = (await w.Gruppen.GetGroupsByClubAsync(wart, w.Verein.Id)).Value!;

        Assert.DoesNotContain(fuerTraining, g => g.MyRelation == GroupRelation.Trainer);
        Assert.All(fuerWart, g => Assert.Equal(GroupRelation.Trainer, g.MyRelation));
    }

    [Fact]
    public async Task Vorstand_SiehtDieGruppenDesVereins_VerwaltetSieAberNicht()
    {
        var w = await AufbauenAsync();
        var vorstand = await PersonAsync(w, ClubRole.Vorstand, mitglied: false);

        var liste = await w.Gruppen.GetGroupsByClubAsync(vorstand, w.Verein.Id);
        var aendern = await w.Gruppen.UpdateGroupAsync(vorstand, w.FremdeGruppe.Id, new UpdateGroupRequest("X", null));

        Assert.True(liste.Succeeded);
        Assert.True(aendern.IsNotFound);
    }

    // ------------------------------------------------------------ Termine

    private static CreateSessionRequest Termin(Guid gruppe) =>
        new(gruppe, GroupTrainingCategory.Puppy, DateTimeOffset.UtcNow.AddDays(2), 60, null, null, [], []);

    [Fact]
    public async Task Termine_TrainingNurFuerEigeneGruppen()
    {
        var w = await AufbauenAsync();
        var training = await PersonAsync(w, ClubRole.Training);
        w.Db.GroupTrainers.Add(new GroupTrainer { GroupId = w.EigeneGruppe.Id, UserId = training, Status = GroupTrainerStatus.Active });
        await w.Db.SaveChangesAsync();

        var eigene = await w.Termine.CreateSessionAsync(training, w.Verein.Id, Termin(w.EigeneGruppe.Id));
        var fremde = await w.Termine.CreateSessionAsync(training, w.Verein.Id, Termin(w.FremdeGruppe.Id));

        Assert.True(eigene.Succeeded);
        Assert.False(fremde.Succeeded);
        Assert.False((await w.Termine.GenerateSeriesAsync(training, w.Verein.Id,
            new GenerateSeriesRequest(w.FremdeGruppe.Id, GroupTrainingCategory.Puppy, [DateTimeOffset.UtcNow.AddDays(3)], 60, null, [], [], false))).Succeeded);
    }

    [Fact]
    public async Task Termine_AusbildungswartUndVerwaltungFuerAlleGruppen()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);

        var termin = await w.Termine.CreateSessionAsync(wart, w.Verein.Id, Termin(w.FremdeGruppe.Id));
        var verwaltung = await w.Termine.CreateSessionAsync(w.Leitung, w.Verein.Id, Termin(w.EigeneGruppe.Id));

        Assert.True(termin.Succeeded);
        Assert.True(verwaltung.Succeeded);
    }

    [Fact]
    public async Task Termine_TrainingKannFremdenTerminNichtAbsagenAendernOderLoeschen_SiehtIhnNichtInDerListe()
    {
        var w = await AufbauenAsync();
        var training = await PersonAsync(w, ClubRole.Training);
        var fremd = (await w.Termine.CreateSessionAsync(w.Leitung, w.Verein.Id, Termin(w.FremdeGruppe.Id))).Value!;

        Assert.True((await w.Termine.CancelSessionAsync(training, fremd.Id)).IsNotFound);
        Assert.True((await w.Termine.DeleteSessionAsync(training, fremd.Id)).IsNotFound);
        Assert.True((await w.Termine.UpdateSessionAsync(training, fremd.Id,
            new UpdateSessionRequest(GroupTrainingCategory.Puppy, DateTimeOffset.UtcNow.AddDays(5), 60, null, null, [], []))).IsNotFound);
        var liste = await w.Termine.GetClubScheduleAsync(training, w.Verein.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), null, null, null, false);
        Assert.Empty(liste.Value!);
    }

    [Fact]
    public async Task Termine_EingetragenerTerminTrainer_SiehtDenTerminAuchOhneZugriffAufDieGruppe()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        var training = await PersonAsync(w, ClubRole.Training);
        var anderes = await PersonAsync(w, ClubRole.Training);
        var termin = (await w.Termine.CreateSessionAsync(wart, w.Verein.Id,
            new CreateSessionRequest(w.FremdeGruppe.Id, GroupTrainingCategory.Puppy, DateTimeOffset.UtcNow.AddDays(2), 60, null, null, [training], []))).Value!;

        var zeit = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var meine = await w.Termine.GetClubScheduleAsync(training, w.Verein.Id, zeit, null, null, null, true);
        var fremde = await w.Termine.GetClubScheduleAsync(anderes, w.Verein.Id, zeit, null, null, null, false);

        Assert.Equal(termin.Id, Assert.Single(meine.Value!).Id);
        Assert.Empty(fremde.Value!);
    }

    [Fact]
    public async Task Termine_MitgliederverwaltungUndVorstandHabenKeinenZugang()
    {
        var w = await AufbauenAsync();
        foreach (var rolle in new[] { ClubRole.Mitgliederverwaltung, ClubRole.Vorstand })
        {
            var id = await PersonAsync(w, rolle);
            Assert.False((await w.Termine.CreateSessionAsync(id, w.Verein.Id, Termin(w.FremdeGruppe.Id))).Succeeded, rolle.ToString());
            Assert.False((await w.Termine.GetClubScheduleAsync(id, w.Verein.Id, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, false)).Succeeded, rolle.ToString());
        }
    }

    [Fact]
    public async Task Termintrainer_NurMitTrainerRolle()
    {
        var w = await AufbauenAsync();
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);
        var request = Termin(w.FremdeGruppe.Id) with { TrainerUserIds = [vorstand] };

        var ergebnis = await w.Termine.CreateSessionAsync(w.Leitung, w.Verein.Id, request);

        Assert.False(ergebnis.Succeeded);
        Assert.DoesNotContain(vorstand, (await w.Termine.GetClubTrainersAsync(w.Leitung, w.Verein.Id)).Value!.Select(t => t.UserId));
    }

    // ------------------------------------------------------------ Bibliothek / Katalog

    [Fact]
    public async Task Bibliothek_AlleTrainerRollen_NichtMitgliederverwaltungOderVorstand()
    {
        var w = await AufbauenAsync();
        var bibliothek = new GroupTrainingService(w.Db);

        foreach (var (rolle, erlaubt) in new[]
        {
            (ClubRole.Training, true), (ClubRole.Ausbildungswart, true), (ClubRole.Verwaltung, true),
            (ClubRole.Mitgliederverwaltung, false), (ClubRole.Vorstand, false),
        })
        {
            var id = await PersonAsync(w, rolle);
            Assert.Equal(erlaubt, (await bibliothek.GetLibraryAsync(id, w.Verein.Id)).Succeeded);
        }
    }

    // ------------------------------------------------------------ Mitglieder

    [Fact]
    public async Task Mitgliederverwaltung_EntscheidetAnfragen_IstAberKeineTrainerin()
    {
        var w = await AufbauenAsync();
        var verwaltung = await PersonAsync(w, ClubRole.Mitgliederverwaltung);
        var anfragende = Guid.NewGuid();
        w.Nutzer.Register(anfragende, "neu@example.org", "Nina", "Neu");
        var anfrage = (await w.Vereine.RequestJoinAsync(anfragende, w.Verein.Id)).Value!;

        var liste = await w.Vereine.GetJoinRequestsAsync(verwaltung, w.Verein.Id);
        var entscheidung = await w.Vereine.DecideJoinRequestAsync(verwaltung, w.Verein.Id, anfrage.Id, approve: true);

        Assert.Single(liste.Value!);
        Assert.True(entscheidung.Succeeded);
        Assert.False(await w.Db.IsAnyTrainerAsync(verwaltung));
        Assert.True((await w.Vereine.GetMembersAsync(verwaltung, w.Verein.Id)).Succeeded);
        Assert.True((await w.Vereine.GetInvitationsAsync(verwaltung, w.Verein.Id)).Succeeded);
    }

    [Theory]
    [InlineData(ClubRole.Training)]
    [InlineData(ClubRole.Ausbildungswart)]
    [InlineData(ClubRole.Vorstand)]
    public async Task Anfragen_Mitglieder_Einladungen_NurFuerMitgliederverwaltungUndVerwaltung(ClubRole rolle)
    {
        var w = await AufbauenAsync();
        var id = await PersonAsync(w, rolle);
        var anfragende = Guid.NewGuid();
        w.Nutzer.Register(anfragende, "neu@example.org", "Nina", "Neu");
        var anfrage = (await w.Vereine.RequestJoinAsync(anfragende, w.Verein.Id)).Value!;
        var invites = new ClubInviteService(w.Db, w.Vereine);

        Assert.True((await w.Vereine.GetJoinRequestsAsync(id, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Vereine.DecideJoinRequestAsync(id, w.Verein.Id, anfrage.Id, approve: true)).IsNotFound);
        Assert.True((await w.Vereine.GetMembersAsync(id, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Vereine.GetInvitationsAsync(id, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Vereine.AddMemberAsync(id, isAdmin: false, w.Verein.Id, new AssignClubMemberRequest("irgendwer@example.org"))).IsNotFound);
        Assert.True((await invites.GetAsync(id, w.Verein.Id)).IsNotFound);
        Assert.True((await invites.RegenerateAsync(id, w.Verein.Id)).IsNotFound);
        Assert.True((await invites.DisableAsync(id, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Vereine.RemoveClubMemberAsync(id, w.Verein.Id, anfragende)).IsNotFound);
    }

    [Fact]
    public async Task Mitgliederverwaltung_EntferntMitglieder_ChefsUndTrainerAberErstNachDemRollenentzug()
    {
        var w = await AufbauenAsync();
        var verwaltung = await PersonAsync(w, ClubRole.Mitgliederverwaltung);
        var einfach = await MitgliedOhneRolleAsync(w);
        var mitRolle = await PersonAsync(w, ClubRole.Training);

        Assert.True((await w.Vereine.RemoveClubMemberAsync(verwaltung, w.Verein.Id, einfach)).Succeeded);
        Assert.False(await w.Db.ClubMemberships.AnyAsync(m => m.UserId == einfach));

        var abgelehnt = await w.Vereine.RemoveClubMemberAsync(verwaltung, w.Verein.Id, mitRolle);
        Assert.False(abgelehnt.Succeeded);
        Assert.True(await w.Db.ClubMemberships.AnyAsync(m => m.UserId == mitRolle));
        Assert.True(await w.Db.ClubTrainers.AnyAsync(t => t.UserId == mitRolle));
    }

    [Fact]
    public async Task Mitgliederliste_ZeigtNurTrainerRollenAlsTrainer()
    {
        var w = await AufbauenAsync();
        var verwaltung = await PersonAsync(w, ClubRole.Mitgliederverwaltung);
        var training = await PersonAsync(w, ClubRole.Training);
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);

        var liste = (await w.Vereine.GetMembersAsync(verwaltung, w.Verein.Id)).Value!;

        Assert.True(liste.Single(m => m.UserId == training).IsTrainer);
        Assert.False(liste.Single(m => m.UserId == vorstand).IsTrainer);
        Assert.False(liste.Single(m => m.UserId == verwaltung).IsTrainer);
    }

    [Fact]
    public async Task Einladungslink_BeitrittMitRolleIstNichtNoetig()
    {
        var w = await AufbauenAsync();
        var invites = new ClubInviteService(w.Db, w.Vereine);
        var code = (await invites.RegenerateAsync(w.Leitung, w.Verein.Id)).Value!.Code;
        var vorstand = await PersonAsync(w, ClubRole.Vorstand, mitglied: false);

        var ergebnis = await invites.JoinAsync(vorstand, code);

        Assert.False(ergebnis.Succeeded);
        Assert.False(await w.Db.ClubMemberships.AnyAsync(m => m.UserId == vorstand));
    }

    // ------------------------------------------------------------ Rollen vergeben

    [Fact]
    public async Task Verwaltung_VergibtJedeRolle_AuchKombiniert()
    {
        var w = await AufbauenAsync();
        var person = await MitgliedOhneRolleAsync(w);

        var ergebnis = await w.Vereine.AssignTrainerAsync(w.Leitung, false, w.Verein.Id,
            new AssignClubTrainerRequest(Mail(w, person), ClubRole.Training | ClubRole.Vorstand));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(ClubRole.Training | ClubRole.Vorstand, await w.Db.GetClubRolesAsync(person, w.Verein.Id));
        Assert.True(await w.Db.IsAnyTrainerAsync(person));
    }

    [Fact]
    public async Task Ausbildungswart_VergibtTraining_ABERKeineAnderenRollen()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        var person = await MitgliedOhneRolleAsync(w);

        foreach (var rolle in new[] { ClubRole.Verwaltung, ClubRole.Ausbildungswart, ClubRole.Mitgliederverwaltung, ClubRole.Vorstand, ClubRole.Training | ClubRole.Vorstand })
        {
            var abgelehnt = await w.Vereine.AssignTrainerAsync(wart, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, person), rolle));
            Assert.False(abgelehnt.Succeeded, rolle.ToString());
        }
        Assert.False(await w.Db.ClubTrainers.AnyAsync(t => t.UserId == person));

        var erlaubt = await w.Vereine.AssignTrainerAsync(wart, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, person), ClubRole.Training));
        Assert.True(erlaubt.Succeeded);
        Assert.Equal(ClubRole.Training, await w.Db.GetClubRolesAsync(person, w.Verein.Id));
    }

    [Fact]
    public async Task Ausbildungswart_FuegtTrainingZuAnderenRollenHinzu_OhneSieZuBeruehren()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);

        var dazu = await w.Vereine.UpdateTrainerRoleAsync(wart, false, w.Verein.Id, vorstand, ClubRole.Vorstand | ClubRole.Training);
        Assert.True(dazu.Succeeded);
        Assert.Equal(ClubRole.Vorstand | ClubRole.Training, await w.Db.GetClubRolesAsync(vorstand, w.Verein.Id));
        Assert.True(await w.Db.IsAnyTrainerAsync(vorstand));

        // Nur Training wieder weg: Vorstand bleibt, die Person ist keine Trainer:in mehr.
        var weg = await w.Vereine.UpdateTrainerRoleAsync(wart, false, w.Verein.Id, vorstand, ClubRole.Vorstand);
        Assert.True(weg.Succeeded);
        Assert.Equal(ClubRole.Vorstand, await w.Db.GetClubRolesAsync(vorstand, w.Verein.Id));
        Assert.DoesNotContain(vorstand, w.Nutzer.TrainerRole);
    }

    [Fact]
    public async Task Ausbildungswart_KannAndereRollenWederHinzufuegenNochEntziehen()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        var training = await PersonAsync(w, ClubRole.Training);
        var vorstand = await PersonAsync(w, ClubRole.Training | ClubRole.Vorstand);

        // Zur Verwaltung machen - sich selbst oder andere.
        Assert.False((await w.Vereine.UpdateTrainerRoleAsync(wart, false, w.Verein.Id, training, ClubRole.Training | ClubRole.Verwaltung)).Succeeded);
        Assert.False((await w.Vereine.UpdateTrainerRoleAsync(wart, false, w.Verein.Id, wart, ClubRole.Ausbildungswart | ClubRole.Verwaltung)).Succeeded);
        // Der Verwaltung die Rolle nehmen.
        Assert.False((await w.Vereine.UpdateTrainerRoleAsync(wart, false, w.Verein.Id, w.Leitung, ClubRole.Training)).Succeeded);
        Assert.False((await w.Vereine.RemoveTrainerAsync(wart, false, w.Verein.Id, w.Leitung)).Succeeded);
        // Vorstand nehmen: weder per Menge noch durch Abberufen der ganzen Person.
        Assert.False((await w.Vereine.UpdateTrainerRoleAsync(wart, false, w.Verein.Id, vorstand, ClubRole.Training)).Succeeded);
        Assert.False((await w.Vereine.RemoveTrainerAsync(wart, false, w.Verein.Id, vorstand)).Succeeded);

        Assert.Equal(ClubRole.Training, await w.Db.GetClubRolesAsync(training, w.Verein.Id));
        Assert.Equal(ClubRole.Ausbildungswart, await w.Db.GetClubRolesAsync(wart, w.Verein.Id));
        Assert.Equal(ClubRole.Verwaltung, await w.Db.GetClubRolesAsync(w.Leitung, w.Verein.Id));
        Assert.Equal(ClubRole.Training | ClubRole.Vorstand, await w.Db.GetClubRolesAsync(vorstand, w.Verein.Id));
    }

    [Fact]
    public async Task Ausbildungswart_BerufenUndAbberufen_NurReineTrainer()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        var training = await PersonAsync(w, ClubRole.Training);

        var weg = await w.Vereine.RemoveTrainerAsync(wart, false, w.Verein.Id, training);

        Assert.True(weg.Succeeded);
        Assert.False(await w.Db.ClubTrainers.AnyAsync(t => t.UserId == training));
        Assert.DoesNotContain(training, w.Nutzer.TrainerRole);
    }

    [Fact]
    public async Task Ausbildungswart_BefoerdertMitgliedZurTrainerin_AuchMitVorhandenerRolle()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);
        var einfach = await MitgliedOhneRolleAsync(w);
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);

        Assert.True((await w.Vereine.PromoteMemberToTrainerAsync(wart, w.Verein.Id, einfach)).Succeeded);
        Assert.True((await w.Vereine.PromoteMemberToTrainerAsync(wart, w.Verein.Id, vorstand)).Succeeded);

        Assert.Equal(ClubRole.Training, await w.Db.GetClubRolesAsync(einfach, w.Verein.Id));
        Assert.Equal(ClubRole.Training | ClubRole.Vorstand, await w.Db.GetClubRolesAsync(vorstand, w.Verein.Id));
        // Wer schon trainiert, wird nicht ein zweites Mal befördert.
        Assert.False((await w.Vereine.PromoteMemberToTrainerAsync(wart, w.Verein.Id, einfach)).Succeeded);
    }

    [Theory]
    [InlineData(ClubRole.Training)]
    [InlineData(ClubRole.Mitgliederverwaltung)]
    [InlineData(ClubRole.Vorstand)]
    public async Task OhneRechtZumBerufen_NotFound(ClubRole rolle)
    {
        var w = await AufbauenAsync();
        var id = await PersonAsync(w, rolle);
        var person = await MitgliedOhneRolleAsync(w);

        Assert.True((await w.Vereine.AssignTrainerAsync(id, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, person)))).IsNotFound);
        Assert.True((await w.Vereine.PromoteMemberToTrainerAsync(id, w.Verein.Id, person)).IsNotFound);
        Assert.True((await w.Vereine.UpdateTrainerRoleAsync(id, false, w.Verein.Id, w.Leitung, ClubRole.Training)).IsNotFound);
        Assert.True((await w.Vereine.RemoveTrainerAsync(id, false, w.Verein.Id, w.Leitung)).IsNotFound);
        Assert.True((await w.Vereine.GetRoleHoldersAsync(id, false, w.Verein.Id)).IsNotFound);
        Assert.True((await w.Vereine.UpdateClubAsync(id, false, w.Verein.Id, new UpdateClubRequest("Neu", null))).IsNotFound);
    }

    [Fact]
    public async Task Ausbildungswart_SiehtDieRollenListe_KannAberDenVereinNichtUmbenennen()
    {
        var w = await AufbauenAsync();
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);

        var liste = (await w.Vereine.GetRoleHoldersAsync(wart, false, w.Verein.Id)).Value!;

        Assert.Contains(liste, p => p.UserId == w.Leitung && p.Roles == ClubRole.Verwaltung);
        Assert.Contains(liste, p => p.UserId == wart && p.Roles == ClubRole.Ausbildungswart);
        Assert.True((await w.Vereine.UpdateClubAsync(wart, false, w.Verein.Id, new UpdateClubRequest("Neu", null))).IsNotFound);
        Assert.Equal("Hundesportverein Nord", (await w.Db.Clubs.SingleAsync()).Name);
    }

    [Fact]
    public async Task Verwaltung_BenenntDenVereinUm()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Vereine.UpdateClubAsync(w.Leitung, false, w.Verein.Id, new UpdateClubRequest("  Neuer Name ", " Beschreibung "));

        Assert.True(ergebnis.Succeeded);
        var verein = await w.Db.Clubs.SingleAsync();
        Assert.Equal("Neuer Name", verein.Name);
        Assert.Equal("Beschreibung", verein.Description);
    }

    [Fact]
    public async Task UngueltigeRollen_WerdenAbgewiesen()
    {
        var w = await AufbauenAsync();
        var person = await MitgliedOhneRolleAsync(w);

        Assert.False((await w.Vereine.AssignTrainerAsync(w.Leitung, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, person), 0))).Succeeded);
        Assert.False((await w.Vereine.AssignTrainerAsync(w.Leitung, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, person), (ClubRole)32))).Succeeded);
        Assert.False((await w.Vereine.UpdateTrainerRoleAsync(w.Leitung, false, w.Verein.Id, w.Leitung, (ClubRole)64 | ClubRole.Verwaltung)).Succeeded);
        Assert.False(await w.Db.ClubTrainers.AnyAsync(t => t.UserId == person));
    }

    [Fact]
    public async Task NurMitgliederBekommenRollen_DerAdminDarfAuchNichtMitglieder()
    {
        var w = await AufbauenAsync();
        var fremd = Guid.NewGuid();
        w.Nutzer.Register(fremd, "fremd@example.org", "Frieda", "Fremd");

        var verwaltung = await w.Vereine.AssignTrainerAsync(w.Leitung, false, w.Verein.Id, new AssignClubTrainerRequest("fremd@example.org", ClubRole.Vorstand));
        var admin = await w.Vereine.AssignTrainerAsync(Guid.NewGuid(), true, w.Verein.Id, new AssignClubTrainerRequest("fremd@example.org", ClubRole.Vorstand));

        Assert.False(verwaltung.Succeeded);
        Assert.True(admin.Succeeded);
    }

    [Fact]
    public async Task LeereMengeEntferntDieZeile_UndEinWiederBerufenerBekommtDieNeueMenge()
    {
        var w = await AufbauenAsync();
        var person = await PersonAsync(w, ClubRole.Training);

        Assert.True((await w.Vereine.UpdateTrainerRoleAsync(w.Leitung, false, w.Verein.Id, person, 0)).Succeeded);
        Assert.False(await w.Db.ClubTrainers.AnyAsync(t => t.UserId == person));

        Assert.True((await w.Vereine.AssignTrainerAsync(w.Leitung, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, person), ClubRole.Vorstand))).Succeeded);
        Assert.Equal(ClubRole.Vorstand, await w.Db.GetClubRolesAsync(person, w.Verein.Id));
    }

    // ------------------------------------------------------------ Letzte Verwaltung

    [Fact]
    public async Task LetzteVerwaltung_KannWederGehenNochSichHerabstufen_AuchNichtMitAndererRolleDazu()
    {
        var w = await AufbauenAsync();
        // Verwaltung neben anderen Rollen ändert nichts: Es zählt das Bit.
        var row = await w.Db.ClubTrainers.SingleAsync(t => t.UserId == w.Leitung);
        row.Role = ClubRole.Verwaltung | ClubRole.Vorstand;
        await w.Db.SaveChangesAsync();

        Assert.False((await w.Vereine.UpdateTrainerRoleAsync(w.Leitung, false, w.Verein.Id, w.Leitung, ClubRole.Training | ClubRole.Vorstand)).Succeeded);
        Assert.False((await w.Vereine.UpdateTrainerRoleAsync(w.Leitung, false, w.Verein.Id, w.Leitung, 0)).Succeeded);
        Assert.False((await w.Vereine.RemoveTrainerAsync(w.Leitung, false, w.Verein.Id, w.Leitung)).Succeeded);
        // Auch der Admin hebelt den Schutz nicht aus.
        Assert.False((await w.Vereine.RemoveTrainerAsync(Guid.NewGuid(), true, w.Verein.Id, w.Leitung)).Succeeded);
        Assert.Equal(ClubRole.Verwaltung | ClubRole.Vorstand, await w.Db.GetClubRolesAsync(w.Leitung, w.Verein.Id));

        // Die Rollen NEBEN der Verwaltung dürfen sich ändern.
        Assert.True((await w.Vereine.UpdateTrainerRoleAsync(w.Leitung, false, w.Verein.Id, w.Leitung, ClubRole.Verwaltung | ClubRole.Training)).Succeeded);
    }

    [Fact]
    public async Task LetzteVerwaltung_KannDenVereinNichtVerlassen_AuchNichtDurchEntfernenDesAdmins()
    {
        var w = await AufbauenAsync();
        w.Db.ClubMemberships.Add(new ClubMembership { ClubId = w.Verein.Id, UserId = w.Leitung, Status = ClubMembershipStatus.Approved });
        await w.Db.SaveChangesAsync();

        Assert.False((await w.Vereine.LeaveClubAsync(w.Leitung, w.Verein.Id)).Succeeded);
        Assert.False((await w.Vereine.RemoveMemberAsync(w.Verein.Id, w.Leitung)).Succeeded);
        Assert.Equal(ClubRole.Verwaltung, await w.Db.GetClubRolesAsync(w.Leitung, w.Verein.Id));
        Assert.True(await w.Db.ClubMemberships.AnyAsync(m => m.UserId == w.Leitung));

        // Mit einer zweiten Verwaltung darf die erste gehen.
        await PersonAsync(w, ClubRole.Verwaltung);
        Assert.True((await w.Vereine.LeaveClubAsync(w.Leitung, w.Verein.Id)).Succeeded);
    }

    [Fact]
    public async Task Mitgliederliste_KennzeichnetJedeRolle_NichtNurTrainerRollen()
    {
        var w = await AufbauenAsync();
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);
        var training = await PersonAsync(w, ClubRole.Training);
        var ohne = await MitgliedOhneRolleAsync(w);

        var liste = (await w.Vereine.GetMembersAsync(w.Leitung, w.Verein.Id)).Value!.ToDictionary(m => m.UserId);

        Assert.True(liste[vorstand].HatRolle);
        Assert.False(liste[vorstand].IsTrainer);
        Assert.True(liste[training].HatRolle);
        Assert.True(liste[training].IsTrainer);
        Assert.False(liste[ohne].HatRolle);
    }

    [Fact]
    public async Task MitZweiterVerwaltung_DarfDieErsteGehen()
    {
        var w = await AufbauenAsync();
        var zweite = await PersonAsync(w, ClubRole.Verwaltung);

        Assert.True((await w.Vereine.UpdateTrainerRoleAsync(w.Leitung, false, w.Verein.Id, w.Leitung, ClubRole.Training)).Succeeded);
        Assert.False((await w.Vereine.RemoveTrainerAsync(zweite, false, w.Verein.Id, zweite)).Succeeded);
    }

    // ------------------------------------------------------------ Nachfolge

    [Fact]
    public async Task Nachfolge_DienstaeltesteMitTrainerRolle_BehaeltMitgliederverwaltungUndVorstand()
    {
        var w = await AufbauenAsync();
        // Älter, aber keine Trainerin: rückt nicht nach.
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);
        var mitgliederverwaltung = await PersonAsync(w, ClubRole.Mitgliederverwaltung);
        var nachfolgerin = await PersonAsync(w, ClubRole.Training | ClubRole.Vorstand);
        var spaeter = await PersonAsync(w, ClubRole.Ausbildungswart);
        var jetzt = DateTimeOffset.UtcNow;
        foreach (var (id, alter) in new[] { (vorstand, 50), (mitgliederverwaltung, 40), (nachfolgerin, 30), (spaeter, 10) })
            (await w.Db.ClubTrainers.SingleAsync(t => t.UserId == id)).CreatedAt = jetzt.AddDays(-alter);
        await w.Db.SaveChangesAsync();

        var befoerderungen = await ClubNachfolge.BefoerdernAsync(w.Db, [w.Leitung]);
        await w.Db.SaveChangesAsync();

        Assert.Equal(nachfolgerin, Assert.Single(befoerderungen).UserId);
        Assert.Equal(ClubRole.Verwaltung | ClubRole.Vorstand, await w.Db.GetClubRolesAsync(nachfolgerin, w.Verein.Id));
        Assert.Equal(ClubRole.Vorstand, await w.Db.GetClubRolesAsync(vorstand, w.Verein.Id));
        Assert.Equal(ClubRole.Mitgliederverwaltung, await w.Db.GetClubRolesAsync(mitgliederverwaltung, w.Verein.Id));
    }

    [Fact]
    public async Task Nachfolge_OhneTrainerRolleImVerein_RuecktNiemand()
    {
        var w = await AufbauenAsync();
        await PersonAsync(w, ClubRole.Vorstand);
        await PersonAsync(w, ClubRole.Mitgliederverwaltung);

        var befoerderungen = await ClubNachfolge.BefoerdernAsync(w.Db, [w.Leitung]);

        Assert.Empty(befoerderungen);
    }

    [Fact]
    public async Task Nachfolge_VerwaltungNebenAndererRolle_ZaehltAlsVerwaltung()
    {
        var w = await AufbauenAsync();
        var zweite = await PersonAsync(w, ClubRole.Verwaltung | ClubRole.Vorstand);
        await PersonAsync(w, ClubRole.Training);

        var befoerderungen = await ClubNachfolge.BefoerdernAsync(w.Db, [w.Leitung]);

        Assert.Empty(befoerderungen);
        Assert.True(await w.Db.CanManageClubAsync(zweite, w.Verein.Id));
    }

    // ------------------------------------------------------------ Listen und Meldungen

    [Fact]
    public async Task MeineVereine_TrainerSeitenSehenNurTrainerRollen_DieVereinsseiteAlle()
    {
        var w = await AufbauenAsync();
        var vorstand = await PersonAsync(w, ClubRole.Vorstand);
        var mitgliederverwaltung = await PersonAsync(w, ClubRole.Mitgliederverwaltung);
        var wart = await PersonAsync(w, ClubRole.Ausbildungswart);

        Assert.Empty((await w.Vereine.GetMyClubsAsync(vorstand)).Value!);
        Assert.Empty((await w.Vereine.GetMyClubsAsync(mitgliederverwaltung)).Value!);
        Assert.Single((await w.Vereine.GetMyClubsAsync(wart)).Value!);

        var seiteVorstand = Assert.Single((await w.Vereine.GetMyClubRolesAsync(vorstand)).Value!);
        Assert.Equal(ClubRole.Vorstand, seiteVorstand.Roles);
        Assert.False(seiteVorstand.CanManage || seiteVorstand.CanManageMembers || seiteVorstand.CanAppointTrainers || seiteVorstand.IsTrainer);

        var seiteMv = Assert.Single((await w.Vereine.GetMyClubRolesAsync(mitgliederverwaltung)).Value!);
        Assert.True(seiteMv.CanManageMembers);
        Assert.False(seiteMv.IsTrainer || seiteMv.CanAppointTrainers || seiteMv.CanManage);

        var seiteWart = Assert.Single((await w.Vereine.GetMyClubRolesAsync(wart)).Value!);
        Assert.True(seiteWart.CanAppointTrainers && seiteWart.IsTrainer);
        Assert.False(seiteWart.CanManage || seiteWart.CanManageMembers);

        var seiteLeitung = Assert.Single((await w.Vereine.GetMyClubRolesAsync(w.Leitung)).Value!);
        Assert.True(seiteLeitung.CanManage && seiteLeitung.CanManageMembers && seiteLeitung.CanAppointTrainers && seiteLeitung.IsTrainer);
    }

    [Fact]
    public async Task Vereinsliste_KenntRollenDerPerson_AuchOhneTrainerRolle()
    {
        var w = await AufbauenAsync();
        var vorstand = await PersonAsync(w, ClubRole.Vorstand, mitglied: false);

        var eintrag = Assert.Single((await w.Vereine.GetBrowsableClubsAsync(vorstand)).Value!);

        Assert.True(eintrag.IsTrainer);
        Assert.Equal(ClubRole.Vorstand, eintrag.Roles);
    }

    [Fact]
    public async Task NeueRolle_MeldetBescheid_MitDerVorhandenenMeldungFuerTrainerRollen()
    {
        var w = await AufbauenAsync();
        var training = await MitgliedOhneRolleAsync(w);
        var vorstand = await MitgliedOhneRolleAsync(w);

        await w.Vereine.AssignTrainerAsync(w.Leitung, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, training), ClubRole.Training));
        await w.Vereine.AssignTrainerAsync(w.Leitung, false, w.Verein.Id, new AssignClubTrainerRequest(Mail(w, vorstand), ClubRole.Vorstand));

        Assert.Contains(w.Meldungen.Created, m => m.UserId == training && m.Message == $"Du bist jetzt Trainer bei \"{w.Verein.Name}\"." && m.LinkPath == "/trainer");
        Assert.Contains(w.Meldungen.Created, m => m.UserId == vorstand && m.Message == $"Du hast im Verein \"{w.Verein.Name}\" eine neue Rolle bekommen." && m.LinkPath == "/clubs");
    }

    [Fact]
    public async Task EigeneRollenAendern_MeldetNichts()
    {
        var w = await AufbauenAsync();

        await w.Vereine.UpdateTrainerRoleAsync(w.Leitung, false, w.Verein.Id, w.Leitung, ClubRole.Verwaltung | ClubRole.Vorstand);

        Assert.Empty(w.Meldungen.Created);
    }

    [Fact]
    public async Task KontoDatenExport_NenntDieRollen()
    {
        var w = await AufbauenAsync();
        var person = await PersonAsync(w, ClubRole.Training | ClubRole.Vorstand, mitglied: false);
        var konten = new Dogity.Application.Account.AccountDataService(w.Db, w.Nutzer, w.Meldungen);

        var export = (await konten.ExportAsync(person)).Value!;

        Assert.Equal("Training, Vorstand", Assert.Single(export.Vereine).Vereinsrollen);
    }
}
