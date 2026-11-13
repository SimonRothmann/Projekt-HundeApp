using Dogity.Application.Tracking;
using Dogity.Domain.Tracking;
using static Dogity.Application.Tests.Tracking.FaehrtenTestHilfe;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Winkel und Schenkel der gelegten Fährte (siehe FaehrtenGeometrie): Schwellen und Randfälle -
/// Rauschen darf keinen Winkel erzeugen, ein Bogen aus zwei Knicken zählt einmal.
/// </summary>
public class FaehrtenGeometrieTests
{
    [Fact]
    public void Rechteckfaehrte_HatDreiSchenkelUndZweiWinkelRechtsUndLinks()
    {
        // 100 m nach Norden, 60 m nach Osten (rechts), 80 m nach Norden (links).
        var g = FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 100), (60, 100), (60, 180)));

        Assert.Equal(2, g.Winkel.Count);
        Assert.Equal(CornerDirection.Right, g.Winkel[0].Direction);
        Assert.Equal(CornerDirection.Left, g.Winkel[1].Direction);
        Assert.All(g.Winkel, w => Assert.Equal(90, w.TurnDegrees, 1));
        Assert.All(g.Winkel, w => Assert.False(w.Sharp));
        Assert.Equal([1, 2], g.Winkel.Select(w => w.Number));
        Assert.Equal(100, g.Winkel[0].StationMeters, 1);
        Assert.Equal(160, g.Winkel[1].StationMeters, 1);
        Assert.Equal(Lat(100), g.Winkel[0].Latitude, 6);
        Assert.Equal(Lon(0), g.Winkel[0].Longitude, 6);

        Assert.Equal(3, g.Schenkel.Count);
        Assert.Equal([100.0, 60.0, 80.0], g.Schenkel.Select(s => Math.Round(s.LengthMeters)));
        Assert.Equal(0, g.Schenkel[0].StartMeters, 1);
        Assert.Equal(100, g.Schenkel[0].EndMeters, 1);
        Assert.Equal(100, g.Schenkel[1].StartMeters, 1);
        Assert.Equal(240, g.LengthMeters, 1);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(110, true)]
    [InlineData(150, true)]
    public void SpitzIstEinWinkelUeberHundertFuenfGradRichtungsaenderung(double grad, bool spitz)
    {
        // Nach 100 m Norden biegt die Fährte um `grad` nach rechts ab und läuft 80 m weiter.
        var rad = grad * Math.PI / 180;
        var g = FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 100), (80 * Math.Sin(rad), 100 + 80 * Math.Cos(rad))));

        var winkel = Assert.Single(g.Winkel);
        Assert.Equal(spitz, winkel.Sharp);
        Assert.Equal(CornerDirection.Right, winkel.Direction);
        Assert.Equal(grad, winkel.TurnDegrees, 1);
    }

    [Fact]
    public void Zittern_ErzeugtKeinenWinkel()
    {
        // Eine gerade Fährte nach Norden, mit ±2 m seitlichem Rauschen: unter der Vereinfachung.
        var punkte = Legung((0, 0), (0, 120));
        for (var i = 0; i < punkte.Count; i++)
            punkte[i].Longitude = Lon(i % 2 == 0 ? 2 : -2);

        var g = FaehrtenGeometrie.Berechnen(punkte);

        Assert.Empty(g.Winkel);
        Assert.Single(g.Schenkel);
    }

    [Fact]
    public void ZickzackUnterZehnMetern_ErzeugtKeinenWinkel()
    {
        // Ausschläge von 4,5 m bleiben nach der Vereinfachung stehen, liegen aber weniger als
        // 10 m auseinander: Sie heben sich auf, die Richtung vorher und nachher ist dieselbe.
        var wege = new List<(double X, double Y)> { (0, 0), (0, 50) };
        for (var i = 0; i < 7; i++)
            wege.Add((i % 2 == 0 ? 4.5 : -4.5, 53 + i * 3));
        wege.Add((0, 80));
        wege.Add((0, 140));

        var g = FaehrtenGeometrie.Berechnen(Legung(wege.ToArray()));

        Assert.Empty(g.Winkel);
        Assert.Single(g.Schenkel);
    }

    [Fact]
    public void ZweiNaheKnicke_WerdenEinWinkelAmMittlerenPunkt()
    {
        // Zwei Wendungen um je 45 Grad, 8 m auseinander: zusammen ein rechter Winkel.
        var rad = 45 * Math.PI / 180;
        var mitte = (X: 8 * Math.Sin(rad), Y: 50 + 8 * Math.Cos(rad));
        var g = FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 50), mitte, (mitte.X + 60, mitte.Y)));

        var winkel = Assert.Single(g.Winkel);
        Assert.Equal(90, winkel.TurnDegrees, 1);
        Assert.Equal(CornerDirection.Right, winkel.Direction);
        Assert.Equal(54, winkel.StationMeters, 1);
        Assert.Equal(2, g.Schenkel.Count);
    }

    [Fact]
    public void ZweiKnickeGetrennt_BleibenZweiWinkel()
    {
        // Dieselben Wendungen, aber 14 m auseinander und je 60 Grad: zwei Winkel.
        var rad = 60 * Math.PI / 180;
        var mitte = (X: 14 * Math.Sin(rad), Y: 50 + 14 * Math.Cos(rad));
        var g = FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 50), mitte, (mitte.X + 60, mitte.Y - 60 * Math.Tan(30 * Math.PI / 180))));

        Assert.Equal(2, g.Winkel.Count);
        Assert.Equal(3, g.Schenkel.Count);
    }

    [Fact]
    public void GeradeFaehrte_IstEinSchenkelOhneWinkel()
    {
        var g = FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 100)));

        Assert.Empty(g.Winkel);
        var schenkel = Assert.Single(g.Schenkel);
        Assert.Equal(100, schenkel.LengthMeters, 1);
        Assert.Equal(1, schenkel.Number);
    }

    [Fact]
    public void EinKnickKurzNachDemStartOderVorDemEnde_IstKeinWinkel()
    {
        // Der Abschnitt davor bzw. danach ist kürzer als 10 m: nicht sicher zu erkennen.
        Assert.Empty(FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 6), (60, 6))).Winkel);
        Assert.Empty(FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 100), (6, 100))).Winkel);
    }

    [Fact]
    public void ZickzackMitTausendenWinkeln_ErgibtWederWinkelNochSchenkel()
    {
        // Rechtwinkliges Zickzack mit 15-m-Abschnitten: weit mehr Winkel als MaxWinkel.
        var wege = new List<(double X, double Y)> { (0, 0) };
        for (var i = 1; i <= FaehrtenGeometrie.MaxWinkel + 20; i++)
            wege.Add(i % 2 == 1 ? (wege[^1].X, wege[^1].Y + 15) : (wege[^1].X + 15, wege[^1].Y));

        var g = FaehrtenGeometrie.Berechnen(Legung(wege.ToArray()));

        Assert.Empty(g.Winkel);
        Assert.Empty(g.Schenkel);
    }

    [Fact]
    public void ZuWenigPunkte_ErgebenNichts()
    {
        var g = FaehrtenGeometrie.Berechnen([Legung((0, 0), (0, 1))[0]]);

        Assert.Empty(g.Winkel);
        Assert.Empty(g.Schenkel);
    }

    [Fact]
    public void DieStationierung_ProjiziertEinenPunktAufDieLinie()
    {
        var g = FaehrtenGeometrie.Berechnen(Legung((0, 0), (0, 100), (60, 100)));

        // 5 m neben dem Schenkel bei 40 m, und ein Punkt hinter dem Ende.
        Assert.Equal(40, g.Stationierung(Lat(40), Lon(5)), 1);
        Assert.Equal(160, g.Stationierung(Lat(100), Lon(80)), 1);
    }
}
