using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Paket M1, Mantrailing im GPS-Teil: Die Auswertung kennt je Art der Aufzeichnung eigene Grenzen
/// (Fährte 3/6 m, Mantrailing 10/25 m), und die Mantrailing-Angaben (Trailalter, Doppelblind,
/// Negativ-Trail, Geruchsartikel am Trail; Person gefunden und Anzeigeverhalten am Lauf) werden
/// geprüft, gespeichert und nur an einer Mantrailing-Spur angenommen.
/// </summary>
public class MantrailingTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private const double BaseLat = 50.0;
    private const double BaseLon = 8.0;
    private const double MetersPerDegreeLat = 111320.0;

    private static double LatAt(double meters) => BaseLat + meters / MetersPerDegreeLat;
    private static double LonOffset(double meters) =>
        BaseLon + meters / (MetersPerDegreeLat * Math.Cos(BaseLat * Math.PI / 180));

    private static List<GpsPoint> Spur() =>
        Enumerable.Range(0, 101)
            .Select(m => new GpsPoint { Latitude = LatAt(m), Longitude = BaseLon, Timestamp = T0.AddSeconds(m), PointType = GpsPointType.Automatic })
            .ToList();

    /// <summary>Ein Lauf, der überall dieselbe Strecke neben der Spur bleibt.</summary>
    private static List<GpsWalkPoint> LaufNeben(double abstandMeter) =>
        Enumerable.Range(0, 101)
            .Select(m => new GpsWalkPoint { Latitude = LatAt(m), Longitude = LonOffset(abstandMeter), Timestamp = T0.AddSeconds(m) })
            .ToList();

    // ---- Toleranz je Art ----

    [Fact]
    public void GleicheStrecke_8mNeben_IstBeiDerFaehrteRotUndBeimMantrailingGruenUndAufDerSpur()
    {
        var faehrte = GpsTrackEvaluator.TryEvaluate(Spur(), LaufNeben(8), GpsTrackKind.Faehrte)!.Value;
        var trail = GpsTrackEvaluator.TryEvaluate(Spur(), LaufNeben(8), GpsTrackKind.Mantrailing)!.Value;

        var fGrenzen = GpsTrackEvaluator.ToleranzFuer(GpsTrackKind.Faehrte);
        var mGrenzen = GpsTrackEvaluator.ToleranzFuer(GpsTrackKind.Mantrailing);

        Assert.True(faehrte.AvgDeviationMeters > fGrenzen.AmberMaxMeters, "Fährte: rot");
        Assert.Equal(0, faehrte.OnTrackPercent, 1);
        Assert.True(trail.AvgDeviationMeters <= mGrenzen.GreenMaxMeters, "Mantrailing: grün");
        Assert.Equal(100, trail.OnTrackPercent, 1);
    }

    [Fact]
    public void GleicheStrecke_18mNeben_IstBeimMantrailingGelbUndNichtMehrAufDerSpur()
    {
        var trail = GpsTrackEvaluator.TryEvaluate(Spur(), LaufNeben(18), GpsTrackKind.Mantrailing)!.Value;
        var grenzen = GpsTrackEvaluator.ToleranzFuer(GpsTrackKind.Mantrailing);

        Assert.True(trail.AvgDeviationMeters > grenzen.GreenMaxMeters && trail.AvgDeviationMeters <= grenzen.AmberMaxMeters, "gelb");
        Assert.Equal(0, trail.OnTrackPercent, 1);
    }

    [Fact]
    public void OhneArt_GiltDieFaehrte()
    {
        var ohne = GpsTrackEvaluator.TryEvaluate(Spur(), LaufNeben(5))!.Value;
        var faehrte = GpsTrackEvaluator.TryEvaluate(Spur(), LaufNeben(5), GpsTrackKind.Faehrte)!.Value;

        Assert.Equal(faehrte.OnTrackPercent, ohne.OnTrackPercent);
    }

    [Fact]
    public void Grenzen_FaehrteBleibtBei3Und6_MantrailingIst10Und25()
    {
        Assert.Equal(new SpurToleranz(3, 3, 6), GpsTrackEvaluator.ToleranzFuer(GpsTrackKind.Faehrte));
        Assert.Equal(new SpurToleranz(10, 10, 25), GpsTrackEvaluator.ToleranzFuer(GpsTrackKind.Mantrailing));
    }

    [Fact]
    public void Mantrailing_ZaehltKeineGegenstaende()
    {
        var gelegt = Spur();
        gelegt.Add(new GpsPoint
        {
            Latitude = LatAt(50), Longitude = BaseLon, Timestamp = T0.AddSeconds(50),
            PointType = GpsPointType.Manual, MarkerType = GpsMarkerType.Article
        });

        var faehrte = GpsTrackEvaluator.TryEvaluate(gelegt, LaufNeben(0), GpsTrackKind.Faehrte)!.Value;
        var trail = GpsTrackEvaluator.TryEvaluate(gelegt, LaufNeben(0), GpsTrackKind.Mantrailing)!.Value;

        Assert.Equal(1, faehrte.ArticlesTotal);
        Assert.Equal(0, trail.ArticlesTotal);
        Assert.Equal(0, trail.ArticlesFound);
    }

    // ---- Service ----

    private static async Task<(GpsTrackService Service, IApplicationDbContext Db, Guid UserId, Guid DogId)> MakeAsync()
    {
        var db = InMemoryDbContext.Create();
        var userId = Guid.NewGuid();
        var dog = new Dog { Name = "Rex" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = userId, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return (new GpsTrackService(db, new FakeWeatherEnrichmentService()), db, userId, dog.Id);
    }

    private static CreateGpsTrackRequest Trail(Guid dogId, GpsTrackKind kind = GpsTrackKind.Mantrailing, bool doppelblind = false,
        bool negativ = false, string? artikel = null) =>
        new(null, 300, null, null, null, null, null,
            Enumerable.Range(0, 5).Select(i => new CreateGpsPointRequest(LatAt(i * 60), BaseLon, T0.AddMinutes(i), 5)).ToList(),
            DogId: dogId, Date: new DateOnly(2026, 1, 1), DurationMinutes: 40,
            Kind: kind, DoubleBlind: doppelblind, NegativeTrail: negativ, ScentArticle: artikel);

    [Fact]
    public async Task Anlegen_SpeichertArtUndAngabenUndLiefertSieZurueck()
    {
        var (service, _, userId, dogId) = await MakeAsync();

        var ergebnis = await service.CreateAsync(userId, Trail(dogId, doppelblind: true, negativ: true, artikel: "  Mütze  "));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(GpsTrackKind.Mantrailing, ergebnis.Value!.Kind);
        Assert.True(ergebnis.Value.DoubleBlind);
        Assert.True(ergebnis.Value.NegativeTrail);
        Assert.Equal("Mütze", ergebnis.Value.ScentArticle);
    }

    [Fact]
    public async Task Anlegen_OhneAngaben_IstEineFaehrteMitLeerenAngaben()
    {
        var (service, _, userId, dogId) = await MakeAsync();

        var ergebnis = await service.CreateAsync(userId, Trail(dogId, GpsTrackKind.Faehrte));

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(GpsTrackKind.Faehrte, ergebnis.Value!.Kind);
        Assert.False(ergebnis.Value.DoubleBlind);
        Assert.False(ergebnis.Value.NegativeTrail);
        Assert.Null(ergebnis.Value.ScentArticle);
    }

    [Theory]
    [InlineData(true, false, null)]
    [InlineData(false, true, null)]
    [InlineData(false, false, "Mütze")]
    public async Task Anlegen_EineFaehrteMitMantrailingAngaben_WirdAbgelehntUndLegtNichtsAn(bool doppelblind, bool negativ, string? artikel)
    {
        var (service, db, userId, dogId) = await MakeAsync();

        var ergebnis = await service.CreateAsync(userId, Trail(dogId, GpsTrackKind.Faehrte, doppelblind, negativ, artikel));

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(db.GpsTracks);
        Assert.Empty(db.TrainingSessions);
    }

    [Fact]
    public async Task Anlegen_TrailalterAusserhalbDerGrenzen_WirdAbgelehntUndLegtNichtsAn()
    {
        var (service, db, userId, dogId) = await MakeAsync();

        foreach (var minuten in new[] { -1, GpsTrackService.MaxTrailAlterMinuten + 1 })
            Assert.False((await service.CreateAsync(userId, Trail(dogId) with { AgeMinutes = minuten })).Succeeded);

        Assert.Empty(db.GpsTracks);
        Assert.Empty(db.TrainingSessions);
        Assert.True((await service.CreateAsync(userId, Trail(dogId) with { AgeMinutes = GpsTrackService.MaxTrailAlterMinuten })).Succeeded);
    }

    [Fact]
    public async Task Anlegen_UnbekannteArt_WirdAbgelehnt()
    {
        var (service, _, userId, dogId) = await MakeAsync();

        var ergebnis = await service.CreateAsync(userId, Trail(dogId, (GpsTrackKind)7));

        Assert.False(ergebnis.Succeeded);
    }

    [Fact]
    public async Task Anlegen_GeruchsartikelZuLang_WirdAbgelehnt()
    {
        var (service, _, userId, dogId) = await MakeAsync();

        var genau = await service.CreateAsync(userId, Trail(dogId, artikel: new string('a', Textlaengen.Geruchsartikel)));
        var zuLang = await service.CreateAsync(userId, Trail(dogId, artikel: new string('a', Textlaengen.Geruchsartikel + 1)));

        Assert.True(genau.Succeeded);
        Assert.False(zuLang.Succeeded);
        Assert.Contains("Geruchsartikel", zuLang.Errors.Single());
    }

    [Fact]
    public async Task AblaufEinerMantrailingSpur_WirdMitDenWeitenGrenzenAusgewertet()
    {
        var (service, _, userId, dogId) = await MakeAsync();
        var trail = (await service.CreateAsync(userId, Trail(dogId))).Value!;
        var faehrte = (await service.CreateAsync(userId, Trail(dogId, GpsTrackKind.Faehrte))).Value!;

        // Dieselbe Strecke, 8 m neben der Spur (4 Minuten, ein Punkt je Minute).
        var lauf = new CreateGpsWalkRunRequest(240, null,
            Enumerable.Range(0, 5).Select(i => new CreateGpsWalkPointRequest(LatAt(i * 60), LonOffset(8), T0.AddHours(2).AddMinutes(i), 5)).ToList());

        var imTrail = await service.AddWalkRunAsync(userId, trail.Id, lauf);
        var beiDerFaehrte = await service.AddWalkRunAsync(userId, faehrte.Id, lauf);

        Assert.Equal(100, imTrail.Value!.OnTrackPercent!.Value, 1);
        Assert.Equal(0, beiDerFaehrte.Value!.OnTrackPercent!.Value, 1);
    }

    // ---- Trail-Angaben ändern ----

    [Fact]
    public async Task UpdateTrail_SetztAlterInMinutenUndAngaben_UndLoeschtLeereZurueck()
    {
        var (service, _, userId, dogId) = await MakeAsync();
        var trail = (await service.CreateAsync(userId, Trail(dogId))).Value!;

        var gesetzt = await service.UpdateTrailAsync(userId, trail.Id, new UpdateGpsTrailRequest(90, true, false, " Pulli "));
        Assert.True(gesetzt.Succeeded);
        Assert.Equal(90, gesetzt.Value!.AgeMinutes);
        Assert.True(gesetzt.Value.DoubleBlind);
        Assert.Equal("Pulli", gesetzt.Value.ScentArticle);

        var geleert = await service.UpdateTrailAsync(userId, trail.Id, new UpdateGpsTrailRequest(null, false, false, "  "));
        Assert.Null(geleert.Value!.AgeMinutes);
        Assert.Null(geleert.Value.ScentArticle);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(GpsTrackService.MaxTrailAlterMinuten + 1)]
    public async Task UpdateTrail_AlterAusserhalbDerGrenzen_WirdAbgelehnt(int minuten)
    {
        var (service, _, userId, dogId) = await MakeAsync();
        var trail = (await service.CreateAsync(userId, Trail(dogId))).Value!;

        Assert.False((await service.UpdateTrailAsync(userId, trail.Id, new UpdateGpsTrailRequest(minuten, false, false, null))).Succeeded);
    }

    [Fact]
    public async Task UpdateTrail_AnEinerFaehrteOderVonFremden_WirdAbgelehnt()
    {
        var (service, _, userId, dogId) = await MakeAsync();
        var trail = (await service.CreateAsync(userId, Trail(dogId))).Value!;
        var faehrte = (await service.CreateAsync(userId, Trail(dogId, GpsTrackKind.Faehrte))).Value!;

        Assert.False((await service.UpdateTrailAsync(userId, faehrte.Id, new UpdateGpsTrailRequest(60, false, false, null))).Succeeded);
        Assert.False((await service.UpdateTrailAsync(Guid.NewGuid(), trail.Id, new UpdateGpsTrailRequest(60, false, false, null))).Succeeded);
        Assert.False((await service.UpdateTrailAsync(userId, trail.Id, new UpdateGpsTrailRequest(60, false, false, new string('x', Textlaengen.Geruchsartikel + 1)))).Succeeded);
    }

    // ---- Ergebnis eines Laufs ----

    private static async Task<(GpsTrackService Service, Guid UserId, Guid TrackId, Guid RunId)> TrailMitLaufAsync(bool negativ = false)
    {
        var (service, _, userId, dogId) = await MakeAsync();
        var trail = (await service.CreateAsync(userId, Trail(dogId, negativ: negativ))).Value!;
        var lauf = await service.AddWalkRunAsync(userId, trail.Id,
            new CreateGpsWalkRunRequest(100, null, [new CreateGpsWalkPointRequest(LatAt(0), BaseLon, T0.AddHours(2), 5)]));
        return (service, userId, trail.Id, lauf.Value!.Id);
    }

    [Fact]
    public async Task UpdateTrailRun_TraegtFundUndAnzeigeEin()
    {
        var (service, userId, trackId, runId) = await TrailMitLaufAsync();

        var ergebnis = await service.UpdateTrailRunAsync(userId, trackId, runId, new UpdateGpsTrailRunRequest(true, TrailIndication.SittingInFront));

        Assert.True(ergebnis.Succeeded);
        Assert.True(ergebnis.Value!.PersonFound);
        Assert.Equal(TrailIndication.SittingInFront, ergebnis.Value.Indication);
    }

    [Fact]
    public async Task UpdateTrailRun_OhneFundBleibtKeineAnzeigeStehen()
    {
        var (service, userId, trackId, runId) = await TrailMitLaufAsync();
        await service.UpdateTrailRunAsync(userId, trackId, runId, new UpdateGpsTrailRunRequest(true, TrailIndication.Barking));

        var korrigiert = await service.UpdateTrailRunAsync(userId, trackId, runId, new UpdateGpsTrailRunRequest(false, TrailIndication.Barking));

        Assert.False(korrigiert.Value!.PersonFound);
        Assert.Null(korrigiert.Value.Indication);
    }

    [Fact]
    public async Task UpdateTrailRun_BeimNegativTrail_GibtEsKeinAnzeigeverhalten()
    {
        var (service, userId, trackId, runId) = await TrailMitLaufAsync(negativ: true);

        var ergebnis = await service.UpdateTrailRunAsync(userId, trackId, runId, new UpdateGpsTrailRunRequest(true, TrailIndication.Barking));

        Assert.True(ergebnis.Value!.PersonFound);
        Assert.Null(ergebnis.Value.Indication);
    }

    [Fact]
    public async Task UpdateTrailRun_UnbekanntesAnzeigeverhaltenFremdeOderFaehrte_WirdAbgelehnt()
    {
        var (service, userId, trackId, runId) = await TrailMitLaufAsync();

        Assert.False((await service.UpdateTrailRunAsync(userId, trackId, runId, new UpdateGpsTrailRunRequest(true, (TrailIndication)42))).Succeeded);
        Assert.False((await service.UpdateTrailRunAsync(Guid.NewGuid(), trackId, runId, new UpdateGpsTrailRunRequest(true, null))).Succeeded);
        Assert.False((await service.UpdateTrailRunAsync(userId, trackId, Guid.NewGuid(), new UpdateGpsTrailRunRequest(true, null))).Succeeded);

        var (service2, _, userId2, dogId2) = await MakeAsync();
        var faehrte = (await service2.CreateAsync(userId2, Trail(dogId2, GpsTrackKind.Faehrte))).Value!;
        var lauf = (await service2.AddWalkRunAsync(userId2, faehrte.Id,
            new CreateGpsWalkRunRequest(10, null, [new CreateGpsWalkPointRequest(LatAt(0), BaseLon, T0.AddHours(2), 5)]))).Value!;
        Assert.False((await service2.UpdateTrailRunAsync(userId2, faehrte.Id, lauf.Id, new UpdateGpsTrailRunRequest(true, null))).Succeeded);
    }

    [Fact]
    public async Task BetreuendeTrainerin_TraegtDasErgebnisEinesLaufsEin_AendertAberNichtDenTrailDerFamilie()
    {
        var (service, db, userId, dogId) = await MakeAsync();
        var trail = (await service.CreateAsync(userId, Trail(dogId))).Value!;
        var lauf = (await service.AddWalkRunAsync(userId, trail.Id,
            new CreateGpsWalkRunRequest(100, null, [new CreateGpsWalkPointRequest(LatAt(0), BaseLon, T0.AddHours(2), 5)]))).Value!;
        var trainerin = Guid.NewGuid();
        db.TrainerAssignments.Add(new TrainerAssignment { DogId = dogId, TrainerId = trainerin, MemberId = userId, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) });
        await db.SaveChangesAsync();

        // Das Ergebnis gehört zum Lauf, den sie sehen und aufzeichnen darf; die Angaben zum Trail
        // gehören zum Eintrag der Familie (TrainingsRechte) und bleiben deren Sache.
        Assert.True((await service.UpdateTrailRunAsync(trainerin, trail.Id, lauf.Id, new UpdateGpsTrailRunRequest(true, TrailIndication.Barking))).Succeeded);
        Assert.False((await service.UpdateTrailAsync(trainerin, trail.Id, new UpdateGpsTrailRequest(60, true, false, "Schal"))).Succeeded);
        Assert.Null((await service.GetByTrainingSessionAsync(userId, trail.TrainingSessionId)).Value!.Single().AgeMinutes);
    }

    [Fact]
    public async Task Lesen_LiefertDieAngabenMitDemTrail()
    {
        var (service, _, userId, dogId) = await MakeAsync();
        var trail = (await service.CreateAsync(userId, Trail(dogId, doppelblind: true, artikel: "Schal"))).Value!;
        await service.UpdateTrailAsync(userId, trail.Id, new UpdateGpsTrailRequest(150, true, false, "Schal"));
        var lauf = (await service.AddWalkRunAsync(userId, trail.Id,
            new CreateGpsWalkRunRequest(10, null, [new CreateGpsWalkPointRequest(LatAt(0), BaseLon, T0.AddHours(2), 5)]))).Value!;
        await service.UpdateTrailRunAsync(userId, trail.Id, lauf.Id, new UpdateGpsTrailRunRequest(true, TrailIndication.StandingInFront));

        var gelesen = (await service.GetByTrainingSessionAsync(userId, trail.TrainingSessionId)).Value!.Single();

        Assert.Equal(150, gelesen.AgeMinutes);
        Assert.Equal(GpsTrackKind.Mantrailing, gelesen.Kind);
        Assert.Equal("Schal", gelesen.ScentArticle);
        Assert.True(gelesen.WalkRuns.Single().PersonFound);
        Assert.Equal(TrailIndication.StandingInFront, gelesen.WalkRuns.Single().Indication);
    }
}
