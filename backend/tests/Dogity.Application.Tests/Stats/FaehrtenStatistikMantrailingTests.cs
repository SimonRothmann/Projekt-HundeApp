using Dogity.Application.Stats;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Stats;

/// <summary>
/// Der Fährten-Verlauf am Hund vergleicht Abweichungen. Läufe auf einer Mantrailing-Spur werden mit
/// anderen Grenzen bewertet (10/25 statt 3/6 m) und gehören deshalb nicht hinein.
/// </summary>
public class FaehrtenStatistikMantrailingTests
{
    [Fact]
    public async Task DogTrackStats_ZaehltNurFaehrtenKeineMantrailingLaeufe()
    {
        var db = InMemoryDbContext.Create();
        var service = new StatsService(db, TimeProvider.System);
        var userId = Guid.NewGuid();
        var dog = new Dog { Name = "Rex" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId });
        var session = new TrainingSession { UserId = userId, DogId = dog.Id, Date = new DateOnly(2026, 10, 1), DurationMinutes = 30 };
        db.TrainingSessions.Add(session);
        foreach (var (kind, abweichung) in new[] { (GpsTrackKind.Faehrte, 2.0), (GpsTrackKind.Mantrailing, 14.0) })
        {
            var track = new GpsTrack { TrainingSessionId = session.Id, Kind = kind };
            db.GpsTracks.Add(track);
            db.GpsWalkRuns.Add(new GpsWalkRun
            {
                TrackId = track.Id, AvgDeviationMeters = abweichung, OnTrackPercent = 80, EvaluatedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var ergebnis = await service.GetDogTrackStatsAsync(userId, dog.Id);

        var lauf = Assert.Single(ergebnis.Value!.Runs);
        Assert.Equal(2.0, lauf.AvgDeviationMeters);
    }
}
