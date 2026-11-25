using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using static Dogity.Application.Tests.Tracking.FaehrtenTestHilfe;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Paket SF1, Schweißfährte als dritte Spurart: Fristen je Art, Auswertungsgrenzen (10/30 m), Haken
/// (Winkel) wie bei der Fährte, Halt am Wundbett oder Verweiserpunkt als Verweisen und die Angaben
/// (Verfahren, Wildart, Schweißmenge, Stehzeit), die nur an einer Schweißfährte angenommen werden.
/// </summary>
public class SchweissfaehrteTests
{
    // ---- Fristen ----

    [Theory]
    [InlineData(GpsTrackKind.Faehrte, 4)]
    [InlineData(GpsTrackKind.Mantrailing, 48)]
    [InlineData(GpsTrackKind.Schweissfaehrte, 72)]
    public void Fristen_JeArt(GpsTrackKind art, int stunden) =>
        Assert.Equal(TimeSpan.FromHours(stunden), Ablauffristen.Fuer(art));

    [Theory]
    [InlineData(GpsTrackKind.Faehrte, 4 * 60, true)]
    [InlineData(GpsTrackKind.Faehrte, 4 * 60 + 1, false)]
    [InlineData(GpsTrackKind.Mantrailing, 48 * 60, true)]
    [InlineData(GpsTrackKind.Mantrailing, 48 * 60 + 1, false)]
    [InlineData(GpsTrackKind.Schweissfaehrte, 72 * 60, true)]
    [InlineData(GpsTrackKind.Schweissfaehrte, 72 * 60 + 1, false)]
    public void NochZuSuchen_GrenzfallAufDieMinute(GpsTrackKind art, int minuten, bool erwartet) =>
        Assert.Equal(erwartet, Ablauffristen.NochZuSuchen(T0, art, T0.AddMinutes(minuten)));

    // ---- Auswertung ----

    private static readonly (double X, double Y)[] Weg = [(0, 0), (0, 100), (60, 100), (60, 180)];

    private static WalkRunEvaluation Auswerten(Lauf lauf, GpsTrackKind art, IEnumerable<GpsPoint>? marker = null) =>
        GpsTrackEvaluator.TryEvaluate(Legung(Weg).Concat(marker ?? []).ToList(), lauf.Punkte, art)!.Value;

    [Fact]
    public void Grenzen_SchweissfaehrteIst10Und30()
    {
        Assert.Equal(new SpurToleranz(10, 10, 30), GpsTrackEvaluator.ToleranzFuer(GpsTrackKind.Schweissfaehrte));
        Assert.Equal(new SpurToleranz(3, 3, 6), GpsTrackEvaluator.ToleranzFuer(GpsTrackKind.Faehrte));
    }

    [Fact]
    public void ZehnMeterNeben_IstBeiDerSchweissfaehrteAufDerSpur_BeiDerFaehrteNicht()
    {
        var lauf = new Lauf(8, 0).Gehe(8, 100).Gehe(68, 100).Gehe(68, 180);

        Assert.Equal(100, Auswerten(lauf, GpsTrackKind.Schweissfaehrte).OnTrackPercent, 0);
        Assert.True(Auswerten(lauf, GpsTrackKind.Faehrte).OnTrackPercent < 50);
    }

    [Fact]
    public void Haken_WerdenAuchBeiDerSchweissfaehrteAusgewertet_BeimTrailNicht()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var schweiss = Auswerten(lauf, GpsTrackKind.Schweissfaehrte);
        var trail = Auswerten(lauf, GpsTrackKind.Mantrailing);

