using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Tracking;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Tracking;
using static Dogity.Application.Tests.Tracking.FaehrtenTestHilfe;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Lücken im Ablauf (Bildschirm aus, kein GPS): Regel, Kennzeichnung am DTO, Vereinfachung ohne erfundene
/// oder verlorene Lücken und Gegenstände "ohne Aufzeichnung". Die Regel gilt gleich im Frontend
/// (lib/ablauf-luecken.ts).
/// </summary>
public class AblaufLueckenTests
{
    private static GpsWalkPoint Ablaufpunkt(double x, double y, double sekunde) => new()
    {
        Latitude = Lat(y),
        Longitude = Lon(x),
        Timestamp = T0.AddHours(1).AddSeconds(sekunde)
    };

    // ---- Regel ----

    [Theory]
    [InlineData(217, 8.7, true)] // Befund aus der Praxis: Bildschirm aus, nächster Punkt auf dem Rückweg
    [InlineData(79, 2.1, false)] // Stehen am Gegenstand
    [InlineData(20, 15, false)] // schneller Weg, nur kurz kein Punkt
    [InlineData(30, 10, false)] // genau 30 s ist noch keine Lücke
    [InlineData(31, 10, true)]
    [InlineData(100, 4.9, false)] // unter 5 m ebenso nicht
    public void Regel_NurWennZeitUndStreckeUeberschrittenSind(double sekunden, double meter, bool erwartet)
    {
        var punkte = new List<GpsWalkPoint> { Ablaufpunkt(0, 0, 0), Ablaufpunkt(0, meter, sekunden) };

        Assert.Equal(erwartet ? [1] : [], GpsTrackEvaluator.LueckenAnfaenge(punkte));
    }

    // ---- Vereinfachung im DTO ----

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

    private static async Task<GpsWalkRunDto> LaufSpeichernAsync(Welt w, IEnumerable<GpsWalkPoint> ablauf)
    {
        var legung = Legung((0, 0), (0, 4000)).Select(p => new CreateGpsPointRequest(p.Latitude, p.Longitude, p.Timestamp, 5)).ToList();
        var spur = (await w.Dienst.CreateAsync(w.Nutzer, new CreateGpsTrackRequest(null, 4000, null, null, null, null, null, legung,
            DogId: w.Hund, Date: new DateOnly(2026, 1, 1), DurationMinutes: 30))).Value!;
        await w.Dienst.AddWalkRunAsync(w.Nutzer, spur.Id, new CreateGpsWalkRunRequest(4000, null,
            ablauf.Select(p => new CreateGpsWalkPointRequest(p.Latitude, p.Longitude, p.Timestamp, 5)).ToList()));
        return (await w.Dienst.GetByTrainingSessionAsync(w.Nutzer, spur.TrainingSessionId)).Value!.Single().WalkRuns.Single();
    }

    /// <summary>Gerade nach Norden: ein Punkt je <paramref name="schritt"/> m und <paramref name="takt"/> s, ab <paramref name="ab"/>.</summary>
    private static IEnumerable<GpsWalkPoint> Gerade(int anzahl, double ab, double startSekunde, double schritt = 2, double takt = 3) =>
        Enumerable.Range(0, anzahl).Select(i => Ablaufpunkt(0, ab + i * schritt, startSekunde + i * takt));

    [Fact]
    public async Task Vereinfachen_ErfindetKeineLuecke_BeiLangsamemGeradenGehen()
    {
        var w = await AufbauenAsync();

        // 2.400 Punkte, je 2 m und 3 s: wird vereinfacht (ab 2.000 Punkten) und besteht danach aus wenigen
        // Punkten mit großen Abständen - das wäre als Lücke lesbar, ist aber keine.
        var lauf = await LaufSpeichernAsync(w, Gerade(2400, 0, 0));

        Assert.True(lauf.Points.Count < 2400);
        Assert.DoesNotContain(lauf.Points, p => p.AfterGap);
    }

    [Fact]
    public async Task Vereinfachen_BehaeltDieEndpunkteEinerLuecke()
    {
        var w = await AufbauenAsync();

        // Zwei gerade Stücke in einer Linie, dazwischen 100 s ohne Punkt und 20 m Sprung. Als eine Linie
        // vereinfacht, verschwänden die Endpunkte der Lücke.
        var vorher = Gerade(1200, 0, 0).ToList();
        var nachher = Gerade(1200, 1199 * 2 + 22, vorher[^1].Timestamp.Subtract(T0.AddHours(1)).TotalSeconds + 100).ToList();
        var lauf = await LaufSpeichernAsync(w, vorher.Concat(nachher));

        var index = lauf.Points.ToList().FindIndex(p => p.AfterGap);
        Assert.True(index > 0);
        Assert.Single(lauf.Points, p => p.AfterGap);
        Assert.Equal(vorher[^1].Timestamp, lauf.Points[index - 1].Timestamp);
        Assert.Equal(nachher[0].Timestamp, lauf.Points[index].Timestamp);
    }

    // ---- Gegenstände ohne Aufzeichnung ----

    [Fact]
    public void Gegenstaende_InDerLuecke_SindOhneAufzeichnung_AndereBleibenNichtErreicht()
    {
        // U-Form wie im Befund: 55 m hin, 10 m quer, 55 m zurück.
        var gelegt = Legung((0, 0), (0, 55), (10, 55), (10, 0));
        var erreicht1 = Marker(0, 10, GpsMarkerType.Article);
        var erreicht2 = Marker(0, 25, GpsMarkerType.Article);
        var inLuecke1 = Marker(0, 45, GpsMarkerType.Article);
        var inLuecke2 = Marker(5, 55, GpsMarkerType.Article);
        var vorDemStart = Marker(0, -20, GpsMarkerType.Article);
        gelegt.AddRange([erreicht1, erreicht2, inLuecke1, inLuecke2, vorDemStart]);

        // Hinweg bis 33 m, dann 217 s ohne Punkt, der nächste liegt schon auf dem Rückweg.
        var hin = new Lauf(0, 0).Gehe(0, 33);
        var zurueck = new Lauf(10, 40, hin.Punkte[^1].Timestamp.AddSeconds(217)).Gehe(10, 0);
        var ablauf = hin.Punkte.Concat(zurueck.Punkte).ToList();

        var erreicht = GpsTrackEvaluator.ErreichteGegenstaende(gelegt, ablauf)!;
        var ohne = GpsTrackEvaluator.NichtAufgezeichneteGegenstaende(gelegt, ablauf, erreicht);

        Assert.DoesNotContain(inLuecke1.Id, erreicht);
        Assert.DoesNotContain(inLuecke2.Id, erreicht);
        Assert.Equal(new[] { inLuecke1.Id, inLuecke2.Id }.Order(), ohne.Order());
        Assert.DoesNotContain(vorDemStart.Id, ohne);
        Assert.DoesNotContain(erreicht1.Id, ohne);
    }

    [Fact]
    public void Gegenstaende_OhneLuecke_KeinerOhneAufzeichnung()
    {
        var gelegt = Legung((0, 0), (0, 55));
        gelegt.Add(Marker(0, 45, GpsMarkerType.Article));
        var ablauf = new Lauf(0, 0).Gehe(0, 20).Punkte;

        Assert.Empty(GpsTrackEvaluator.NichtAufgezeichneteGegenstaende(gelegt, ablauf, GpsTrackEvaluator.ErreichteGegenstaende(gelegt, ablauf)!));
    }
}
