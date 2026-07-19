using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Testet das nachträgliche Bearbeiten des Ablauf-Versuch-Kommentars
/// (GpsWalkRun.Comment) - inkl. Zugriffsschutz über den Hund der zugehörigen
/// Trainingseinheit (siehe Wunsch 1 / TODO).
/// </summary>
public class GpsTrackServiceTests
{
    private sealed record Setup(Guid UserId, Guid TrackId, Guid WalkRunId);

    private static async Task<(GpsTrackService Service, Setup Setup)> MakeAsync()
    {
        var db = InMemoryDbContext.Create();
        var service = new GpsTrackService(db);

        var userId = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId, Role = DogOwnerRole.Owner });
        var session = new TrainingSession { UserId = userId, DogId = dog.Id, Date = DateOnly.FromDateTime(DateTime.Today), DurationMinutes = 30 };
        var track = new GpsTrack { TrainingSessionId = session.Id };
        var walkRun = new GpsWalkRun { TrackId = track.Id, Comment = null };
        db.TrainingSessions.Add(session);
        db.GpsTracks.Add(track);
        db.GpsWalkRuns.Add(walkRun);
        await db.SaveChangesAsync();

        return (service, new Setup(userId, track.Id, walkRun.Id));
    }

    [Fact]
    public async Task UpdateWalkRun_ByOwner_SetsAndTrimsComment()
    {
        var (service, s) = await MakeAsync();

        var result = await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("  bei Regen gelaufen  "));

        Assert.True(result.Succeeded);
        Assert.Equal("bei Regen gelaufen", result.Value!.Comment);
    }

    [Fact]
    public async Task UpdateWalkRun_EmptyComment_ClearsToNull()
    {
        var (service, s) = await MakeAsync();
        await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("erst was"));

        var result = await service.UpdateWalkRunAsync(s.UserId, s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("   "));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Comment);
    }

    [Fact]
    public async Task UpdateWalkRun_ByOtherUser_Fails()
    {
        var (service, s) = await MakeAsync();

        var result = await service.UpdateWalkRunAsync(Guid.NewGuid(), s.TrackId, s.WalkRunId, new UpdateGpsWalkRunRequest("fremd"));

        Assert.False(result.Succeeded);
    }
}
