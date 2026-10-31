using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Die Auswertung eines Ablaufs lässt sich neu anstoßen (POST .../evaluate, dokumentiert in
/// docs/TRACK_EVALUATION.md): wer die Fährte sehen darf, darf es; Fremde bekommen "nicht gefunden".
/// </summary>
public class EvaluateWalkRunTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private sealed record Welt(ApplicationDbContext Db, GpsTrackService Dienst, Guid Besitzer, Guid Track, Guid Run, Guid Hund);

    private static async Task<Welt> AufbauenAsync(int abweichungMeter = 0)
    {
        var db = InMemoryDbContext.Create();
        var dienst = new GpsTrackService(db, new FakeWeatherEnrichmentService());
        var besitzer = Guid.NewGuid();
        var hund = new Dog { Name = "Bello" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        var einheit = new TrainingSession { UserId = besitzer, DogId = hund.Id, Date = new DateOnly(2026, 9, 1), DurationMinutes = 30 };
        var spur = new GpsTrack { TrainingSessionId = einheit.Id };
        db.TrainingSessions.Add(einheit);
        db.GpsTracks.Add(spur);
        // Eine gerade Spur nach Norden, etwa 110 m lang.
        for (var i = 0; i <= 10; i++)
            db.GpsPoints.Add(new GpsPoint { TrackId = spur.Id, Latitude = 48.9 + i * 0.0001, Longitude = 8.5, Timestamp = T0.AddSeconds(i * 10) });
        var lauf = new GpsWalkRun { TrackId = spur.Id };
        // Der Ablauf liegt um abweichungMeter (Längengrad: ~73 m je 0,001 Grad bei 49 Grad) neben der Spur.
        for (var i = 0; i <= 10; i++)
            lauf.Points.Add(new GpsWalkPoint { WalkRunId = lauf.Id, Latitude = 48.9 + i * 0.0001, Longitude = 8.5 + abweichungMeter / 73_000.0, Timestamp = T0.AddHours(1).AddSeconds(i * 10) });
        db.GpsWalkRuns.Add(lauf);
        await db.SaveChangesAsync();
        return new Welt(db, dienst, besitzer, spur.Id, lauf.Id, hund.Id);
    }

    [Fact]
    public async Task DerBesitzerStoesstDieAuswertungAn_UndBekommtSieZurueck()
    {
        var w = await AufbauenAsync(abweichungMeter: 0);

        var ergebnis = await w.Dienst.EvaluateWalkRunAsync(w.Besitzer, w.Track, w.Run);

        Assert.True(ergebnis.Succeeded);
        Assert.NotNull(ergebnis.Value!.EvaluatedAt);
        Assert.True(ergebnis.Value.AvgDeviationMeters <= 1, "Der Ablauf liegt auf der Spur.");
        Assert.True(ergebnis.Value.OnTrackPercent >= 99);
    }

    [Fact]
    public async Task EineNeuAuswertungErsetztDenAltenStand()
    {
        var w = await AufbauenAsync(abweichungMeter: 20);
        var erste = (await w.Dienst.EvaluateWalkRunAsync(w.Besitzer, w.Track, w.Run)).Value!;
        Assert.True(erste.AvgDeviationMeters > 10);

        // Die Punkte des Ablaufs wandern auf die Spur; die Neu-Auswertung sieht den neuen Stand.
        foreach (var punkt in w.Db.GpsWalkPoints) punkt.Longitude = 8.5;
        await w.Db.SaveChangesAsync();
        var zweite = (await w.Dienst.EvaluateWalkRunAsync(w.Besitzer, w.Track, w.Run)).Value!;

        Assert.True(zweite.AvgDeviationMeters < 1);
    }

    [Fact]
    public async Task EineBetreuendeTrainerinDarfDieAuswertungAnstossen_EineFremdePersonNicht()
    {
        var w = await AufbauenAsync();
        var trainer = Guid.NewGuid();
        w.Db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainer, MemberId = w.Besitzer, DogId = w.Hund });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Dienst.EvaluateWalkRunAsync(trainer, w.Track, w.Run)).Succeeded);
        Assert.True((await w.Dienst.EvaluateWalkRunAsync(Guid.NewGuid(), w.Track, w.Run)).IsNotFound);
    }

    [Fact]
    public async Task EinUnbekannterAblaufOderEineAndereFaehrteIstNichtGefunden()
    {
        var w = await AufbauenAsync();

        Assert.True((await w.Dienst.EvaluateWalkRunAsync(w.Besitzer, w.Track, Guid.NewGuid())).IsNotFound);
        Assert.True((await w.Dienst.EvaluateWalkRunAsync(w.Besitzer, Guid.NewGuid(), w.Run)).IsNotFound);
    }
}