        Assert.Equal(2, schweiss.Corners.Count);
        Assert.Equal(3, schweiss.Legs.Count);
        Assert.Empty(trail.Corners);
    }

    [Theory]
    [InlineData(GpsMarkerType.WoundBed)]
    [InlineData(GpsMarkerType.IndicatorPoint)]
    [InlineData(GpsMarkerType.Article)]
    public void HaltAmWundbettOderVerweiserpunkt_IstVerweisen(GpsMarkerType art)
    {
        var lauf = new Lauf(0, 0).Gehe(0, 37).Halt(20).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var ergebnis = Auswerten(lauf, GpsTrackKind.Schweissfaehrte, [Marker(0, 40, art)]);

        Assert.Equal(WalkStopKind.Indication, Assert.Single(ergebnis.Stops).Kind);
    }

    [Fact]
    public void HaltAmLeckerlipot_BleibtErklaert_NichtVerweisen()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 37).Halt(20).Gehe(0, 100).Gehe(60, 100).Gehe(60, 180);

        var ergebnis = Auswerten(lauf, GpsTrackKind.Schweissfaehrte, [Marker(0, 40, GpsMarkerType.TreatPot)]);

        Assert.Equal(WalkStopKind.Explained, Assert.Single(ergebnis.Stops).Kind);
    }

    [Fact]
    public void Betten_ZaehlenSeitSf2AlsZiele_GegenstaendeNicht()
    {
        var lauf = new Lauf(0, 0).Gehe(0, 60);

        var ergebnis = Auswerten(lauf, GpsTrackKind.Schweissfaehrte,
            [Marker(0, 40, GpsMarkerType.WoundBed), Marker(0, 80, GpsMarkerType.IndicatorPoint), Marker(0, 50, GpsMarkerType.Article)]);

        Assert.Equal(2, ergebnis.ArticlesTotal);
        Assert.Equal(1, ergebnis.ArticlesFound); // das Bett bei 80 m liegt mehr als 8 m vom letzten Punkt (60 m)
    }

    // ---- Angaben im Dienst ----

    private static async Task<(GpsTrackService Dienst, IApplicationDbContext Db, Guid User, Guid Hund)> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var user = Guid.NewGuid();
        var hund = new Dog { Name = "Rex" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = user, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return (new GpsTrackService(db, new FakeWeatherEnrichmentService()), db, user, hund.Id);
    }

    private static CreateGpsTrackRequest Legen(Guid hund, GpsTrackKind art = GpsTrackKind.Schweissfaehrte,
        string? verfahren = null, string? wildart = null, int? ml = null, int? stehzeit = null,
        GpsMarkerType marker = GpsMarkerType.WoundBed) =>
        new(null, 400, null, null, null, null, null,
            [
                new CreateGpsPointRequest(Lat(0), Lon(0), T0, 5),
                new CreateGpsPointRequest(Lat(100), Lon(0), T0.AddMinutes(5), 5),
                new CreateGpsPointRequest(Lat(50), Lon(0), T0.AddMinutes(3), 5, PointType: GpsPointType.Manual, Label: "Bett", MarkerType: marker)
            ],
            DogId: hund, Date: new DateOnly(2026, 1, 1), DurationMinutes: 30, Kind: art,
            Verfahren: verfahren, Wildart: wildart, SchweissMl: ml, StehzeitStunden: stehzeit);

    [Fact]
    public async Task Anlegen_SpeichertArtAngabenUndMarkerart()
    {
        var (dienst, db, user, hund) = await AufbauenAsync();

        var spur = (await dienst.CreateAsync(user, Legen(hund, verfahren: " Tupf ", wildart: "Rotwild", ml: 250, stehzeit: 20))).Value!;

        Assert.Equal(GpsTrackKind.Schweissfaehrte, spur.Kind);
        Assert.Equal("Tupf", spur.Verfahren);
        Assert.Equal("Rotwild", spur.Wildart);
        Assert.Equal(250, spur.SchweissMl);
        Assert.Equal(20, spur.StehzeitStunden);
        var gelesen = (await dienst.GetByTrainingSessionAsync(user, spur.TrainingSessionId)).Value!.Single();
        Assert.Equal(20, gelesen.StehzeitStunden);
        Assert.Contains(gelesen.Points, p => p.MarkerType == GpsMarkerType.WoundBed);
    }

    [Fact]
    public async Task Anlegen_AlleAngabenSindFreiwillig()
    {
        var (dienst, db, user, hund) = await AufbauenAsync();

        var spur = (await dienst.CreateAsync(user, Legen(hund))).Value!;

        Assert.Null(spur.Verfahren);
        Assert.Null(spur.Wildart);
        Assert.Null(spur.SchweissMl);
        Assert.Null(spur.StehzeitStunden);
    }

    [Theory]
    [InlineData(GpsTrackKind.Faehrte)]
    [InlineData(GpsTrackKind.Mantrailing)]
    public async Task Anlegen_AngabenAnEinerAnderenArt_WerdenAbgelehnt(GpsTrackKind art)
    {
        var (dienst, db, user, hund) = await AufbauenAsync();

        Assert.False((await dienst.CreateAsync(user, Legen(hund, art, verfahren: "Tupf"))).Succeeded);
        Assert.False((await dienst.CreateAsync(user, Legen(hund, art, wildart: "Rotwild"))).Succeeded);
        Assert.False((await dienst.CreateAsync(user, Legen(hund, art, ml: 100))).Succeeded);
        Assert.False((await dienst.CreateAsync(user, Legen(hund, art, stehzeit: 20))).Succeeded);
    }

    [Theory]
    [InlineData("Tropfen", null, null, null)]
    [InlineData(null, "Wolf", null, null)]
    [InlineData(null, null, -1, null)]
    [InlineData(null, null, 1001, null)]
    [InlineData(null, null, null, 0)]
    [InlineData(null, null, null, 73)]
    public async Task Anlegen_UngueltigeAngaben_WerdenAbgelehntUndLegenNichtsAn(string? verfahren, string? wildart, int? ml, int? stehzeit)
    {
        var (dienst, db, user, hund) = await AufbauenAsync();

        var ergebnis = await dienst.CreateAsync(user, Legen(hund, verfahren: verfahren, wildart: wildart, ml: ml, stehzeit: stehzeit));

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(db.GpsTracks);
        Assert.Empty(db.TrainingSessions);
    }

    [Fact]
    public async Task Anlegen_GrenzwerteSindErlaubt_UndAlleListenbegriffe()
    {
        var (dienst, db, user, hund) = await AufbauenAsync();

        Assert.True((await dienst.CreateAsync(user, Legen(hund, ml: 0, stehzeit: 1))).Succeeded);
        Assert.True((await dienst.CreateAsync(user, Legen(hund, ml: 1000, stehzeit: 72))).Succeeded);
        foreach (var verfahren in Schweissangaben.Verfahren)
            Assert.True((await dienst.CreateAsync(user, Legen(hund, verfahren: verfahren))).Succeeded);
        foreach (var wildart in Schweissangaben.Wildarten)
            Assert.True((await dienst.CreateAsync(user, Legen(hund, wildart: wildart))).Succeeded);
    }

    [Fact]
    public async Task Anlegen_SchweissfaehrteOhneTrailAngaben_MitVerdecktErlaubt()
    {
        var (dienst, db, user, hund) = await AufbauenAsync();

        Assert.True((await dienst.CreateAsync(user, Legen(hund) with { DoubleBlind = true })).Value!.DoubleBlind);
        Assert.False((await dienst.CreateAsync(user, Legen(hund) with { NegativeTrail = true })).Succeeded);
        Assert.False((await dienst.CreateAsync(user, Legen(hund) with { ScentArticle = "Mütze" })).Succeeded);
    }

    [Fact]
    public async Task Ablauf_NachVierzigStunden_WirdBeiDerSchweissfaehrteAngenommen_BeiDerFaehrteNicht()
    {
        var (dienst, db, user, hund) = await AufbauenAsync();
        var schweiss = (await dienst.CreateAsync(user, Legen(hund))).Value!;
        var faehrte = (await dienst.CreateAsync(user, Legen(hund, GpsTrackKind.Faehrte, marker: GpsMarkerType.Other))).Value!;
        var beginn = T0.AddHours(40);
        CreateGpsWalkRunRequest Ablauf() =>
            new(null, null, [new CreateGpsWalkPointRequest(Lat(0), Lon(0), beginn, 5), new CreateGpsWalkPointRequest(Lat(100), Lon(0), beginn.AddMinutes(5), 5)]);

        Assert.True((await dienst.AddWalkRunAsync(user, schweiss.Id, Ablauf())).Succeeded);
        Assert.False((await dienst.AddWalkRunAsync(user, faehrte.Id, Ablauf())).Succeeded);
    }
}
