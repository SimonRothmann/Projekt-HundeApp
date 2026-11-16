using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Ein Ablauf trägt wie die Fährte eine Kennung vom Gerät: Kam die Antwort nicht an, legt das
/// erneute Senden aus der Offline-Warteschlange keinen zweiten an (und scheitert nicht an der
/// Regel "eine Spur wird nur einmal abgelaufen"). Ein zweiter Ablauf verfälschte die Statistik.
/// </summary>
public class AblaufKennungTests
{
    private static readonly DateTimeOffset Beginn = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    private static async Task<(GpsTrackService Dienst, Guid UserId, Guid TrackId, ApplicationDbContext Db)> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var dienst = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var userId = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = userId });
        var einheit = new TrainingSession { UserId = userId, DogId = hund.Id, Date = DateOnly.FromDateTime(Beginn.UtcDateTime), DurationMinutes = 30 };
        db.TrainingSessions.Add(einheit);
        var spur = new GpsTrack { TrainingSessionId = einheit.Id };
        db.GpsTracks.Add(spur);
        await db.SaveChangesAsync();
        return (dienst, userId, spur.Id, db);
    }

    private static CreateGpsWalkRunRequest Ablauf(Guid? id) =>
        new(null, null, [new CreateGpsWalkPointRequest(48.9, 8.5, Beginn, 5), new CreateGpsWalkPointRequest(48.9, 8.5001, Beginn.AddMinutes(5), 5)], id);

    [Fact]
    public async Task Ablauf_MitKennung_DasErneuteSendenLegtKeinenZweitenAnUndAntwortetMitDemVorhandenen()
    {
        var (dienst, user, spur, db) = await AufbauenAsync();
        var kennung = Guid.NewGuid();

        var erster = await dienst.AddWalkRunAsync(user, spur, Ablauf(kennung));
        var zweiter = await dienst.AddWalkRunAsync(user, spur, Ablauf(kennung));

        Assert.True(erster.Succeeded);
        Assert.True(zweiter.Succeeded);
        Assert.Equal(kennung, erster.Value!.Id);
        Assert.Equal(kennung, zweiter.Value!.Id);
        Assert.Equal(erster.Value.Points.Count, zweiter.Value.Points.Count);
        Assert.Equal(1, await db.GpsWalkRuns.CountAsync());
    }

    [Fact]
    public async Task Ablauf_OhneKennung_BleibtBeiDerRegelNurEinmalAbgelaufen()
    {
        var (dienst, user, spur, db) = await AufbauenAsync();

        Assert.True((await dienst.AddWalkRunAsync(user, spur, Ablauf(null))).Succeeded);
        var zweiter = await dienst.AddWalkRunAsync(user, spur, Ablauf(null));

        Assert.False(zweiter.Succeeded);
        Assert.Equal(1, await db.GpsWalkRuns.CountAsync());
    }

    [Fact]
    public async Task Ablauf_MitKennungEinerAnderenSpur_IstEin404()
    {
        var (dienst, user, spur, db) = await AufbauenAsync();
        var kennung = Guid.NewGuid();
        Assert.True((await dienst.AddWalkRunAsync(user, spur, Ablauf(kennung))).Succeeded);

        var andereSpur = new GpsTrack { TrainingSessionId = (await db.GpsTracks.FirstAsync()).TrainingSessionId };
        db.GpsTracks.Add(andereSpur);
        await db.SaveChangesAsync();

        var ergebnis = await dienst.AddWalkRunAsync(user, andereSpur.Id, Ablauf(kennung));

        Assert.True(ergebnis.IsNotFound);
        Assert.Equal(1, await db.GpsWalkRuns.CountAsync());
    }

    [Fact]
    public async Task Ablauf_MitKennung_FremdeBekommenDenVorhandenenNichtZuSehen()
    {
        var (dienst, user, spur, _) = await AufbauenAsync();
        var kennung = Guid.NewGuid();
        Assert.True((await dienst.AddWalkRunAsync(user, spur, Ablauf(kennung))).Succeeded);

        var ergebnis = await dienst.AddWalkRunAsync(Guid.NewGuid(), spur, Ablauf(kennung));

        Assert.True(ergebnis.IsNotFound);
    }
}
