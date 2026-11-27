using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using static Dogity.Application.Tests.Tracking.FaehrtenTestHilfe;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Paket SF2, die Schweißfährte arbeiten wie auf der Prüfung: Rückrufe (eine Regel, auch in
/// lib/rueckruf.ts), Ende der Legung, Betten als Ziele, "Zum Stück gekommen" mit Totverbellen/Totverweisen
/// und "Verdeckt absuchen" auch an der Schweißfährte.
/// </summary>
public class SchweissPruefungTests
{
    // Eine lange gerade Fährte nach Norden: Abweichung = Abstand in X.
    private static List<GpsPoint> Gelegt(params GpsPoint[] marker) => Legung((0, 0), (0, 400)).Concat(marker).ToList();

    private static int Rueckrufe(Lauf lauf) => GpsTrackEvaluator.Rueckrufe(Gelegt(), lauf.Punkte)!.Value.Stellen.Count;

    // ---- Rückrufe ----

    [Fact]
    public void Rueckrufe_GeraderWegAufDerLinie_Keiner() =>
        Assert.Equal(0, Rueckrufe(new Lauf(0, 0).Gehe(0, 300)));

    [Fact]
    public void Rueckrufe_NeunzigMeterParallelInVierzigMeterAbstand_Einer() =>
        Assert.Equal(1, Rueckrufe(new Lauf(0, 0).Gehe(0, 20).Gehe(40, 20).Gehe(40, 110)));

    [Fact]
    public void Rueckrufe_SiebzigMeterAbgekommenDannZurueck_Keiner() =>
        Assert.Equal(0, Rueckrufe(new Lauf(0, 0).Gehe(0, 20).Gehe(40, 20).Gehe(40, 40).Gehe(0, 40).Gehe(0, 200)));

    [Fact]
    public void Rueckrufe_ZweimalAbgekommenMitRueckkehr_Zwei() =>
        Assert.Equal(2, Rueckrufe(new Lauf(0, 0).Gehe(0, 20).Gehe(40, 20).Gehe(40, 110).Gehe(0, 110).Gehe(0, 130)
            .Gehe(40, 130).Gehe(40, 220)));

    [Fact]
    public void Rueckrufe_DreimalAbgekommen_Drei() =>
        Assert.Equal(3, Rueckrufe(new Lauf(0, 0).Gehe(0, 10).Gehe(40, 10).Gehe(40, 100).Gehe(0, 100)
            .Gehe(0, 110).Gehe(40, 110).Gehe(40, 200).Gehe(0, 200)
            .Gehe(0, 210).Gehe(40, 210).Gehe(40, 300)));

    [Fact]
    public void Rueckrufe_EineLangeAbweichungOhneRueckkehr_ZaehltNurEinmal() =>
        Assert.Equal(1, Rueckrufe(new Lauf(0, 0).Gehe(0, 10).Gehe(40, 10).Gehe(40, 300)));

    [Fact]
    public void Rueckrufe_VorDemAnschuss_ZaehlenNicht()
    {
        // Das Gespann sucht den Anschuss erst weit abseits (60 m daneben) und findet ihn dann.
        var lauf = new Lauf(60, 100).Gehe(60, 250).Gehe(0, 250).Gehe(0, 0).Gehe(0, 100);

        Assert.Equal(0, Rueckrufe(lauf));
    }

    [Fact]
    public void Rueckrufe_OhneGefundenenAnschuss_Keine() =>
        Assert.Equal(0, Rueckrufe(new Lauf(60, 100).Gehe(60, 300)));

    [Fact]
    public void Rueckrufe_AnschussUmZwoelfMeterVerfehlt_GespannParallel_ZaehltEinen()
    {
        var erg = GpsTrackEvaluator.Rueckrufe(Gelegt(), new Lauf(12, 0).Gehe(12, 20).Gehe(50, 20).Gehe(50, 110).Punkte)!.Value;

        Assert.Single(erg.Stellen);
        Assert.True(erg.AnschussGefunden);
    }

    [Fact]
    public void Rueckrufe_AnschussVerfehlt_GespannTrifftWeiterHintenAufDieFaehrte_ZaehltEinen() =>
        Assert.Equal(1, Rueckrufe(new Lauf(25, 0).Gehe(25, 10).Gehe(5, 60).Gehe(45, 60).Gehe(45, 160)));

    [Fact]
    public void Rueckrufe_NieNaeherAlsFuenfzehnMeter_AnschussNichtGefunden()
    {
        var erg = GpsTrackEvaluator.Rueckrufe(Gelegt(), new Lauf(60, 100).Gehe(60, 300).Punkte)!.Value;

        Assert.Empty(erg.Stellen);
        Assert.False(erg.AnschussGefunden);
    }

    [Fact]
    public void Rueckrufe_LueckenstueckZaehltNichtAlsWeg()
    {
        // Abgekommen, dann 100 m Luft ohne Aufzeichnung (Bildschirm aus): Die Luftlinie zählt nicht als Weg.
        var lauf = new Lauf(0, 0).Gehe(0, 10).Gehe(40, 10).Gehe(40, 40);
        lauf.Punkte.Add(new GpsWalkPoint { Latitude = Lat(140), Longitude = Lon(40), Timestamp = lauf.Punkte[^1].Timestamp.AddSeconds(90) });

        Assert.Equal(0, Rueckrufe(lauf));
    }

    [Fact]
    public void Rueckrufe_PositionIstDieDesAblaufpunkts()
    {
        var ergebnis = GpsTrackEvaluator.Rueckrufe(Gelegt(), new Lauf(0, 0).Gehe(0, 10).Gehe(40, 10).Gehe(40, 110).Punkte)!.Value;

        var ruf = Assert.Single(ergebnis.Stellen);
        Assert.InRange(ruf.Longitude, Lon(39), Lon(41));
        Assert.InRange(ruf.Latitude, Lat(10), Lat(110));
    }

    // ---- Ende der Legung ----

    [Theory]
    [InlineData(395, true)]
    [InlineData(380, false)]
    public void EndeErreicht_ZehnMeterAmLetztenPunkt(double bis, bool erwartet) =>
        Assert.Equal(erwartet, GpsTrackEvaluator.EndeErreicht(Gelegt(), new Lauf(0, 0).Gehe(0, bis).Punkte));

    // ---- Betten ----

    [Fact]
    public void Betten_ErreichteBetten_NichtErreichteUndGegenstaende()
    {
        var bett = Marker(0, 100, GpsMarkerType.WoundBed);
        var verweiser = Marker(0, 300, GpsMarkerType.IndicatorPoint);
        var gegenstand = Marker(0, 50, GpsMarkerType.Article);
        var gelegt = Gelegt(bett, verweiser, gegenstand);
        var lauf = new Lauf(0, 0).Gehe(0, 150);

        var erreicht = GpsTrackEvaluator.ErreichteGegenstaende(gelegt, lauf.Punkte, GpsTrackKind.Schweissfaehrte)!;

        Assert.Equal([bett.Id], erreicht);
    }

    // ---- Dienst ----

    private sealed record Welt(GpsTrackService Dienst, Guid Nutzer, Guid Hund);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var hund = new Dog { Name = "Rex" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return new Welt(new GpsTrackService(db, new FakeWeatherEnrichmentService()), nutzer, hund.Id);
    }

    private static CreateGpsTrackRequest Legen(Guid hund, GpsTrackKind art)
    {
        var linie = Legung((0, 0), (0, 400)).Select(p => new CreateGpsPointRequest(p.Latitude, p.Longitude, p.Timestamp, 5)).ToList();
        linie.Add(new CreateGpsPointRequest(Lat(100), Lon(0), T0.AddSeconds(5), 5, GpsPointType.Manual, "Bett",
            art == GpsTrackKind.Schweissfaehrte ? GpsMarkerType.WoundBed : GpsMarkerType.Article));
        return new(null, 400, null, null, null, null, null, linie,
            DogId: hund, Date: new DateOnly(2026, 1, 1), DurationMinutes: 30, Kind: art);
    }

    private static CreateGpsWalkRunRequest Ablauf(Lauf lauf) =>
        new(null, null, lauf.Punkte.Select(p => new CreateGpsWalkPointRequest(p.Latitude, p.Longitude, p.Timestamp, 5)).ToList());

    [Fact]
    public async Task Verdeckt_SchweissfaehrteNurVorDemErstenAblauf_TrailBleibtBeiSeinemWeg()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legen(w.Hund, GpsTrackKind.Schweissfaehrte))).Value!;

        Assert.True((await w.Dienst.UpdateVerdecktAsync(w.Nutzer, spur.Id, new UpdateGpsVerdecktRequest(true))).Value!.DoubleBlind);
        Assert.True((await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, Ablauf(new Lauf(0, 0).Gehe(0, 100)))).Succeeded);
        Assert.False((await w.Dienst.UpdateVerdecktAsync(w.Nutzer, spur.Id, new UpdateGpsVerdecktRequest(false))).Succeeded);
        var trail = (await w.Dienst.CreateAsync(w.Nutzer, Legen(w.Hund, GpsTrackKind.Mantrailing))).Value!;
        Assert.False((await w.Dienst.UpdateVerdecktAsync(w.Nutzer, trail.Id, new UpdateGpsVerdecktRequest(true))).Succeeded);
    }

    [Fact]
    public async Task Ablauf_SchweissfaehrteLiefertRueckrufeEndeUndBettenHinweis_FaehrteNicht()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legen(w.Hund, GpsTrackKind.Schweissfaehrte))).Value!;
        var lauf = new Lauf(0, 0).Gehe(0, 10).Gehe(40, 10).Gehe(40, 100).Gehe(0, 100).Gehe(0, 395);

        var dto = (await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, Ablauf(lauf))).Value!;

        Assert.Equal(1, dto.Recalls);
        Assert.Single(dto.RecallPoints!);
        Assert.True(dto.EndReached);
        Assert.True(dto.AnschussFound);
        Assert.Equal(1, dto.ArticlesTotal);
        Assert.Equal(1, dto.ArticlesFound);
        Assert.Single(dto.ReachedArticleIds!);

        var faehrte = (await w.Dienst.CreateAsync(w.Nutzer, Legen(w.Hund, GpsTrackKind.Faehrte))).Value!;
        var fDto = (await w.Dienst.AddWalkRunAsync(w.Nutzer, faehrte.Id, Ablauf(new Lauf(0, 0).Gehe(0, 395)))).Value!;
        Assert.Null(fDto.Recalls);
        Assert.Null(fDto.RecallPoints);
        Assert.Null(fDto.EndReached);
        Assert.Null(fDto.AnschussFound);
    }

    [Fact]
    public async Task Betten_VerwiesenUndUeberlaufen_AufgenommenUndFremdeMarkerNicht()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legen(w.Hund, GpsTrackKind.Schweissfaehrte))).Value!;
        var lauf = (await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, Ablauf(new Lauf(0, 0).Gehe(0, 150)))).Value!;
        var bett = spur.Points.Single(p => p.MarkerType == GpsMarkerType.WoundBed && p.PointType == GpsPointType.Manual).Id;
        var strecke = spur.Points.First(p => p.PointType == GpsPointType.Automatic).Id;

        var verwiesen = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, spur.Id, lauf.Id, [new(bett, ArticleResult.Indicated)]);
        Assert.Equal(1, verwiesen.Value!.ArticlesConfirmedFound);
        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, spur.Id, lauf.Id, [new(bett, ArticleResult.Missed)])).Succeeded);
        Assert.False((await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, spur.Id, lauf.Id, [new(bett, ArticleResult.PickedUp)])).Succeeded);
        Assert.False((await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, spur.Id, lauf.Id, [new(strecke, ArticleResult.Indicated)])).Succeeded);
        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, spur.Id, lauf.Id, [new(bett, null)])).Succeeded);
        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, spur.Id, Guid.NewGuid(), [])).IsNotFound);
        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(Guid.NewGuid(), spur.Id, lauf.Id, [new(bett, ArticleResult.Indicated)])).IsNotFound);
    }

    [Fact]
    public async Task Stueck_ZumStueckGekommenMitTotverbellenOderTotverweisen_AndereVerhaltenNicht()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legen(w.Hund, GpsTrackKind.Schweissfaehrte))).Value!;
        var lauf = (await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, Ablauf(new Lauf(0, 0).Gehe(0, 395)))).Value!;

        var verbellt = (await w.Dienst.UpdateTrailRunAsync(w.Nutzer, spur.Id, lauf.Id, new UpdateGpsTrailRunRequest(true, TrailIndication.Barking))).Value!;
        Assert.True(verbellt.PersonFound);
        Assert.Equal(TrailIndication.Barking, verbellt.Indication);
        Assert.Equal(TrailIndication.ReturnIndication,
            (await w.Dienst.UpdateTrailRunAsync(w.Nutzer, spur.Id, lauf.Id, new UpdateGpsTrailRunRequest(true, TrailIndication.ReturnIndication))).Value!.Indication);
        Assert.False((await w.Dienst.UpdateTrailRunAsync(w.Nutzer, spur.Id, lauf.Id, new UpdateGpsTrailRunRequest(true, TrailIndication.SittingInFront))).Succeeded);
        // Ohne Stück bleibt kein Verhalten stehen.
        var ohne = (await w.Dienst.UpdateTrailRunAsync(w.Nutzer, spur.Id, lauf.Id, new UpdateGpsTrailRunRequest(false, TrailIndication.Barking))).Value!;
        Assert.False(ohne.PersonFound);
        Assert.Null(ohne.Indication);
        Assert.True((await w.Dienst.UpdateTrailRunAsync(w.Nutzer, spur.Id, lauf.Id, new UpdateGpsTrailRunRequest(null, null))).Succeeded);
        Assert.True((await w.Dienst.UpdateTrailRunAsync(Guid.NewGuid(), spur.Id, lauf.Id, new UpdateGpsTrailRunRequest(true, null))).IsNotFound);

        var faehrte = (await w.Dienst.CreateAsync(w.Nutzer, Legen(w.Hund, GpsTrackKind.Faehrte))).Value!;
        var fLauf = (await w.Dienst.AddWalkRunAsync(w.Nutzer, faehrte.Id, Ablauf(new Lauf(0, 0).Gehe(0, 100)))).Value!;
        Assert.False((await w.Dienst.UpdateTrailRunAsync(w.Nutzer, faehrte.Id, fLauf.Id, new UpdateGpsTrailRunRequest(true, null))).Succeeded);
    }
}
