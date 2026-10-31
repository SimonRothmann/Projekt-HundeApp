using Dogity.Application.Tracking;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Die GPS-Punkte des Demo-Trails (Mantrailing, siehe DemoSzenarienSeeder): die Laufspur der
/// Versteckperson durch ein Wohngebiet - ein Rechteck aus drei Straßen mit zwei
/// Richtungswechseln, ca. 300 m - und der Lauf des Hundes, der teils 5 bis 20 m neben der Spur
/// arbeitet. Wie bei <see cref="DemoFaehrtenGeometrie"/> entstehen nur die ROHPUNKTE; die
/// Auswertung (10/25 m, "auf der Spur") macht der GpsTrackEvaluator beim Speichern.
///
/// Deterministisch (feste Zufallszahlen), damit zwei Server dieselbe Linie zeigen.
/// </summary>
internal static class DemoTrailGeometrie
{
    private const int Saat = 3010;
    // Irgendein Wohngebiet; die Karte braucht einen Ort, mehr nicht.
    private const double StartBreite = 48.7770;
    private const double StartLaenge = 9.1850;

    /// <summary>Ecken der Laufspur in Metern (x = Osten, y = Norden): 100 m, Wechsel, 100 m, Wechsel, 100 m.</summary>
    private static readonly (double X, double Y)[] Ecken = [(0, 0), (0, 100), (100, 100), (100, 0)];

    private const double Gehtempo = 1.4;
    private const double Suchtempo = 0.9;
    /// <summary>Wie lange der Hund an der Person anzeigt, bevor der Hundeführer da ist (der Lauf endet dort).</summary>
    private const int AnzeigeSekunden = 20;

    public const double LaengeMeter = 300;

    public static (List<CreateGpsPointRequest> Gelegt, List<CreateGpsWalkPointRequest> Abgelaufen) Erzeuge(
        DateTimeOffset gelegtUm, TimeSpan liegezeit)
    {
        var zufall = new Random(Saat);

        // Die Versteckperson geht im normalen Schritt, ein Punkt alle 2 m.
        var gelegt = new List<CreateGpsPointRequest>();
        for (var meter = 0.0; meter <= LaengeMeter + 0.001; meter += 2.0)
        {
            var (x, y, _, _) = Position(meter);
            var (lat, lon) = Umrechnen(x + Normal(zufall) * 0.6, y + Normal(zufall) * 0.6);
            gelegt.Add(new CreateGpsPointRequest(lat, lon, gelegtUm.AddSeconds(meter / Gehtempo), 4.0));
        }

        // Das Team sucht nach Ablauf der Liegezeit. Der Hund zieht seitlich in den Wind: Die Linie
        // des Hundeführers pendelt zwischen 3 und 19 m neben der Spur und kommt an den Ecken zurück.
        var suchbeginn = gelegtUm.AddSeconds(LaengeMeter / Gehtempo) + liegezeit;
        var abgelaufen = new List<CreateGpsWalkPointRequest>();
        var meterAbgelaufen = 0.0;
        var sekunde = 0;
        while (meterAbgelaufen < LaengeMeter)
        {
            var (x, y, nx, ny) = Position(meterAbgelaufen);
            var seitlich = 11 + 8 * Math.Sin(2 * Math.PI * meterAbgelaufen / 140 + 0.6) + Normal(zufall) * 0.8;
            var (lat, lon) = Umrechnen(x + nx * seitlich, y + ny * seitlich);
            abgelaufen.Add(new CreateGpsWalkPointRequest(lat, lon, suchbeginn.AddSeconds(sekunde++), 4.0));
            meterAbgelaufen += Math.Max(0.4, Suchtempo + Normal(zufall) * 0.15);
        }

        // Am Ende steht das Team bei der Person: Der Hund zeigt an (vorsitzen), der Hundeführer rückt nicht vor.
        var (ex, ey, _, _) = Position(LaengeMeter);
        for (var i = 0; i < AnzeigeSekunden; i++)
        {
            var (lat, lon) = Umrechnen(ex + 3 + Normal(zufall) * 0.4, ey + Normal(zufall) * 0.4);
            abgelaufen.Add(new CreateGpsWalkPointRequest(lat, lon, suchbeginn.AddSeconds(sekunde++), 4.0));
        }

        return (gelegt, abgelaufen);
    }

    /// <summary>Punkt nach <paramref name="meter"/> Streckenmetern samt Einheitsnormale (rechts der Laufrichtung).</summary>
    private static (double X, double Y, double Nx, double Ny) Position(double meter)
    {
        var rest = meter;
        for (var i = 1; i < Ecken.Length; i++)
        {
            var dx = Ecken[i].X - Ecken[i - 1].X;
            var dy = Ecken[i].Y - Ecken[i - 1].Y;
            var laenge = Math.Sqrt(dx * dx + dy * dy);
            if (rest <= laenge || i == Ecken.Length - 1)
            {
                var anteil = Math.Min(rest, laenge) / laenge;
                return (Ecken[i - 1].X + dx * anteil, Ecken[i - 1].Y + dy * anteil, dy / laenge, -dx / laenge);
            }
            rest -= laenge;
        }
        throw new InvalidOperationException("Unerreichbar: die Schleife endet mit dem letzten Schenkel.");
    }

    private static (double Lat, double Lon) Umrechnen(double x, double y)
    {
        const double meterJeGrad = 111_320.0;
        var lat = StartBreite + y / meterJeGrad;
        var lon = StartLaenge + x / (meterJeGrad * Math.Cos(StartBreite * Math.PI / 180));
        return (lat, lon);
    }

    // Box-Muller: eine standardnormalverteilte Zahl aus zwei gleichverteilten.
    private static double Normal(Random zufall)
    {
        var u1 = 1.0 - zufall.NextDouble();
        var u2 = zufall.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
