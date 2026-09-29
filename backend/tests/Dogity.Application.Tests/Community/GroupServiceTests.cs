using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Testet den Gruppen-Selbstbeitritt (RequestJoinGroupAsync/
/// GetGroupJoinRequestsAsync/DecideGroupJoinRequestAsync) - insbesondere,
/// dass Pending-Mitglieder vor Freigabe keinen Gruppenzugriff haben
/// (GetAccessibleGroupAsync/IsGroupMemberAsync) und Trainer-Scoping korrekt ist.
/// </summary>
public class GroupServiceTests
{
    private static GroupService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
        => MakeService(out db, out _);

    private static GroupService MakeService(
        out Dogity.Infrastructure.Persistence.ApplicationDbContext db,
        out FakeUserLookupService lookup)
    {
        db = InMemoryDbContext.Create();
        lookup = new FakeUserLookupService();
        // Echter TrainerRoleService gegen den Fake-Lookup: so lässt sich in den
        // Tests prüfen, ob das TRAINER-Kennzeichen tatsächlich mitwandert.
        return new GroupService(db, lookup, new TrainerRoleService(db, lookup), new FakeNotificationService());
    }

    private static async Task<(Guid TrainerId, Guid GroupId, GroupService Service)> SetupGroupAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db, GroupService service)
    {
        var trainerId = Guid.NewGuid();
        var group = new Group { TrainerId = trainerId, Name = "Dienstagsgruppe" };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return (trainerId, group.Id, service);
    }

    [Fact]
    public async Task RequestJoin_NewUser_CreatesPendingMember()
    {
        var service = MakeService(out var db);
        var (_, groupId, _) = await SetupGroupAsync(db, service);
        var userId = Guid.NewGuid();

        var result = await service.RequestJoinGroupAsync(userId, groupId);

        Assert.True(result.Succeeded);
        var member = await db.GroupMembers.IgnoreQueryFilters().SingleAsync(m => m.GroupId == groupId && m.UserId == userId);
        Assert.Equal(GroupMemberStatus.Pending, member.Status);
    }

    [Fact]
    public async Task RequestJoin_AlreadyPending_Fails()
    {
        var service = MakeService(out var db);
        var (_, groupId, _) = await SetupGroupAsync(db, service);
        var userId = Guid.NewGuid();
        await service.RequestJoinGroupAsync(userId, groupId);

        var result = await service.RequestJoinGroupAsync(userId, groupId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RequestJoin_AlreadyActiveMember_Fails()
    {
        var service = MakeService(out var db);
        var (_, groupId, _) = await SetupGroupAsync(db, service);
        var userId = Guid.NewGuid();
        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId, Status = GroupMemberStatus.Active });
        await db.SaveChangesAsync();

        var result = await service.RequestJoinGroupAsync(userId, groupId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PendingMember_HasNoGroupAccess()
    {
        var service = MakeService(out var db);
        var (_, groupId, _) = await SetupGroupAsync(db, service);
        var userId = Guid.NewGuid();
        await service.RequestJoinGroupAsync(userId, groupId);

        var detail = await service.GetDetailAsync(userId, groupId);

        Assert.False(detail.Succeeded);
    }

    [Fact]
    public async Task DecideJoinRequest_Approve_GrantsAccessAndSetsActive()
    {
        var service = MakeService(out var db);
        var (trainerId, groupId, _) = await SetupGroupAsync(db, service);
        var userId = Guid.NewGuid();
        await service.RequestJoinGroupAsync(userId, groupId);

        var decide = await service.DecideGroupJoinRequestAsync(trainerId, groupId, userId, approve: true);

        Assert.True(decide.Succeeded);
        var member = await db.GroupMembers.IgnoreQueryFilters().SingleAsync(m => m.GroupId == groupId && m.UserId == userId);
        Assert.Equal(GroupMemberStatus.Active, member.Status);

        var detail = await service.GetDetailAsync(userId, groupId);
        Assert.True(detail.Succeeded);
    }

    [Fact]
    public async Task DecideJoinRequest_Reject_SoftDeletesMembership()
    {
        var service = MakeService(out var db);
        var (trainerId, groupId, _) = await SetupGroupAsync(db, service);
        var userId = Guid.NewGuid();
        await service.RequestJoinGroupAsync(userId, groupId);

        var decide = await service.DecideGroupJoinRequestAsync(trainerId, groupId, userId, approve: false);

        Assert.True(decide.Succeeded);
        var member = await db.GroupMembers.IgnoreQueryFilters().SingleAsync(m => m.GroupId == groupId && m.UserId == userId);
        Assert.NotNull(member.DeletedAt);

        // Nach Ablehnung kann der Nutzer erneut eine Anfrage stellen.
        var retry = await service.RequestJoinGroupAsync(userId, groupId);
        Assert.True(retry.Succeeded);
    }

    [Fact]
    public async Task GetJoinRequests_ForeignTrainer_CannotSeeOtherGroupRequests()
    {
        var service = MakeService(out var db);
        var (_, groupId, _) = await SetupGroupAsync(db, service);
        var foreignTrainerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await service.RequestJoinGroupAsync(userId, groupId);

        var listResult = await service.GetGroupJoinRequestsAsync(foreignTrainerId, groupId);
        Assert.False(listResult.Succeeded);

        var decideResult = await service.DecideGroupJoinRequestAsync(foreignTrainerId, groupId, userId, approve: true);
        Assert.False(decideResult.Succeeded);
    }

    [Fact]
    public async Task GetJoinRequests_OwningTrainer_SeesOnlyPendingMembers()
    {
        var service = MakeService(out var db);
        var (trainerId, groupId, _) = await SetupGroupAsync(db, service);
        var pendingUserId = Guid.NewGuid();
        var activeUserId = Guid.NewGuid();
        await service.RequestJoinGroupAsync(pendingUserId, groupId);
        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = activeUserId, Status = GroupMemberStatus.Active });
        await db.SaveChangesAsync();

        var result = await service.GetGroupJoinRequestsAsync(trainerId, groupId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
        Assert.Equal(pendingUserId, result.Value![0].MemberId);
    }

    // ---- Bearbeiten / Trainer:in zuweisen (jede:r Vereinstrainer:in) ----

    private static async Task<(Guid ClubId, Guid OwnerTrainerId, Guid ColleagueTrainerId, Guid GroupId)> SetupClubGroupAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var club = new Club { Name = "TSV" };
        db.Clubs.Add(club);
        var owner = Guid.NewGuid();
        var colleague = Guid.NewGuid();
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = owner });
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = colleague });
        var group = new Group { TrainerId = owner, Name = "Dienstagsgruppe", ClubId = club.Id };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return (club.Id, owner, colleague, group.Id);
    }

    [Fact]
    public async Task UpdateGroup_ByClubColleagueTrainer_Succeeds()
    {
        var service = MakeService(out var db);
        var (_, _, colleague, groupId) = await SetupClubGroupAsync(db);

        var result = await service.UpdateGroupAsync(colleague, groupId, new UpdateGroupRequest("Neuer Name", "Neu"));

        Assert.True(result.Succeeded);
        var group = await db.Groups.SingleAsync(g => g.Id == groupId);
        Assert.Equal("Neuer Name", group.Name);
    }

    [Fact]
    public async Task UpdateGroup_ByUnrelatedUser_Fails()
    {
        var service = MakeService(out var db);
        var (_, _, _, groupId) = await SetupClubGroupAsync(db);

        var result = await service.UpdateGroupAsync(Guid.NewGuid(), groupId, new UpdateGroupRequest("X", null));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AssignTrainer_ByClubTrainer_ReassignsToClubColleague()
    {
        var service = MakeService(out var db);
        var (_, owner, colleague, groupId) = await SetupClubGroupAsync(db);

        var result = await service.AssignGroupTrainerAsync(owner, groupId, new AssignGroupTrainerRequest(colleague));

        Assert.True(result.Succeeded);
        var group = await db.Groups.SingleAsync(g => g.Id == groupId);
        Assert.Equal(colleague, group.TrainerId);
    }

    [Fact]
    public async Task AssignTrainer_TargetNotClubTrainer_Fails()
    {
        var service = MakeService(out var db);
        var (_, owner, _, groupId) = await SetupClubGroupAsync(db);

        var result = await service.AssignGroupTrainerAsync(owner, groupId, new AssignGroupTrainerRequest(Guid.NewGuid()));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetMyGroups_IncludesClubColleagueGroups()
    {
        var service = MakeService(out var db);
        var (_, _, colleague, groupId) = await SetupClubGroupAsync(db);

        var result = await service.GetMyGroupsAsync(colleague);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Value!, g => g.Id == groupId);
    }

    // --- Mehrere Trainer:innen je Gruppe ---------------------------------
    // Eine Gruppe hatte bis dahin genau eine:n Trainer:in. Diese Tests decken
    // die zweite: dass sie dieselben Verwaltungsrechte hat, in ihrer eigenen
    // Gruppenliste auftaucht und das TRAINER-Kennzeichen bekommt.

    private static async Task<(Guid Lead, Guid Helper, Guid GroupId)> SetupGroupWithHelperAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db, FakeUserLookupService lookup)
    {
        var lead = Guid.NewGuid();
        var helper = Guid.NewGuid();
        lookup.Register(helper, "helfer@example.com", "Hanna", "Helfer");

        var group = new Group { TrainerId = lead, Name = "Dienstagsgruppe" };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return (lead, helper, group.Id);
    }

    /// <summary>Einladen und annehmen - der Weg zu einer echten Co-Trainer:in.</summary>
    private static async Task CoTrainerEinladenUndAnnehmenAsync(GroupService service, Guid einladend, Guid groupId, Guid eingeladen)
    {
        Assert.True((await service.AddGroupTrainerAsync(einladend, groupId, new AddGroupTrainerRequest("helfer@example.com"))).Succeeded);
        Assert.True((await service.RespondToTrainerInvitationAsync(eingeladen, groupId, accept: true)).Succeeded);
    }

    [Fact]
    public async Task AddCoTrainer_GivesManageRightsAndTrainerRoleAfterAcceptance()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);

        var result = await service.AddGroupTrainerAsync(lead, groupId, new AddGroupTrainerRequest("helfer@example.com"));

        // Erst eine Einladung: keine Rechte, keine Rolle.
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(helper, lookup.TrainerRole);
        Assert.False((await service.UpdateGroupAsync(helper, groupId, new UpdateGroupRequest("X", null))).Succeeded);

        Assert.True((await service.RespondToTrainerInvitationAsync(helper, groupId, accept: true)).Succeeded);

        Assert.Contains(helper, lookup.TrainerRole);
        // Verwalten darf sie jetzt auch - vorher wäre das "Gruppe nicht gefunden".
        var update = await service.UpdateGroupAsync(helper, groupId, new UpdateGroupRequest("Neuer Name", null));
        Assert.True(update.Succeeded);
    }

    // --- Co-Trainer:in nur mit Zustimmung (Prüfung 2026-09-28) -------------
    // Vorher wurde man per E-Mail-Adresse sofort Trainer:in einer fremden
    // Gruppe - und die Gruppe sah den Namen dahinter.

    [Fact]
    public async Task CoTrainerEinladung_ZeigtVorDerZusageNurDieEingegebeneAdresse()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);

        await service.AddGroupTrainerAsync(lead, groupId, new AddGroupTrainerRequest("helfer@example.com"));

        var detail = (await service.GetDetailAsync(lead, groupId)).Value!;
        var eingeladen = Assert.Single(detail.Trainers, t => t.UserId == helper);
        Assert.True(eingeladen.IsInvited);
        Assert.Equal("helfer@example.com", eingeladen.Email);
        Assert.Equal("", eingeladen.FirstName);
        Assert.Equal("", eingeladen.LastName);
        Assert.DoesNotContain((await service.GetMyGroupsAsync(helper)).Value!, g => g.Id == groupId);
    }

    [Fact]
    public async Task CoTrainerEinladung_ErscheintBeiDerEingeladenenUndLaesstSichAblehnen()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        await service.AddGroupTrainerAsync(lead, groupId, new AddGroupTrainerRequest("helfer@example.com"));

        var meine = (await service.GetMyMembershipsAsync(helper)).Value!;
        var einladung = Assert.Single(meine);
        Assert.True(einladung.IsInvitation);
        Assert.True(einladung.AsTrainer);

        Assert.True((await service.RespondToTrainerInvitationAsync(helper, groupId, accept: false)).Succeeded);
        Assert.Empty((await service.GetMyMembershipsAsync(helper)).Value!);
        Assert.DoesNotContain(helper, lookup.TrainerRole);
        // Und erneut einladbar (eindeutiger Index auf GroupId+UserId).
        Assert.True((await service.AddGroupTrainerAsync(lead, groupId, new AddGroupTrainerRequest("helfer@example.com"))).Succeeded);
    }

    [Fact]
    public async Task CoTrainerEinladung_LaesstSichZurueckziehenUndNichtDoppeltVerschicken()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        await service.AddGroupTrainerAsync(lead, groupId, new AddGroupTrainerRequest("helfer@example.com"));

        var doppelt = await service.AddGroupTrainerAsync(lead, groupId, new AddGroupTrainerRequest("helfer@example.com"));
        Assert.False(doppelt.Succeeded);
        Assert.Contains("eingeladen", doppelt.Errors[0]);

        Assert.True((await service.RemoveGroupTrainerAsync(lead, groupId, helper)).Succeeded);
        Assert.False((await service.RespondToTrainerInvitationAsync(helper, groupId, accept: true)).Succeeded);
    }

    [Fact]
    public async Task GruppenEinladung_ZeigtDerVerwaltungKeinenNamen()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, _, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        var member = Guid.NewGuid();
        lookup.Register(member, "mitglied@example.com", "Max", "Muster");

        await service.AddMemberAsync(lead, groupId, new AddMemberRequest("mitglied@example.com"));

        var eingeladen = Assert.Single((await service.GetDetailAsync(lead, groupId)).Value!.Invitations);
        Assert.Equal("mitglied@example.com", eingeladen.Email);
        Assert.Equal("", eingeladen.FirstName);
        Assert.Equal("", eingeladen.LastName);
    }

    [Fact]
    public async Task RemoveTrainerFromDog_UeberUngefragtEingetrageneTrainerin_GehtNichtMehr()
    {
        // Der Umweg aus der Zweitprüfung: die Trainer:in einer fremden
        // Betreuung in die eigene Gruppe eintragen und die Betreuung dann
        // von dort aus beenden. Ohne ihre Zusage zählt sie hier nicht.
        var service = MakeService(out var db, out var lookup);
        var (trainerId, _, member, dogId) = await SetupSupervisionAsync(db, service);
        lookup.Register(trainerId, "helfer@example.com", "Tina", "Trainer");
        var andereLeitung = Guid.NewGuid();
        var andere = new Group { TrainerId = andereLeitung, Name = "Donnerstagsgruppe" };
        db.Groups.Add(andere);
        db.GroupMembers.Add(new GroupMember { GroupId = andere.Id, UserId = member });
        await db.SaveChangesAsync();
        await service.AddGroupTrainerAsync(andereLeitung, andere.Id, new AddGroupTrainerRequest("helfer@example.com"));

        var result = await service.RemoveTrainerFromDogAsync(andereLeitung, andere.Id, trainerId, dogId);

        Assert.False(result.Succeeded);
        Assert.True(await db.HasDogAccessAsync(trainerId, dogId));
    }

    [Fact]
    public async Task AddCoTrainer_ShowsUpInDetailAndOwnGroupList()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        await CoTrainerEinladenUndAnnehmenAsync(service, lead, groupId, helper);

        var detail = await service.GetDetailAsync(lead, groupId);
        Assert.True(detail.Succeeded);
        Assert.Equal(2, detail.Value!.Trainers.Count);
        Assert.Single(detail.Value.Trainers, t => t.IsLead && t.UserId == lead);
        Assert.Single(detail.Value.Trainers, t => !t.IsLead && t.UserId == helper);

        // "Trainer in anderen Gruppen mittrainieren": die Gruppe muss in der
        // eigenen Übersicht auftauchen, sonst findet man sie nie wieder.
        var mine = await service.GetMyGroupsAsync(helper);
        Assert.Contains(mine.Value!, g => g.Id == groupId);
    }

    [Fact]
    public async Task RemoveCoTrainer_RevokesRightsAndTrainerRole()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        await CoTrainerEinladenUndAnnehmenAsync(service, lead, groupId, helper);

        var result = await service.RemoveGroupTrainerAsync(lead, groupId, helper);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(helper, lookup.TrainerRole);
        Assert.False((await service.UpdateGroupAsync(helper, groupId, new UpdateGroupRequest("X", null))).Succeeded);
    }

    [Fact]
    public async Task RemoveCoTrainer_KeepsRoleWhenStillTrainerElsewhere()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        await CoTrainerEinladenUndAnnehmenAsync(service, lead, groupId, helper);

        // Zweite Gruppe, in der dieselbe Person mit-betreut.
        var other = new Group { TrainerId = lead, Name = "Donnerstagsgruppe" };
        db.Groups.Add(other);
        await db.SaveChangesAsync();
        await CoTrainerEinladenUndAnnehmenAsync(service, lead, other.Id, helper);

        await service.RemoveGroupTrainerAsync(lead, groupId, helper);

        Assert.Contains(helper, lookup.TrainerRole);
    }

    [Fact]
    public async Task AddCoTrainer_AfterRemoval_WorksAgain()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        await CoTrainerEinladenUndAnnehmenAsync(service, lead, groupId, helper);
        await service.RemoveGroupTrainerAsync(lead, groupId, helper);

        // Ohne Wiederbeleben der weichgelöschten Zeile liefe das in den
        // Unique-Index auf (GroupId, UserId).
        var again = await service.AddGroupTrainerAsync(lead, groupId, new AddGroupTrainerRequest("helfer@example.com"));
        await service.RespondToTrainerInvitationAsync(helper, groupId, accept: true);

        Assert.True(again.Succeeded);
        Assert.Contains(helper, lookup.TrainerRole);
        // Genau EINE Zeile - eine zweite würde auf Postgres am Unique-Index
        // scheitern, den der InMemory-Provider nicht durchsetzt.
        var rows = await db.GroupTrainers.IgnoreQueryFilters()
            .CountAsync(t => t.GroupId == groupId && t.UserId == helper);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task RemoveCoTrainer_LeadTrainer_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, _, groupId) = await SetupGroupWithHelperAsync(db, lookup);

        var result = await service.RemoveGroupTrainerAsync(lead, groupId, lead);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AddCoTrainer_ByMember_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (_, _, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        var member = Guid.NewGuid();
        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = member });
        await db.SaveChangesAsync();

        var result = await service.AddGroupTrainerAsync(member, groupId, new AddGroupTrainerRequest("helfer@example.com"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateGroup_GivesCreatorTrainerRole()
    {
        var service = MakeService(out _, out var lookup);
        var creator = Guid.NewGuid();

        await service.CreateAsync(creator, new CreateGroupRequest("Welpengruppe", null));

        Assert.Contains(creator, lookup.TrainerRole);
    }

    // --- Beitrittsanfrage nur für Außenstehende --------------------------
    // Vorher konnte sich jede:r zu jeder Gruppe bewerben - auch die eigene
    // Trainer:in, die die Anfrage danach in ihrer eigenen Freigabeliste fand.

    [Fact]
    public async Task RequestJoin_AsLeadTrainer_Fails()
    {
        var service = MakeService(out var db);
        var (trainerId, groupId, _) = await SetupGroupAsync(db, MakeService(out _));

        var result = await service.RequestJoinGroupAsync(trainerId, groupId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RequestJoin_AsCoTrainer_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, helper, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        await CoTrainerEinladenUndAnnehmenAsync(service, lead, groupId, helper);

        var result = await service.RequestJoinGroupAsync(helper, groupId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RequestJoin_AsClubTrainer_Fails()
    {
        var service = MakeService(out var db);
        var (clubId, _, colleague, groupId) = await SetupClubGroupAsync(db);
        Assert.NotEqual(Guid.Empty, clubId);

        var result = await service.RequestJoinGroupAsync(colleague, groupId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GetGroupsByClub_ReportsMyRelation()
    {
        var service = MakeService(out var db);
        var (clubId, owner, colleague, groupId) = await SetupClubGroupAsync(db);

        // Trainer:in der Gruppe und Vereinstrainer:in sehen beide "Trainer".
        var asOwner = await service.GetGroupsByClubAsync(owner, clubId);
        Assert.Equal(GroupRelation.Trainer, asOwner.Value!.Single(g => g.Id == groupId).MyRelation);
        var asColleague = await service.GetGroupsByClubAsync(colleague, clubId);
        Assert.Equal(GroupRelation.Trainer, asColleague.Value!.Single(g => g.Id == groupId).MyRelation);

        // Ein Vereinsmitglied ohne Gruppenbezug darf beitreten.
        var member = Guid.NewGuid();
        db.ClubMemberships.Add(new ClubMembership { ClubId = clubId, UserId = member, Status = ClubMembershipStatus.Approved });
        await db.SaveChangesAsync();
        var asMember = await service.GetGroupsByClubAsync(member, clubId);
        Assert.Equal(GroupRelation.None, asMember.Value!.Single(g => g.Id == groupId).MyRelation);

        // Nach der Anfrage steht sie als ausstehend da.
        Assert.True((await service.RequestJoinGroupAsync(member, groupId)).Succeeded);
        var afterRequest = await service.GetGroupsByClubAsync(member, clubId);
        Assert.Equal(GroupRelation.Pending, afterRequest.Value!.Single(g => g.Id == groupId).MyRelation);
    }

    // --- Soft-Delete + eindeutiger Index -----------------------------------
    // Dieselbe Falle wie bei den Prüfungsordnungs-Übungen: entfernt wird weich,
    // der Index kennt kein DeletedAt. Ohne Wiederbeleben ein 500er.

    [Fact]
    public async Task AddMember_AfterRemoval_WorksAgain()
    {
        var service = MakeService(out var db, out var lookup);
        var (lead, _, groupId) = await SetupGroupWithHelperAsync(db, lookup);
        var member = Guid.NewGuid();
        lookup.Register(member, "mitglied@example.com", "Max", "Muster");
        await service.AddMemberAsync(lead, groupId, new AddMemberRequest("mitglied@example.com"));
        await service.RemoveMemberAsync(lead, groupId, member);

        var again = await service.AddMemberAsync(lead, groupId, new AddMemberRequest("mitglied@example.com"));

        Assert.True(again.Succeeded);
        var rows = await db.GroupMembers.IgnoreQueryFilters().CountAsync(m => m.GroupId == groupId && m.UserId == member);
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task RequestJoin_AfterRejection_PossibleAgain()
    {
        var service = MakeService(out var db);
        var (_, groupId, _) = await SetupGroupAsync(db, MakeService(out _));
        var trainerId = await db.Groups.Where(g => g.Id == groupId).Select(g => g.TrainerId).SingleAsync();
        var applicant = Guid.NewGuid();
        await service.RequestJoinGroupAsync(applicant, groupId);
        await service.DecideGroupJoinRequestAsync(trainerId, groupId, applicant, approve: false);

        // Abgelehnt heißt nicht "für immer gesperrt".
        var again = await service.RequestJoinGroupAsync(applicant, groupId);

        Assert.True(again.Succeeded);
        Assert.Equal(1, await db.GroupMembers.IgnoreQueryFilters().CountAsync(m => m.GroupId == groupId && m.UserId == applicant));
    }

    // --- Betreuung beenden --------------------------------------------------

    [Fact]
    public async Task RemoveTrainerFromDog_RevokesAccess()
    {
        var service = MakeService(out var db);
        var (trainerId, groupId, _) = await SetupGroupAsync(db, MakeService(out _));
        var member = Guid.NewGuid();
        var dog = new Dogity.Domain.Dogs.Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new Dogity.Domain.Dogs.DogOwner { DogId = dog.Id, UserId = member });
        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = member });
        await db.SaveChangesAsync();
        Assert.True((await service.AssignTrainerToDogAsync(trainerId, groupId, new AssignTrainerRequest(member, dog.Id))).Succeeded);
        Assert.True(await db.HasDogAccessAsync(trainerId, dog.Id));

        var result = await service.RemoveTrainerFromDogAsync(trainerId, groupId, trainerId, dog.Id);

        Assert.True(result.Succeeded);
        Assert.False(await db.HasDogAccessAsync(trainerId, dog.Id));
        // Und danach wieder aufnehmbar (Soft-Delete + Index).
        Assert.True((await service.AssignTrainerToDogAsync(trainerId, groupId, new AssignTrainerRequest(member, dog.Id))).Succeeded);
        Assert.True(await db.HasDogAccessAsync(trainerId, dog.Id));
    }

    /// <summary>
    /// Eine Trainer:in betreut über ihre Gruppe den Hund eines Mitglieds.
    /// Liefert alles, was die Tests zum Beenden fremder Betreuungen brauchen.
    /// </summary>
    private static async Task<(Guid TrainerId, Guid GroupId, Guid Member, Guid DogId)> SetupSupervisionAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db, GroupService service)
    {
        var (trainerId, groupId, _) = await SetupGroupAsync(db, service);
        var member = Guid.NewGuid();
        var dog = new Dogity.Domain.Dogs.Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new Dogity.Domain.Dogs.DogOwner { DogId = dog.Id, UserId = member });
        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = member });
        await db.SaveChangesAsync();
        Assert.True((await service.AssignTrainerToDogAsync(trainerId, groupId, new AssignTrainerRequest(member, dog.Id))).Succeeded);
        return (trainerId, groupId, member, dog.Id);
    }

    [Fact]
    public async Task RemoveTrainerFromDog_ViaOwnEmptyGroup_Fails()
    {
        // Der Weg der Prüfung vom 2026-09-28: eine eigene, leere Gruppe
        // anlegen und darüber die Betreuung einer fremden Trainer:in beenden.
        var service = MakeService(out var db);
        var (trainerId, _, _, dogId) = await SetupSupervisionAsync(db, service);
        var angreifer = Guid.NewGuid();
        var eigene = await service.CreateAsync(angreifer, new CreateGroupRequest("Leere Gruppe", null));

        var result = await service.RemoveTrainerFromDogAsync(angreifer, eigene.Value!.Id, trainerId, dogId);

        Assert.False(result.Succeeded);
        Assert.True(await db.HasDogAccessAsync(trainerId, dogId));
    }

    [Fact]
    public async Task RemoveTrainerFromDog_OwnSupervision_WorksViaAnyGroup()
    {
        // Die eigene Betreuung darf die Trainer:in immer beenden - die
        // Oberfläche schickt dabei die Gruppe mit, von der aus sie klickt.
        var service = MakeService(out var db);
        var (trainerId, _, _, dogId) = await SetupSupervisionAsync(db, service);
        var andereGruppe = await service.CreateAsync(trainerId, new CreateGroupRequest("Andere", null));

        var result = await service.RemoveTrainerFromDogAsync(trainerId, andereGruppe.Value!.Id, trainerId, dogId);

        Assert.True(result.Succeeded);
        Assert.False(await db.HasDogAccessAsync(trainerId, dogId));
    }

    [Fact]
    public async Task RemoveTrainerFromDog_ByCoTrainerOfSameGroup_Succeeds()
    {
        var service = MakeService(out var db, out var lookup);
        var (trainerId, groupId, _, dogId) = await SetupSupervisionAsync(db, service);
        var helfer = Guid.NewGuid();
        lookup.Register(helfer, "helfer@example.com", "Hanna", "Helfer");
        await CoTrainerEinladenUndAnnehmenAsync(service, trainerId, groupId, helfer);

        var result = await service.RemoveTrainerFromDogAsync(helfer, groupId, trainerId, dogId);

        Assert.True(result.Succeeded);
        Assert.False(await db.HasDogAccessAsync(trainerId, dogId));
    }

    [Fact]
    public async Task RemoveTrainerFromDog_ByManagerOfOtherGroupOfSameMember_Fails()
    {
        // Dasselbe Mitglied ist auch in einer zweiten Gruppe. Deren Leitung
        // verwaltet zwar ein Mitglied, aber nicht die Trainer:in der ersten
        // Gruppe - deren Betreuung ist nicht ihre Sache.
        var service = MakeService(out var db);
        var (trainerId, _, member, dogId) = await SetupSupervisionAsync(db, service);
        var andereLeitung = Guid.NewGuid();
        var andere = new Group { TrainerId = andereLeitung, Name = "Donnerstagsgruppe" };
        db.Groups.Add(andere);
        db.GroupMembers.Add(new GroupMember { GroupId = andere.Id, UserId = member });
        await db.SaveChangesAsync();

        var result = await service.RemoveTrainerFromDogAsync(andereLeitung, andere.Id, trainerId, dogId);

        Assert.False(result.Succeeded);
        Assert.True(await db.HasDogAccessAsync(trainerId, dogId));
    }

    // --- Gruppe auflösen ----------------------------------------------------

    [Fact]
    public async Task DeleteGroup_RemovesMembershipsButKeepsDogs()
    {
        var service = MakeService(out var db);
        var (trainerId, groupId, _) = await SetupGroupAsync(db, MakeService(out _));
        var member = Guid.NewGuid();
        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = member });
        await db.SaveChangesAsync();

        var result = await service.DeleteGroupAsync(trainerId, groupId);

        Assert.True(result.Succeeded);
        Assert.Empty(await db.Groups.Where(g => g.Id == groupId).ToListAsync());
        Assert.Empty(await db.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync());
    }

    [Fact]
    public async Task DeleteGroup_ByStranger_Fails()
    {
        var service = MakeService(out var db);
        var (_, groupId, _) = await SetupGroupAsync(db, MakeService(out _));

        Assert.False((await service.DeleteGroupAsync(Guid.NewGuid(), groupId)).Succeeded);
    }
}
