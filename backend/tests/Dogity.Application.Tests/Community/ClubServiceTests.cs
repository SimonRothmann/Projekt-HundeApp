using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Testet die berechtigungskritischen Scoping-Regeln von ClubService
/// (Beitrittsanfragen, Freigabe, Mitgliederliste, Beförderung, Verlassen)
/// gegen eine InMemory-ApplicationDbContext mit aktivierten Soft-Delete-
/// QueryFiltern - exakt wie im Produktionsbetrieb.
/// </summary>
public class ClubServiceTests
{
    private static ClubService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db, out FakeNotificationService notifications)
    {
        db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        notifications = new FakeNotificationService();
        return new ClubService(db, lookup, notifications);
    }

    private static async Task<(Guid TrainerId, Guid MemberId, Guid OtherUserId, Guid ClubId, ClubService Service)>
        SetupDefaultClubAsync()
    {
        var service = MakeService(out var db, out _);

        var trainerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        var club = new Club { Name = "Testverein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainerId });
        await db.SaveChangesAsync();

        return (trainerId, memberId, otherId, club.Id, service);
    }

    [Fact]
    public async Task RequestJoin_NewUser_CreatesAsPending()
    {
        var (_, memberId, _, clubId, service) = await SetupDefaultClubAsync();

        var result = await service.RequestJoinAsync(memberId, clubId);

        Assert.True(result.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, result.Value!.Status);
    }

    [Fact]
    public async Task RequestJoin_AlreadyPending_Fails()
    {
        var (_, memberId, _, clubId, service) = await SetupDefaultClubAsync();
        await service.RequestJoinAsync(memberId, clubId);

        var result = await service.RequestJoinAsync(memberId, clubId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RequestJoin_AfterRejection_Succeeds()
    {
        var service = MakeService(out var db, out var notifications);
        var trainerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainerId });
        await db.SaveChangesAsync();

        var req = await service.RequestJoinAsync(memberId, club.Id);
        await service.DecideJoinRequestAsync(trainerId, club.Id, req.Value!.Id, approve: false);

        var result = await service.RequestJoinAsync(memberId, club.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(ClubMembershipStatus.Pending, result.Value!.Status);
    }

    [Fact]
    public async Task DecideJoinRequest_ApproveByTrainer_SetsApprovedAndNotifies()
    {
        var service = MakeService(out var db, out var notifications);
        var trainerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainerId });
        await db.SaveChangesAsync();

        var req = await service.RequestJoinAsync(memberId, club.Id);
        var result = await service.DecideJoinRequestAsync(trainerId, club.Id, req.Value!.Id, approve: true);

        Assert.True(result.Succeeded);
        Assert.Single(notifications.Created, n => n.UserId == memberId && n.Message.Contains("angenommen"));
    }

    [Fact]
    public async Task DecideJoinRequest_ForeignTrainer_CannotSeeOtherClubRequests()
    {
        var service = MakeService(out var db, out var notifications);
        var trainerA = Guid.NewGuid();
        var trainerB = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var clubA = new Club { Name = "Verein A" };
        var clubB = new Club { Name = "Verein B" };
        db.Clubs.AddRange(clubA, clubB);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = clubA.Id, UserId = trainerA });
        db.ClubTrainers.Add(new ClubTrainer { ClubId = clubB.Id, UserId = trainerB });
        await db.SaveChangesAsync();

        var req = await service.RequestJoinAsync(memberId, clubA.Id);

        // trainerB (fremder Verein) versucht Anfrage in clubA zu sehen/entscheiden
        var listResult = await service.GetJoinRequestsAsync(trainerB, clubA.Id);
        Assert.False(listResult.Succeeded);

        var decideResult = await service.DecideJoinRequestAsync(trainerB, clubA.Id, req.Value!.Id, approve: true);
        Assert.False(decideResult.Succeeded);
    }

    [Fact]
    public async Task GetMembers_ForeignTrainer_Cannot_SeeOtherClubMembers()
    {
        var service = MakeService(out var db, out var notifications);
        var trainerA = Guid.NewGuid();
        var trainerB = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var clubA = new Club { Name = "Verein A" };
        var clubB = new Club { Name = "Verein B" };
        db.Clubs.AddRange(clubA, clubB);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = clubA.Id, UserId = trainerA });
        db.ClubTrainers.Add(new ClubTrainer { ClubId = clubB.Id, UserId = trainerB });
        db.ClubMemberships.Add(new ClubMembership { ClubId = clubA.Id, UserId = memberId, Status = ClubMembershipStatus.Approved });
        await db.SaveChangesAsync();

        var result = await service.GetMembersAsync(trainerB, clubA.Id);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PromoteMember_RequiresApprovedMembership()
    {
        var service = MakeService(out var db, out var notifications);
        var trainerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainerId });
        await db.SaveChangesAsync();

        // Ziel-User ist kein Mitglied → sollte scheitern
        var result = await service.PromoteMemberToTrainerAsync(trainerId, club.Id, targetId);
        Assert.False(result.Succeeded);

        // Ziel-User als Pending-Mitglied → immer noch scheitern
        db.ClubMemberships.Add(new ClubMembership { ClubId = club.Id, UserId = targetId, Status = ClubMembershipStatus.Pending });
        await db.SaveChangesAsync();
        result = await service.PromoteMemberToTrainerAsync(trainerId, club.Id, targetId);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PromoteMember_ApprovedMember_CreatesTrainerAndNotifies()
    {
        var service = MakeService(out var db, out var notifications);
        var trainerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainerId });
        db.ClubMemberships.Add(new ClubMembership { ClubId = club.Id, UserId = targetId, Status = ClubMembershipStatus.Approved });
        await db.SaveChangesAsync();

        var result = await service.PromoteMemberToTrainerAsync(trainerId, club.Id, targetId);

        Assert.True(result.Succeeded);
        Assert.Single(notifications.Created, n => n.UserId == targetId && n.Message.Contains("Trainer"));
    }

    [Fact]
    public async Task LeaveClub_ActiveMember_SoftDeletesMembership()
    {
        var service = MakeService(out var db, out var notifications);
        var memberId = Guid.NewGuid();
        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        var membership = new ClubMembership { ClubId = club.Id, UserId = memberId, Status = ClubMembershipStatus.Approved };
        db.ClubMemberships.Add(membership);
        await db.SaveChangesAsync();

        var result = await service.LeaveClubAsync(memberId, club.Id);

        Assert.True(result.Succeeded);
        // Nach dem Verlassen: Membership ist soft-deleted → GetMyMemberships gibt leere Liste
        var myMemberships = await service.GetMyMembershipsAsync(memberId);
        Assert.Empty(myMemberships.Value!);
    }

    [Fact]
    public async Task LeaveClub_NoMembership_Fails()
    {
        var service = MakeService(out var db, out var notifications);
        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        await db.SaveChangesAsync();

        var result = await service.LeaveClubAsync(Guid.NewGuid(), club.Id);

        Assert.False(result.Succeeded);
    }
}
