using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Paket F3, Fährte: Material je Gegenstand (am Marker, nach dem Legen eingetragen) und das Ergebnis je
/// Gegenstand im Ablauf (verwiesen, aufgenommen, überlaufen). Beides sind Eingaben der Hundeführer:in; die
/// GPS-Zahl <c>ArticlesFound</c> bleibt davon unberührt.
/// </summary>
public class GegenstaendeTests
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

    /// <summary>Eine Spur mit drei Gegenständen (bei 20, 40 und 60 m) und einem Leckerlipot, gelegt in dieser Reihenfolge.</summary>
    private static CreateGpsTrackRequest Legung(Guid hund, GpsTrackKind art = GpsTrackKind.Faehrte)
    {
        var punkte = Enumerable.Range(0, 5)
            .Select(i => new CreateGpsPointRequest(LatAt(i * 20), BaseLon, T0.AddMinutes(i), 5))
            .ToList();
        punkte.Add(new CreateGpsPointRequest(LatAt(20), BaseLon, T0.AddSeconds(30), 5, GpsPointType.Manual, null, GpsMarkerType.Article));
        punkte.Add(new CreateGpsPointRequest(LatAt(30), BaseLon, T0.AddSeconds(90), 5, GpsPointType.Manual, null, GpsMarkerType.TreatPot));
        punkte.Add(new CreateGpsPointRequest(LatAt(40), BaseLon, T0.AddSeconds(150), 5, GpsPointType.Manual, null, GpsMarkerType.Article));
        punkte.Add(new CreateGpsPointRequest(LatAt(60), BaseLon, T0.AddSeconds(210), 5, GpsPointType.Manual, null, GpsMarkerType.Article));
        return new CreateGpsTrackRequest(null, 80, null, null, null, null, null, punkte,
            DogId: hund, Date: new DateOnly(2026, 1, 1), DurationMinutes: 30, Kind: art);
    }

    private sealed record Faehrte(GpsTrackDto Spur, GpsWalkRunDto Lauf)
    {
        /// <summary>Die Gegenstände in Legereihenfolge (Gegenstand 1, 2, 3).</summary>
        internal IReadOnlyList<GpsPointDto> Gegenstaende =>
            Spur.Points.Where(p => p.PointType == GpsPointType.Manual && p.MarkerType == GpsMarkerType.Article).OrderBy(p => p.Timestamp).ToList();
        internal GpsPointDto Leckerlipot => Spur.Points.Single(p => p.MarkerType == GpsMarkerType.TreatPot);
    }

    private static async Task<Faehrte> FaehrteMitLaufAsync(Welt w, GpsTrackKind art = GpsTrackKind.Faehrte)
    {
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, art))).Value!;
        var lauf = (await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, new CreateGpsWalkRunRequest(80, null,
            Enumerable.Range(0, 5).Select(i => new CreateGpsWalkPointRequest(LatAt(i * 20), BaseLon, T0.AddHours(2).AddMinutes(i), 5)).ToList()))).Value!;
        return new Faehrte(spur, lauf);
    }

    // ---- Material ----

    [Fact]
    public async Task Material_AllePunkteHabenEineId_DasMaterialStartetLeer()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!;

        Assert.All(spur.Points, p => Assert.NotEqual(Guid.Empty, p.Id));
        Assert.All(spur.Points, p => Assert.Null(p.Material));
        Assert.False(spur.ExcludeFromStats);
    }

    [Fact]
    public async Task Material_Setzen_AendernUndEntfernen()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var gegenstand = f.Gegenstaende[0];

        var gesetzt = await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, gegenstand.Id, new UpdateGpsMarkerRequest("  Leder "));
        Assert.True(gesetzt.Succeeded);
        Assert.Equal("Leder", gesetzt.Value!.Material);

        var geaendert = await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, gegenstand.Id, new UpdateGpsMarkerRequest("Holz"));
        Assert.Equal("Holz", geaendert.Value!.Material);

        // Beim Lesen steht es am Marker, die Nachbarn bleiben leer.
        var gelesen = (await w.Dienst.GetByTrainingSessionAsync(w.Nutzer, f.Spur.TrainingSessionId)).Value!.Single();
        Assert.Equal("Holz", gelesen.Points.Single(p => p.Id == gegenstand.Id).Material);
        Assert.Single(gelesen.Points, p => p.Material != null);

        // Leer und null entfernen die Angabe (erneut tippen = abwählen).
        Assert.Null((await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, gegenstand.Id, new UpdateGpsMarkerRequest(" "))).Value!.Material);
        await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, gegenstand.Id, new UpdateGpsMarkerRequest("Stoff"));
        Assert.Null((await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, gegenstand.Id, new UpdateGpsMarkerRequest(null))).Value!.Material);
    }

    [Fact]
    public async Task Material_UnbekanntesOderNichtExaktGeschriebenes_WirdAbgelehnt()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);

        foreach (var material in new[] { "Metall", "leder", "Leder, Holz" })
        {
            var ergebnis = await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, f.Gegenstaende[0].Id, new UpdateGpsMarkerRequest(material));
            Assert.False(ergebnis.Succeeded, material);
            Assert.False(ergebnis.IsNotFound, material);
        }

        Assert.All(w.Db.GpsPoints, p => Assert.Null(p.Material));
    }

    [Fact]
    public void Material_JedeAuswahlPasstInDieSpalte()
    {
        Assert.All(GegenstandsMaterial.Alle, m => Assert.True(m.Length <= Textlaengen.GegenstandsMaterial, m));
        Assert.Equal(GegenstandsMaterial.Alle.Count, GegenstandsMaterial.Alle.Distinct().Count());
    }

    [Fact]
    public async Task Material_NurBeiEinemGegenstand_NichtBeiLeckerlipotOderAutomatischemPunkt()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var automatisch = f.Spur.Points.First(p => p.PointType == GpsPointType.Automatic);

        foreach (var punkt in new[] { f.Leckerlipot, automatisch })
        {
            var ergebnis = await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, punkt.Id, new UpdateGpsMarkerRequest("Leder"));
            Assert.False(ergebnis.Succeeded);
            Assert.False(ergebnis.IsNotFound);
        }

        Assert.All(w.Db.GpsPoints, p => Assert.Null(p.Material));
    }

    [Fact]
    public async Task Material_BeimMantrailing_GibtEsNicht()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund, GpsTrackKind.Mantrailing))).Value!;
        var marker = spur.Points.First(p => p.PointType == GpsPointType.Manual);

        var ergebnis = await w.Dienst.UpdateMarkerAsync(w.Nutzer, spur.Id, marker.Id, new UpdateGpsMarkerRequest("Leder"));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Material_FremdeFaehrteOderMarkerEinerAnderenFaehrte_IstNichtGefunden()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var andere = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!;

        var fremder = await w.Dienst.UpdateMarkerAsync(Guid.NewGuid(), f.Spur.Id, f.Gegenstaende[0].Id, new UpdateGpsMarkerRequest("Leder"));
        var anderesZiel = await w.Dienst.UpdateMarkerAsync(w.Nutzer, andere.Id, f.Gegenstaende[0].Id, new UpdateGpsMarkerRequest("Leder"));
        var unbekannt = await w.Dienst.UpdateMarkerAsync(w.Nutzer, f.Spur.Id, Guid.NewGuid(), new UpdateGpsMarkerRequest("Leder"));

        Assert.True(fremder.IsNotFound);
        Assert.True(anderesZiel.IsNotFound);
        Assert.True(unbekannt.IsNotFound);
        Assert.All(w.Db.GpsPoints, p => Assert.Null(p.Material));
    }

    [Fact]
    public async Task Material_BetreuendeTrainerin_DarfEsNichtEintragen()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var trainerin = Guid.NewGuid();
        w.Db.TrainerAssignments.Add(new TrainerAssignment { DogId = w.Hund, TrainerId = trainerin, MemberId = w.Nutzer, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) });
        await w.Db.SaveChangesAsync();

        // Material gehört zum Eintrag der Familie (TrainingsRechte) - die Angaben im Ablauf darf sie dagegen machen.
        Assert.True((await w.Dienst.UpdateMarkerAsync(trainerin, f.Spur.Id, f.Gegenstaende[0].Id, new UpdateGpsMarkerRequest("Leder"))).IsNotFound);
        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(trainerin, f.Spur.Id, f.Lauf.Id,
            [new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[0].Id, ArticleResult.Indicated)])).Succeeded);
    }

    // ---- Ergebnis je Gegenstand ----

    [Fact]
    public async Task Ergebnis_OhneAngaben_GibtEsKeineBestaetigteZahl_DieGpsZahlBleibt()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);

        Assert.Empty(f.Lauf.Articles);
        Assert.Null(f.Lauf.ArticlesConfirmedFound);
        Assert.Equal(3, f.Lauf.ArticlesTotal);
        Assert.Equal(3, f.Lauf.ArticlesFound);
    }

    [Fact]
    public async Task Ergebnis_Speichern_AendernUndEntfernen_MitBestaetigterZahl()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var (eins, zwei, drei) = (f.Gegenstaende[0].Id, f.Gegenstaende[1].Id, f.Gegenstaende[2].Id);

        var gespeichert = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
        [
            new UpdateGpsWalkRunArticleRequest(eins, ArticleResult.Indicated),
            new UpdateGpsWalkRunArticleRequest(zwei, ArticleResult.PickedUp),
            new UpdateGpsWalkRunArticleRequest(drei, ArticleResult.Missed),
        ]);

        Assert.True(gespeichert.Succeeded);
        Assert.Equal(3, gespeichert.Value!.Articles.Count);
        Assert.Equal(ArticleResult.PickedUp, gespeichert.Value.Articles.Single(a => a.PointId == zwei).Result);
        // Verwiesen und aufgenommen zählen als gefunden, überlaufen nicht.
        Assert.Equal(2, gespeichert.Value.ArticlesConfirmedFound);
        // Die GPS-Zahl bleibt, wie sie berechnet wurde.
        Assert.Equal(f.Lauf.ArticlesFound, gespeichert.Value.ArticlesFound);

        // Ändern: der überlaufene wird doch noch verwiesen; null nimmt den zweiten wieder heraus.
        var geaendert = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
        [
            new UpdateGpsWalkRunArticleRequest(drei, ArticleResult.Indicated),
            new UpdateGpsWalkRunArticleRequest(zwei, null),
        ]);
        Assert.Equal(2, geaendert.Value!.Articles.Count);
        Assert.DoesNotContain(geaendert.Value.Articles, a => a.PointId == zwei);
        Assert.Equal(2, geaendert.Value.ArticlesConfirmedFound);

        // Alles entfernt: wieder keine Angabe (null statt 0).
        var leer = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
        [
            new UpdateGpsWalkRunArticleRequest(eins, null),
            new UpdateGpsWalkRunArticleRequest(drei, null),
        ]);
        Assert.Empty(leer.Value!.Articles);
        Assert.Null(leer.Value.ArticlesConfirmedFound);
        Assert.Empty(w.Db.GpsWalkRunArticles.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Ergebnis_Wiederholen_IstIdempotent_UndEinEntfernenOhneAngabeIstHarmlos()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var anfrage = new[]
        {
            new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[0].Id, ArticleResult.Indicated),
            new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[1].Id, null),
        };

        await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id, anfrage);
        var zweites = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id, anfrage);

        Assert.True(zweites.Succeeded);
        Assert.Single(zweites.Value!.Articles);
        Assert.Single(w.Db.GpsWalkRunArticles);
    }

    [Fact]
    public async Task Ergebnis_DoppelteGegenstandsangabeInEinerAnfrage_ZaehltDieLetzte()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var id = f.Gegenstaende[0].Id;

        var ergebnis = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
            [new UpdateGpsWalkRunArticleRequest(id, ArticleResult.Missed), new UpdateGpsWalkRunArticleRequest(id, ArticleResult.PickedUp)]);

        Assert.Equal(ArticleResult.PickedUp, Assert.Single(ergebnis.Value!.Articles).Result);
    }

    [Fact]
    public async Task Ergebnis_FremdeOderNichtGegenstandsPunkte_LehnenDieGanzeAnfrageAb()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var andere = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!;
        var gut = new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[0].Id, ArticleResult.Indicated);

        var ungueltige = new[]
        {
            Guid.NewGuid(),                                                                       // gibt es nicht
            f.Leckerlipot.Id,                                                                     // Marker, aber kein Gegenstand
            f.Spur.Points.First(p => p.PointType == GpsPointType.Automatic).Id,                  // GPS-Punkt
            andere.Points.First(p => p.MarkerType == GpsMarkerType.Article && p.PointType == GpsPointType.Manual).Id, // andere Fährte
        };
        foreach (var id in ungueltige)
        {
            var ergebnis = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id, [gut, new UpdateGpsWalkRunArticleRequest(id, ArticleResult.Indicated)]);
            Assert.False(ergebnis.Succeeded);
            Assert.False(ergebnis.IsNotFound);
        }

        // Alles oder nichts: Auch die gültige Angabe daneben wurde nicht gespeichert.
        Assert.Empty(w.Db.GpsWalkRunArticles);
    }

    [Fact]
    public async Task Ergebnis_NullElementImArray_WirdAlsEingabefehlerAbgelehnt()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);

        var ergebnis = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
            [new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[0].Id, ArticleResult.Indicated), null!]);

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Empty(w.Db.GpsWalkRunArticles);
    }

    [Fact]
    public async Task Ergebnis_UnbekannterWert_WirdAbgelehnt()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);

        var ergebnis = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
            [new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[0].Id, (ArticleResult)9)]);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(w.Db.GpsWalkRunArticles);
    }

    [Fact]
    public async Task Ergebnis_Rechte_FremdeUndAndereAblaeufeSindNichtGefunden_MantrailingGibtEsNicht()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var anfrage = new[] { new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[0].Id, ArticleResult.Indicated) };

        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(Guid.NewGuid(), f.Spur.Id, f.Lauf.Id, anfrage)).IsNotFound);
        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, Guid.NewGuid(), anfrage)).IsNotFound);
        Assert.True((await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, Guid.NewGuid(), f.Lauf.Id, anfrage)).IsNotFound);

        var trail = await FaehrteMitLaufAsync(w, GpsTrackKind.Mantrailing);
        var markerId = trail.Spur.Points.First(p => p.PointType == GpsPointType.Manual).Id;
        var mantrailing = await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, trail.Spur.Id, trail.Lauf.Id,
            [new UpdateGpsWalkRunArticleRequest(markerId, ArticleResult.Indicated)]);
        Assert.False(mantrailing.Succeeded);
        Assert.False(mantrailing.IsNotFound);
        Assert.Empty(w.Db.GpsWalkRunArticles);
    }

    [Fact]
    public async Task Ergebnis_BleibtBeiNeuauswertungUndKommtBeimLesenMit()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
            [new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[2].Id, ArticleResult.Missed)]);

        var neu = await w.Dienst.EvaluateWalkRunAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id);

        // Eine Eingabe, kein Rechenergebnis: die Auswertung ersetzt Halte und Winkel, nicht diese Angabe.
        Assert.Equal(ArticleResult.Missed, Assert.Single(neu.Value!.Articles).Result);
        var gelesen = (await w.Dienst.GetByTrainingSessionAsync(w.Nutzer, f.Spur.TrainingSessionId)).Value!.Single().WalkRuns.Single();
        Assert.Equal(ArticleResult.Missed, Assert.Single(gelesen.Articles).Result);
        Assert.Equal(0, gelesen.ArticlesConfirmedFound);
    }

    [Fact]
    public async Task FaehrteLoeschen_LoeschtDieGegenstandsangabenWeichMit()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        await w.Dienst.UpdateWalkRunArticlesAsync(w.Nutzer, f.Spur.Id, f.Lauf.Id,
            [new UpdateGpsWalkRunArticleRequest(f.Gegenstaende[0].Id, ArticleResult.Indicated)]);

        await w.Dienst.DeleteAsync(w.Nutzer, f.Spur.Id);

        Assert.Empty(w.Db.GpsWalkRunArticles);
        Assert.NotNull(Assert.Single(w.Db.GpsWalkRunArticles.IgnoreQueryFilters()).DeletedAt);
    }

    [Fact]
    public async Task GpsHinweis_NennDieErreichtenGegenstaende_WieDieGpsZahl()
    {
        var w = await AufbauenAsync();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, Legung(w.Hund))).Value!;
        var gegenstaende = spur.Points.Where(p => p.MarkerType == GpsMarkerType.Article && p.PointType == GpsPointType.Manual).OrderBy(p => p.Timestamp).ToList();

        // Der Ablauf endet bei 45 m: Gegenstand 1 (20 m) und 2 (40 m) liegen in 8 m Nähe, Gegenstand 3 (60 m) nicht.
        var lauf = (await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, new CreateGpsWalkRunRequest(45, null,
            Enumerable.Range(0, 10).Select(i => new CreateGpsWalkPointRequest(LatAt(i * 5), BaseLon, T0.AddHours(2).AddMinutes(i), 5)).ToList()))).Value!;

        Assert.Equal(2, lauf.ArticlesFound);
        Assert.Equal(gegenstaende.Take(2).Select(g => g.Id).Order(), lauf.ReachedArticleIds.Order());
        // Beim späteren Lesen derselbe Hinweis.
        var gelesen = (await w.Dienst.GetByTrainingSessionAsync(w.Nutzer, spur.TrainingSessionId)).Value!.Single().WalkRuns.Single();
        Assert.Equal(lauf.ReachedArticleIds.Order(), gelesen.ReachedArticleIds.Order());
    }

    [Fact]
    public void GpsHinweis_UeberDerAufwandsgrenze_GibtNull()
    {
        var marker = new GpsPoint { Latitude = 48.9, Longitude = 8.5, Timestamp = T0, PointType = GpsPointType.Manual, MarkerType = GpsMarkerType.Article };
        var linie = new GpsPoint { Latitude = 48.9, Longitude = 8.5, Timestamp = T0, PointType = GpsPointType.Automatic };
        var ablauf = Enumerable.Range(0, 1000).Select(_ => new GpsWalkPoint { Latitude = 48.9, Longitude = 8.5 }).ToList();

        Assert.Single(GpsTrackEvaluator.ErreichteGegenstaende([linie, marker], ablauf)!);

        // 2 Punkte x Grenze ist weit darüber, ohne dass die Liste so groß sein muss: die Prüfung rechnet nur mit den Zahlen.
        var riesig = new GpsPoint[1] { marker }.Concat(Enumerable.Repeat(linie, (int)(GpsTrackEvaluator.MaxAuswertungsAufwand / 1000) + 1));
        Assert.Null(GpsTrackEvaluator.ErreichteGegenstaende(riesig, ablauf));
    }

    // ---- Aus der Entwicklung nehmen ----

    [Fact]
    public async Task Entwicklung_HerausnehmenUndWiederHinein_BeiEigenerFaehrte()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);

        var heraus = await w.Dienst.UpdateStatsAsync(w.Nutzer, f.Spur.Id, new UpdateGpsTrackStatsRequest(true));
        Assert.True(heraus.Succeeded);
        Assert.True(heraus.Value!.ExcludeFromStats);
        // Die Fährte bleibt vollständig erhalten.
        Assert.Equal(f.Spur.Points.Count, heraus.Value.Points.Count);
        Assert.Single(heraus.Value.WalkRuns);

        var hinein = await w.Dienst.UpdateStatsAsync(w.Nutzer, f.Spur.Id, new UpdateGpsTrackStatsRequest(false));
        Assert.False(hinein.Value!.ExcludeFromStats);
    }

    [Fact]
    public async Task Entwicklung_FremdeTrainerinUndMantrailing_Abgelehnt()
    {
        var w = await AufbauenAsync();
        var f = await FaehrteMitLaufAsync(w);
        var trail = await FaehrteMitLaufAsync(w, GpsTrackKind.Mantrailing);
        var trainerin = Guid.NewGuid();
        w.Db.TrainerAssignments.Add(new TrainerAssignment { DogId = w.Hund, TrainerId = trainerin, MemberId = w.Nutzer, StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Dienst.UpdateStatsAsync(Guid.NewGuid(), f.Spur.Id, new UpdateGpsTrackStatsRequest(true))).IsNotFound);
        Assert.True((await w.Dienst.UpdateStatsAsync(trainerin, f.Spur.Id, new UpdateGpsTrackStatsRequest(true))).IsNotFound);
        var mantrailing = await w.Dienst.UpdateStatsAsync(w.Nutzer, trail.Spur.Id, new UpdateGpsTrackStatsRequest(true));
        Assert.False(mantrailing.Succeeded);
        Assert.False(w.Db.GpsTracks.Any(t => t.ExcludeFromStats));
    }
}
