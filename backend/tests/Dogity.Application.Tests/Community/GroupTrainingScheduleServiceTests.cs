using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Testet die Gruppentraining-Terminplanung (siehe docs/GROUP_TRAINING_SCHEDULE.md):
/// ClubTrainer planen Termine (Inhalt = Bausteine + Freitext, mehrere Trainer),
/// Serien-Generator, Absagen; Mitglieder sehen nur die Termine ihrer Gruppen.
/// </summary>
public class GroupTrainingScheduleServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private static GroupTrainingScheduleService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new GroupTrainingScheduleService(db, new FakeUserLookupService());
    }

    private static async Task<(Guid UserId, Guid ClubId, Guid GroupId)> SetupAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db)
    {
        var userId = Guid.NewGuid();
        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = userId });
        var group = new Group { TrainerId = userId, Name = "Dienstagsgruppe", ClubId = club.Id };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return (userId, club.Id, group.Id);
    }

    private static async Task<Guid> AddExerciseAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db, Guid clubId, int min = 10)
    {
        var e = new GroupTrainingExercise { ClubId = clubId, Title = "Baustein", Category = GroupTrainingCategory.Puppy, DurationMinutes = min };
        db.GroupTrainingExercises.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private static CreateSessionRequest Req(Guid groupId, Guid exId, Guid trainerId) =>
        new(groupId, GroupTrainingCategory.Puppy, DateTimeOffset.UtcNow.AddDays(1), 60, "Wald", null,
            [trainerId],
            [new SessionContentInput(ExerciseId: exId), new SessionContentInput(FreeText: "Spielen zum Abschluss")]);

    [Fact]
    public async Task CreateSession_ClubTrainer_PersistsWithContentAndTrainer()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var exId = await AddExerciseAsync(db, s.ClubId, min: 10);

        var result = await service.CreateSessionAsync(s.UserId, s.ClubId, Req(s.GroupId, exId, s.UserId));

        Assert.True(result.Succeeded);
        Assert.Equal("Wald", result.Value!.Location);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(exId, result.Value.Items[0].ExerciseId);
        Assert.Equal("Spielen zum Abschluss", result.Value.Items[1].FreeText);
        Assert.Equal(10, result.Value.PlannedMinutes); // Freitext = 0 Min
        Assert.Contains(result.Value.Trainers, t => t.UserId == s.UserId);
    }

    [Fact]
    public async Task CreateSession_NonTrainer_Fails()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var exId = await AddExerciseAsync(db, s.ClubId);

        var result = await service.CreateSessionAsync(Guid.NewGuid(), s.ClubId, Req(s.GroupId, exId, s.UserId));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateSession_GroupNotInClub_Fails()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var foreignGroup = new Group { TrainerId = Guid.NewGuid(), Name = "Fremd", ClubId = Guid.NewGuid() };
        db.Groups.Add(foreignGroup);
        await db.SaveChangesAsync();
        var exId = await AddExerciseAsync(db, s.ClubId);

        var result = await service.CreateSessionAsync(s.UserId, s.ClubId, Req(foreignGroup.Id, exId, s.UserId));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateSession_ItemWithBothOrNeither_Fails()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var req = new CreateSessionRequest(s.GroupId, GroupTrainingCategory.Puppy, DateTimeOffset.UtcNow.AddDays(1), 60, null, null,
            [s.UserId], [new SessionContentInput(ExerciseId: null, FreeText: null)]);
        var result = await service.CreateSessionAsync(s.UserId, s.ClubId, req);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Cancel_SetsStatusCancelled()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var exId = await AddExerciseAsync(db, s.ClubId);
        var created = await service.CreateSessionAsync(s.UserId, s.ClubId, Req(s.GroupId, exId, s.UserId));

        var cancel = await service.CancelSessionAsync(s.UserId, created.Value!.Id);

        Assert.True(cancel.Succeeded);
        var list = await service.GetClubScheduleAsync(s.UserId, s.ClubId, Today, null, null, null, false);
        Assert.Equal(GroupTrainingSessionStatus.Cancelled, list.Value!.Single().Status);
    }

    [Fact]
    public async Task Update_ReplacesItemsAndTrainers()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var exId = await AddExerciseAsync(db, s.ClubId);
        var created = await service.CreateSessionAsync(s.UserId, s.ClubId, Req(s.GroupId, exId, s.UserId));

        var otherTrainer = Guid.NewGuid();
        db.ClubTrainers.Add(new ClubTrainer { ClubId = s.ClubId, UserId = otherTrainer });
        await db.SaveChangesAsync();

        var update = await service.UpdateSessionAsync(s.UserId, created.Value!.Id,
            new UpdateSessionRequest(GroupTrainingCategory.YoungDog, DateTimeOffset.UtcNow.AddDays(2), 90, "Parkplatz", "Notiz",
                [otherTrainer], [new SessionContentInput(FreeText: "Nur Freitext")]));

        Assert.True(update.Succeeded);
        Assert.Equal(GroupTrainingCategory.YoungDog, update.Value!.Category);
        Assert.Equal("Parkplatz", update.Value.Location);
        Assert.Single(update.Value.Items);
        Assert.Equal("Nur Freitext", update.Value.Items[0].FreeText);
        Assert.Single(update.Value.Trainers);
        Assert.Equal(otherTrainer, update.Value.Trainers[0].UserId);
    }

    [Fact]
    public async Task GenerateSeries_CreatesOnePerStart()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var exId = await AddExerciseAsync(db, s.ClubId);
        var starts = new[] { DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(8), DateTimeOffset.UtcNow.AddDays(15) };

        var result = await service.GenerateSeriesAsync(s.UserId, s.ClubId,
            new GenerateSeriesRequest(s.GroupId, GroupTrainingCategory.YoungDog, starts, 60, "Platz", [s.UserId],
                [new SessionContentInput(ExerciseId: exId)]));

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Value!.Count);
        Assert.All(result.Value, x => Assert.Single(x.Items));
    }

    [Fact]
    public async Task MemberSchedule_ShowsOnlyOwnGroupsSessions()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var exId = await AddExerciseAsync(db, s.ClubId);
        await service.CreateSessionAsync(s.UserId, s.ClubId, Req(s.GroupId, exId, s.UserId));

        var memberId = Guid.NewGuid();
        // Noch kein Mitglied -> sieht nichts
        var before = await service.GetMemberScheduleAsync(memberId, Today);
        Assert.Empty(before.Value!);

        db.GroupMembers.Add(new GroupMember { GroupId = s.GroupId, UserId = memberId, Status = GroupMemberStatus.Active });
        await db.SaveChangesAsync();

        var after = await service.GetMemberScheduleAsync(memberId, Today);
        Assert.Single(after.Value!);
    }

    private static async Task AddExAsync(Dogity.Infrastructure.Persistence.ApplicationDbContext db, Guid clubId, GroupTrainingCategory cat, string focus, string title)
    {
        db.GroupTrainingExercises.Add(new GroupTrainingExercise { ClubId = clubId, Category = cat, Title = title, Focus = focus, DurationMinutes = 10 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GenerateContent_Puppy_FollowsSkeleton_AnkommenFirst_SpielenLast()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var p = GroupTrainingCategory.Puppy;
        await AddExAsync(db, s.ClubId, p, "Ankommen", "Ankommen A");
        await AddExAsync(db, s.ClubId, p, "Entspannung", "Ruhe");
        await AddExAsync(db, s.ClubId, p, "Futterhand", "Futterhand folgen");
        await AddExAsync(db, s.ClubId, p, "Sozialisierung", "Sozialkontakt");
        await AddExAsync(db, s.ClubId, p, "Hinterhandarbeit", "Podest");
        await AddExAsync(db, s.ClubId, p, "Spielen", "Freies Spiel");

        var result = await service.GenerateContentAsync(s.UserId, s.ClubId, p);

        Assert.True(result.Succeeded);
        var focuses = result.Value!.Select(e => e.Focus).ToList();
        Assert.Equal("Ankommen", focuses.First());
        Assert.Equal("Spielen", focuses.Last());
        Assert.Contains("Futterhand", focuses);
    }

    [Fact]
    public async Task GenerateContent_YoungDog_StartsWithLeinenfuehrigkeit()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var j = GroupTrainingCategory.YoungDog;
        await AddExAsync(db, s.ClubId, j, "Leinenführigkeit", "LF lockere Leine");
        await AddExAsync(db, s.ClubId, j, "Ablenkung", "Bleib mit Ablenkung");
        await AddExAsync(db, s.ClubId, j, "Ablage", "Ablage auf Distanz");
        await AddExAsync(db, s.ClubId, j, "Hinterhandarbeit", "Pivot");

        var result = await service.GenerateContentAsync(s.UserId, s.ClubId, j);

        Assert.True(result.Succeeded);
        Assert.Equal("Leinenführigkeit", result.Value!.First().Focus);
        Assert.True(result.Value.Count >= 2);
    }

    [Fact]
    public async Task GenerateContent_EmptyPool_ReturnsEmpty_NoCrash()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.GenerateContentAsync(s.UserId, s.ClubId, GroupTrainingCategory.Basis);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetClubTrainers_ReturnsAllClubTrainers()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var other = Guid.NewGuid();
        db.ClubTrainers.Add(new ClubTrainer { ClubId = s.ClubId, UserId = other });
        await db.SaveChangesAsync();

        var result = await service.GetClubTrainersAsync(s.UserId, s.ClubId);

        Assert.True(result.Succeeded);
        var ids = result.Value!.Select(t => t.UserId).ToHashSet();
        Assert.Contains(s.UserId, ids);
        Assert.Contains(other, ids);
    }

    [Fact]
    public async Task GenerateSeries_AutoGenerateContent_FillsEachSession()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var p = GroupTrainingCategory.Puppy;
        await AddExAsync(db, s.ClubId, p, "Ankommen", "Ankommen");
        await AddExAsync(db, s.ClubId, p, "Entspannung", "Ruhe");
        await AddExAsync(db, s.ClubId, p, "Futterhand", "Futterhand");
        await AddExAsync(db, s.ClubId, p, "Spielen", "Spielen");

        var starts = new[] { DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(8) };
        var result = await service.GenerateSeriesAsync(s.UserId, s.ClubId,
            new GenerateSeriesRequest(s.GroupId, p, starts, 60, null, [s.UserId], Array.Empty<SessionContentInput>(), AutoGenerateContent: true));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, x => Assert.NotEmpty(x.Items)); // je Termin ein generierter Mix
    }
}
