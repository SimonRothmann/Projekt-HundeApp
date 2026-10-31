using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Die Frist zum Ablaufen (Fährte 24, Trail 48 Stunden nach dem Ende des Legens)
/// gilt auch im Dienst, nicht nur am Knopf der Oberfläche. Verglichen wird der
/// BEGINN DES ABLAUFS mit dem Ende des Legens: Ein offline aufgezeichneter Ablauf,
/// der erst Stunden später ankommt, darf nicht scheitern.
/// </summary>
public class AblaufFristTests
{
    private static readonly DateTimeOffset Gelegt = new(2020, 5, 3, 18, 0, 0, TimeSpan.Zero);

    private static async Task<(GpsTrackService Dienst, Guid UserId, Guid TrackId, ApplicationDbContext Db)> AufbauenAsync(
        GpsTrackKind art = GpsTrackKind.Faehrte, bool mitPunkten = true)
    {
        var db = InMemoryDbContext.Create();
        var dienst = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var userId = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = userId });
        var einheit = new TrainingSession { UserId = userId, DogId = hund.Id, Date = DateOnly.FromDateTime(Gelegt.UtcDateTime), DurationMinutes = 30 };
        var spur = new GpsTrack { TrainingSessionId = einheit.Id, Kind = art };
        db.TrainingSessions.Add(einheit);
        db.GpsTracks.Add(spur);
        if (mitPunkten)
        {
            db.GpsPoints.Add(new GpsPoint { TrackId = spur.Id, Latitude = 48.9, Longitude = 8.5, Timestamp = Gelegt.AddMinutes(-20) });
            db.GpsPoints.Add(new GpsPoint { TrackId = spur.Id, Latitude = 48.9, Longitude = 8.5001, Timestamp = Gelegt });
            // Ein Marker nach dem Legen verlängert die Frist nicht.
            db.GpsPoints.Add(new GpsPoint { TrackId = spur.Id, Latitude = 48.9, Longitude = 8.5, Timestamp = Gelegt.AddHours(10), PointType = GpsPointType.Manual });
        }
        await db.SaveChangesAsync();
        return (dienst, userId, spur.Id, db);
    }

    private static CreateGpsWalkRunRequest Ablauf(DateTimeOffset beginn) =>
        new(null, null, [new CreateGpsWalkPointRequest(48.9, 8.5, beginn, 5), new CreateGpsWalkPointRequest(48.9, 8.5001, beginn.AddMinutes(5), 5)]);

    [Theory]
    [InlineData(GpsTrackKind.Faehrte, 24, true)]
    [InlineData(GpsTrackKind.Faehrte, 25, false)]
    [InlineData(GpsTrackKind.Mantrailing, 48, true)]
    [InlineData(GpsTrackKind.Mantrailing, 49, false)]
    public async Task Ablauf_NurInnerhalbDerFristNachDemLegen(GpsTrackKind art, int stundenNachDemLegen, bool erlaubt)
    {
        var (dienst, user, spur, db) = await AufbauenAsync(art);

        var ergebnis = await dienst.AddWalkRunAsync(user, spur, Ablauf(Gelegt.AddHours(stundenNachDemLegen)));

        Assert.Equal(erlaubt, ergebnis.Succeeded);
        if (!erlaubt)
        {
            Assert.False(ergebnis.IsNotFound);
            Assert.Empty(db.GpsWalkRuns);
        }
    }

    [Fact]
    public async Task Ablauf_ZaehltDerBeginnDesAblaufs_NichtDerZeitpunktDerAnfrage()
    {
        // Das Legen liegt Jahre zurück, der Ablauf begann eine Stunde danach: so kommt er
        // aus der Offline-Warteschlange an.
        var (dienst, user, spur, _) = await AufbauenAsync();

        Assert.True((await dienst.AddWalkRunAsync(user, spur, Ablauf(Gelegt.AddHours(1)))).Succeeded);
    }

    [Fact]
    public async Task Ablauf_OhneAutomatischenPunkt_LaesstSichNichtPruefenUndGehtDurch()
    {
        var (dienst, user, spur, _) = await AufbauenAsync(mitPunkten: false);

        Assert.True((await dienst.AddWalkRunAsync(user, spur, Ablauf(Gelegt.AddDays(30)))).Succeeded);
    }
}
