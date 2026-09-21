using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Mitglied einer Gruppe wird nur, wer selbst zustimmt.
///
/// Bis 2026-09-21 reichte die E-Mail-Adresse einer fremden Person: Gruppe
/// anlegen (darf jede:r), Person hinzufügen (war sofort aktiv), sich selbst als
/// Betreuer:in ihres Hundes eintragen - und schon lagen Tagebuch, Ziele und
/// Fährten samt Standorten offen. Diese Tests halten die Kette an jeder Stelle
/// fest, an der sie jetzt reißt.
/// </summary>
public class GroupInvitationTests
{
    private const string OpferMail = "opfer@example.com";

    private sealed record Umgebung(
        ApplicationDbContext Db,
        FakeUserLookupService Lookup,
        FakeNotificationService Notifications,
        GroupService Service);

    private static Umgebung Aufbauen()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var notifications = new FakeNotificationService();
        var service = new GroupService(db, lookup, new TrainerRoleService(db, lookup), notifications);
        return new Umgebung(db, lookup, notifications, service);
    }

    /// <summary>Eine vereinsfreie Gruppe - die darf jede:r anlegen.</summary>
    private static async Task<(Guid TrainerId, Guid GroupId)> GruppeAsync(Umgebung u)
    {
        var trainerId = Guid.NewGuid();
        u.Lookup.Register(trainerId, "trainer@example.com", "Tina", "Trainer");
        var created = await u.Service.CreateAsync(trainerId, new CreateGroupRequest("Dienstagsgruppe", null, null));
        return (trainerId, created.Value!.Id);
    }

    private static async Task<(Guid UserId, Guid DogId)> MitgliedMitHundAsync(Umgebung u, string mail = OpferMail)
    {
        var userId = Guid.NewGuid();
        u.Lookup.Register(userId, mail, "Olga", "Opfer");
        var dog = new Dog { Name = "Bello" };
        u.Db.Dogs.Add(dog);
        u.Db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId });
        await u.Db.SaveChangesAsync();
        return (userId, dog.Id);
    }

    [Fact]
    public async Task FremdeEMailAdresse_ReichtNichtFuerZugriffAufDenHund()
    {
        var u = Aufbauen();
        var (angreifer, groupId) = await GruppeAsync(u);
        var (opfer, hund) = await MitgliedMitHundAsync(u);

        Assert.True((await u.Service.AddMemberAsync(angreifer, groupId, new AddMemberRequest(OpferMail))).Succeeded);

        // Weder die Hunde auflisten noch sich eintragen - und damit kein Zugriff.
        Assert.False((await u.Service.GetMemberDogsAsync(angreifer, groupId, opfer)).Succeeded);
        Assert.False((await u.Service.AssignTrainerToDogAsync(angreifer, groupId, new AssignTrainerRequest(opfer, hund))).Succeeded);
        Assert.False(await u.Db.HasDogAccessAsync(angreifer, hund));
    }

    [Fact]
    public async Task Hinzufuegen_LegtEinladungAn_UndBenachrichtigt()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        var (mitglied, _) = await MitgliedMitHundAsync(u);

        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        var zeile = await u.Db.GroupMembers.SingleAsync(m => m.GroupId == groupId && m.UserId == mitglied);
        Assert.Equal(GroupMemberStatus.Invited, zeile.Status);
        var hinweis = Assert.Single(u.Notifications.Created, n => n.UserId == mitglied);
        Assert.Contains("Dienstagsgruppe", hinweis.Message);

        // Die Einladung zählt nicht als Mitglied - weder in der Liste noch im Zähler.
        var detail = await u.Service.GetDetailAsync(trainer, groupId);
        Assert.Empty(detail.Value!.Members);
        Assert.Equal(0, detail.Value.Group.MemberCount);
        Assert.Equal(mitglied, Assert.Single(detail.Value.Invitations).UserId);
    }

    [Fact]
    public async Task Annehmen_MachtZumMitglied_UndErlaubtDieBetreuung()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        var (mitglied, hund) = await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        Assert.True((await u.Service.RespondToInvitationAsync(mitglied, groupId, accept: true)).Succeeded);

        Assert.True((await u.Service.AssignTrainerToDogAsync(trainer, groupId, new AssignTrainerRequest(mitglied, hund))).Succeeded);
        Assert.True(await u.Db.HasDogAccessAsync(trainer, hund));
        // Das Mitglied erfährt, wer ab jetzt mitliest.
        Assert.Contains(u.Notifications.Created, n => n.UserId == mitglied && n.Message.Contains("Bello"));
    }

    [Fact]
    public async Task Ablehnen_EntferntDieEinladung_ErneutEinladenGeht()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        var (mitglied, _) = await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        Assert.True((await u.Service.RespondToInvitationAsync(mitglied, groupId, accept: false)).Succeeded);

        Assert.False(await u.Db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == mitglied));
        Assert.True((await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail))).Succeeded);
        Assert.Equal(GroupMemberStatus.Invited,
            (await u.Db.GroupMembers.SingleAsync(m => m.GroupId == groupId && m.UserId == mitglied)).Status);
    }

    [Fact]
    public async Task Annehmen_OhneEinladung_Scheitert()
    {
        var u = Aufbauen();
        var (_, groupId) = await GruppeAsync(u);

        Assert.False((await u.Service.RespondToInvitationAsync(Guid.NewGuid(), groupId, accept: true)).Succeeded);
    }

    [Fact]
    public async Task EinladungAnnehmen_KannNurDieEingeladenePerson()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        var (mitglied, _) = await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        // Die Trainer:in kann nicht für das Mitglied zusagen.
        Assert.False((await u.Service.RespondToInvitationAsync(trainer, groupId, accept: true)).Succeeded);
        Assert.Equal(GroupMemberStatus.Invited,
            (await u.Db.GroupMembers.SingleAsync(m => m.GroupId == groupId && m.UserId == mitglied)).Status);
    }

    [Fact]
    public async Task Hinzufuegen_NachEigenerAnfrage_NimmtDirektAuf()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        var (mitglied, _) = await MitgliedMitHundAsync(u);
        await u.Service.RequestJoinGroupAsync(mitglied, groupId);

        Assert.True((await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail))).Succeeded);

        Assert.Equal(GroupMemberStatus.Active,
            (await u.Db.GroupMembers.SingleAsync(m => m.GroupId == groupId && m.UserId == mitglied)).Status);
    }

    [Fact]
    public async Task BeitretenWollen_NachEinladung_NimmtDieEinladungAn()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        var (mitglied, _) = await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        Assert.True((await u.Service.RequestJoinGroupAsync(mitglied, groupId)).Succeeded);

        Assert.Equal(GroupMemberStatus.Active,
            (await u.Db.GroupMembers.SingleAsync(m => m.GroupId == groupId && m.UserId == mitglied)).Status);
    }

    [Fact]
    public async Task EinladungDoppelt_Scheitert()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        var zweites = await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        Assert.False(zweites.Succeeded);
        Assert.Contains("eingeladen", zweites.Errors.Single());
    }

    [Fact]
    public async Task Einladungen_SiehtNurWerDieGruppeVerwaltet()
    {
        var u = Aufbauen();
        var (trainer, groupId) = await GruppeAsync(u);
        var (dabei, _) = await MitgliedMitHundAsync(u, "dabei@example.com");
        await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest("dabei@example.com"));
        await u.Service.RespondToInvitationAsync(dabei, groupId, accept: true);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));

        var alsMitglied = await u.Service.GetDetailAsync(dabei, groupId);

        Assert.True(alsMitglied.Succeeded);
        Assert.Empty(alsMitglied.Value!.Invitations);
    }

    [Fact]
    public async Task MeineMitgliedschaften_ZeigtEinladungenZuerst()
    {
        var u = Aufbauen();
        var (trainer, alteGruppe) = await GruppeAsync(u);
        var neueGruppe = (await u.Service.CreateAsync(trainer, new CreateGroupRequest("Anfänger", null, null))).Value!.Id;
        var (mitglied, _) = await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, alteGruppe, new AddMemberRequest(OpferMail));
        await u.Service.RespondToInvitationAsync(mitglied, alteGruppe, accept: true);
        await u.Service.AddMemberAsync(trainer, neueGruppe, new AddMemberRequest(OpferMail));

        var liste = (await u.Service.GetMyMembershipsAsync(mitglied)).Value!;

        Assert.Equal(2, liste.Count);
        Assert.True(liste[0].IsInvitation);
        Assert.Equal(neueGruppe, liste[0].GroupId);
        Assert.False(liste[1].IsInvitation);
        Assert.Equal("Tina Trainer", liste[1].TrainerName);
    }

    // --- Verlassen und Entfernen beenden die Betreuung ----------------------

    private static async Task<(Guid Trainer, Guid GroupId, Guid Mitglied, Guid Hund)> BetreuungAsync(Umgebung u)
    {
        var (trainer, groupId) = await GruppeAsync(u);
        var (mitglied, hund) = await MitgliedMitHundAsync(u);
        await u.Service.AddMemberAsync(trainer, groupId, new AddMemberRequest(OpferMail));
        await u.Service.RespondToInvitationAsync(mitglied, groupId, accept: true);
        Assert.True((await u.Service.AssignTrainerToDogAsync(trainer, groupId, new AssignTrainerRequest(mitglied, hund))).Succeeded);
        return (trainer, groupId, mitglied, hund);
    }

    [Fact]
    public async Task Verlassen_BeendetDieBetreuung()
    {
        var u = Aufbauen();
        var (trainer, groupId, mitglied, hund) = await BetreuungAsync(u);

        Assert.True((await u.Service.LeaveGroupAsync(mitglied, groupId)).Succeeded);

        Assert.False(await u.Db.HasDogAccessAsync(trainer, hund));
        Assert.Empty((await u.Service.GetMyMembershipsAsync(mitglied)).Value!);
    }

    [Fact]
    public async Task Entfernen_BeendetDieBetreuung()
    {
        var u = Aufbauen();
        var (trainer, groupId, mitglied, hund) = await BetreuungAsync(u);

        Assert.True((await u.Service.RemoveMemberAsync(trainer, groupId, mitglied)).Succeeded);

        Assert.False(await u.Db.HasDogAccessAsync(trainer, hund));
    }

    [Fact]
    public async Task Verlassen_LaesstBetreuungUeberAndereGruppeBestehen()
    {
        var u = Aufbauen();
        var (trainer, groupId, mitglied, hund) = await BetreuungAsync(u);
        var zweite = (await u.Service.CreateAsync(trainer, new CreateGroupRequest("Samstag", null, null))).Value!.Id;
        await u.Service.AddMemberAsync(trainer, zweite, new AddMemberRequest(OpferMail));
        await u.Service.RespondToInvitationAsync(mitglied, zweite, accept: true);

        await u.Service.LeaveGroupAsync(mitglied, groupId);

        Assert.True(await u.Db.HasDogAccessAsync(trainer, hund));
    }

    [Fact]
    public async Task Verlassen_LaesstBetreuungAndererTrainerUnberuehrt()
    {
        var u = Aufbauen();
        var (_, groupId, mitglied, hund) = await BetreuungAsync(u);
        // Eine zweite, unabhängige Gruppe mit eigener Trainerin.
        var andere = Guid.NewGuid();
        u.Lookup.Register(andere, "andere@example.com", "Ute", "Andere");
        var ihreGruppe = (await u.Service.CreateAsync(andere, new CreateGroupRequest("Fährte", null, null))).Value!.Id;
        await u.Service.AddMemberAsync(andere, ihreGruppe, new AddMemberRequest(OpferMail));
        await u.Service.RespondToInvitationAsync(mitglied, ihreGruppe, accept: true);
        await u.Service.AssignTrainerToDogAsync(andere, ihreGruppe, new AssignTrainerRequest(mitglied, hund));

        await u.Service.LeaveGroupAsync(mitglied, groupId);

        Assert.True(await u.Db.HasDogAccessAsync(andere, hund));
    }

    [Fact]
    public async Task Verlassen_OhneMitgliedschaft_Scheitert()
    {
        var u = Aufbauen();
        var (_, groupId) = await GruppeAsync(u);

        Assert.False((await u.Service.LeaveGroupAsync(Guid.NewGuid(), groupId)).Succeeded);
    }
}
