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
    {
        db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        return new GroupService(db, lookup);
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
}
