using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Paket F4, Fährte: Bodenzustand beim Legen und "Verdeckt absuchen" (DoubleBlind bei der Fährte, nur solange
/// noch kein Ablauf existiert, mit den Rechten des Bearbeitens).
/// </summary>
public class FaehrteBodenVerdecktTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private const double BaseLat = 50.0;
    private const double BaseLon = 8.0;
    private const double MetersPerDegreeLat = 111320.0;

    private static double LatAt(double meters) => BaseLat + meters / MetersPerDegreeLat;

    private sealed record Welt(GpsTrackService Dienst, IApplicationDbContext Db, Guid Nutzer, Guid Hund);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var hund = new Dog { Name = "Rex" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return new Welt(new GpsTrackService(db, new FakeWeatherEnrichmentService()), db, nutzer, hund.Id);
    }

    private static CreateGpsTrackRequest Legung(Guid hund, GpsTrackKind art = GpsTrackKind.Faehrte, string? boden = null)
    {
        var punkte = Enumerable.Range(0, 5)
            .Select(i => new CreateGpsPointRequest(LatAt(i * 20), BaseLon, T0.AddMinutes(i), 5))
            .ToList();
        return new CreateGpsTrackRequest(null, 80, null, null, null, null, null, punkte,
            DogId: hund, Date: new DateOnly(2026, 1, 1), DurationMinutes: 30, Kind: art, GroundCondition: boden);
    }

    private static CreateGpsWalkRunRequest Lauf() =>
        new(80, null, Enumerable.Range(0, 5).Select(i => new CreateGpsWalkPointRequest(LatAt(i * 20), BaseLon, T0.AddHours(2).AddMinutes(i), 5)).ToList());

    // ---- Bodenzustand ----

    [Fact]
    public async Task Bodenzustand_WirdBeimLegenGespeichertUndGelesen()
    {
        var w = await AufbauenAsync();

        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, boden: " feucht, Schnee "))).Value!;

        Assert.Equal("feucht, Schnee", spur.GroundCondition);
        var gelesen = (await w.Dienst.GetByTrainingSessionAsync(w.Nutzer, spur.TrainingSessionId)).Value!.Single();
        Assert.Equal("feucht, Schnee", gelesen.GroundCondition);
    }

    [Fact]
    public async Task Bodenzustand_OhneAngabe_BleibtLeer()
    {
        var w = await AufbauenAsync();

        Assert.Null((await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!.GroundCondition);
        Assert.Null((await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, boden: "  "))).Value!.GroundCondition);
    }

    [Fact]
    public async Task Bodenzustand_HundertZeichenGehen_EinhunderteinsWerdenAbgelehnt()
    {
        var w = await AufbauenAsync();

        Assert.True((await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, boden: new string('a', 100)))).Succeeded);

        var zuLang = await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, boden: new string('a', 101)));
        Assert.False(zuLang.Succeeded);
        Assert.Single(w.Db.GpsTracks);
    }

    [Fact]
    public async Task Bodenzustand_BeimTrail_WirdNichtGespeichert()
    {
        var w = await AufbauenAsync();

        var trail = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, GpsTrackKind.Mantrailing, "nass"))).Value!;

        Assert.Null(trail.GroundCondition);
    }

    // ---- Verdeckt absuchen ----

    [Fact]
    public async Task Verdeckt_SetzenUndZuruecknehmen_SolangeKeinAblaufExistiert()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!;
        Assert.False(spur.DoubleBlind);

        var verdeckt = await w.Dienst.UpdateVerdecktAsync(w.Nutzer, spur.Id, new UpdateGpsVerdecktRequest(true));
        Assert.True(verdeckt.Succeeded);
        Assert.True(verdeckt.Value!.DoubleBlind);
        // Die Fährte selbst bleibt unverändert.
        Assert.Equal(spur.Points.Count, verdeckt.Value.Points.Count);

        var offen = await w.Dienst.UpdateVerdecktAsync(w.Nutzer, spur.Id, new UpdateGpsVerdecktRequest(false));
        Assert.False(offen.Value!.DoubleBlind);
    }

    [Fact]
    public async Task Verdeckt_NachEinemAblauf_WirdAbgelehnt()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!;
        await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, Lauf());

        var ergebnis = await w.Dienst.UpdateVerdecktAsync(w.Nutzer, spur.Id, new UpdateGpsVerdecktRequest(true));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.False(w.Db.GpsTracks.Single().DoubleBlind);
    }

    [Fact]
    public async Task Verdeckt_Rechte_NurWerBearbeitenDarf()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!;
        var trainerin = Guid.NewGuid();
        w.Db.TrainerAssignments.Add(new TrainerAssignment { DogId = w.Hund, TrainerId = trainerin, MemberId = w.Nutzer, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) });
        await w.Db.SaveChangesAsync();

        // Fremde und betreuende Trainer:innen sehen die Fährte höchstens - ändern dürfen sie nichts.
        Assert.True((await w.Dienst.UpdateVerdecktAsync(Guid.NewGuid(), spur.Id, new UpdateGpsVerdecktRequest(true))).IsNotFound);
        Assert.True((await w.Dienst.UpdateVerdecktAsync(trainerin, spur.Id, new UpdateGpsVerdecktRequest(true))).IsNotFound);
        Assert.True((await w.Dienst.UpdateVerdecktAsync(w.Nutzer, Guid.NewGuid(), new UpdateGpsVerdecktRequest(true))).IsNotFound);
        Assert.False(w.Db.GpsTracks.Single().DoubleBlind);
    }

    [Fact]
    public async Task Verdeckt_BeimTrail_GehtUeberDenTrailWeg()
    {
        var w = await AufbauenAsync();
        var trail = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, GpsTrackKind.Mantrailing))).Value!;

        var ergebnis = await w.Dienst.UpdateVerdecktAsync(w.Nutzer, trail.Id, new UpdateGpsVerdecktRequest(true));

        Assert.False(ergebnis.Succeeded);
        Assert.False(w.Db.GpsTracks.Single().DoubleBlind);
    }

    [Fact]
    public async Task Verdeckt_BeimAnlegenDerFaehrte_BleibtAbgelehnt()
    {
        var w = await AufbauenAsync();
        var anfrage = Legung(w.Hund) with { DoubleBlind = true };

        // Eine Fährte wird erst gelegt und dann verdeckt eingestellt - nicht beim Legen mitgeschickt.
        var ergebnis = await w.Dienst.CreateAsync(w.Nutzer, anfrage);

        Assert.False(ergebnis.Succeeded);
        Assert.Equal("Verdeckt absuchen stellst du nach dem Legen in den Fährtendetails ein.", ergebnis.Errors.Single());
    }
}
