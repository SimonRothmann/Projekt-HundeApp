using Dogity.Application.Tracking;
using Dogity.Domain.Tracking;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Erzeugt die GPS-Punkte der Demo-Fährten (siehe DemoSzenarienSeeder): eine
/// Winkelfährte mit drei Gegenständen und dazu einen Ablauf, dessen
/// Abweichung sich von Lauf zu Lauf verbessert. Nur die ROHPUNKTE entstehen
/// hier - die Kennzahlen (Abweichung, "auf Fährte", Gegenstände, Halte)
/// berechnet danach der GpsTrackEvaluator beim Speichern des Ablaufs, genau
/// wie bei einer echten Aufzeichnung.
///
/// Deterministisch (feste Zufallszahlen je Lauf): zwei Server mit derselben
/// Demo-Datenbank zeigen dieselben Linien, und der Test bleibt stabil.
/// </summary>
internal static class DemoFaehrtenGeometrie
{
    // Gewählt (durch Probieren), damit die Abweichung der vier Läufe sichtbar von Fährte zu Fährte
    // sinkt: ein Zufallspfad bei kleiner Streuung kann sonst auch einmal "schlechter" ausfallen.
    private const int SaatVersatz = 1040;
    // Irgendein Übungsgelände; die Karte braucht einen Ort, mehr nicht.
    private const double StartBreite = 48.7758;
    private const double StartLaenge = 9.1829;

    /// <summary>Ecken der Winkelfährte in Metern (x = Osten, y = Norden): 150 m, Winkel, 120 m, Winkel, 130 m.</summary>
    private static readonly (double X, double Y)[] Ecken = [(0, 0), (0, 150), (120, 150), (120, 20)];

    /// <summary>Streckenmeter, an denen die drei Gegenstände liegen.</summary>
    private static readonly double[] GegenstandMeter = [130, 262, 395];

    private static readonly string[] GegenstandNamen = ["Handschuh", "Lederriemen", "Holzstück"];

    private const double LaufgeschwindigkeitLegen = 1.3;
    private const double LaufgeschwindigkeitSuchen = 0.95;

    public static double LaengeMeter { get; } = Laenge();

    /// <param name="abweichung">Streuung der seitlichen Abweichung des Hundeführers in Metern (kleiner = besser).</param>
    public static (List<CreateGpsPointRequest> Gelegt, List<CreateGpsWalkPointRequest> Abgelaufen) Erzeuge(
        int lauf, DateTimeOffset gelegtUm, TimeSpan standzeit, double abweichung)
    {
        var zufall = new Random(SaatVersatz + lauf);

        // ---- Gelegte Linie: alle 2 m ein Punkt, mit dem Rauschen eines Handys ----
        var gelegt = new List<CreateGpsPointRequest>();
        for (var meter = 0.0; meter <= LaengeMeter + 0.001; meter += 2.0)
        {
            var (x, y, _, _) = Position(meter);
            var (lat, lon) = Umrechnen(x + Normal(zufall) * 0.6, y + Normal(zufall) * 0.6);
            gelegt.Add(new CreateGpsPointRequest(lat, lon, gelegtUm.AddSeconds(meter / LaufgeschwindigkeitLegen), 4.0));
        }

        for (var i = 0; i < GegenstandMeter.Length; i++)
        {
            var (x, y, _, _) = Position(GegenstandMeter[i]);
            var (lat, lon) = Umrechnen(x, y);
            gelegt.Add(new CreateGpsPointRequest(
                lat, lon, gelegtUm.AddSeconds(GegenstandMeter[i] / LaufgeschwindigkeitLegen + 1), 3.0,
                GpsPointType.Manual, GegenstandNamen[i], GpsMarkerType.Article));
        }

        // ---- Ablauf: der Hundeführer folgt der Spur, mit wandernder seitlicher Abweichung ----
        var suchbeginn = gelegtUm.AddSeconds(LaengeMeter / LaufgeschwindigkeitLegen) + standzeit;
        var abgelaufen = new List<CreateGpsWalkPointRequest>();
        var meterAbgelaufen = 0.0;
        var seitlich = 0.0;
        var sekunde = 0;
        var gehaltenBei = new HashSet<int>();
        var haltSekunden = 0;

        while (meterAbgelaufen < LaengeMeter)
        {
            var (x, y, nx, ny) = Position(meterAbgelaufen);
            var (lat, lon) = Umrechnen(x + nx * seitlich + Normal(zufall) * 0.7, y + ny * seitlich + Normal(zufall) * 0.7);
            abgelaufen.Add(new CreateGpsWalkPointRequest(lat, lon, suchbeginn.AddSeconds(sekunde++), 4.0));

            if (haltSekunden > 0)
            {
                // Stehen bleiben: Der Hund verweist oder sucht, der Hundeführer rückt nicht vor.
                haltSekunden--;
                continue;
            }

            // Ein Halt an jedem Gegenstand (Verweisen, erwünscht). Im ersten Lauf zusätzlich
            // mitten auf dem zweiten Schenkel: der Hund sucht, ohne dass dort etwas liegt.
            for (var i = 0; i < GegenstandMeter.Length; i++)
            {
                if (meterAbgelaufen >= GegenstandMeter[i] - 1 && gehaltenBei.Add(i))
                    haltSekunden = 12;
            }
            if (lauf == 0 && meterAbgelaufen >= 205 && gehaltenBei.Add(100))
                haltSekunden = 14;

            if (haltSekunden > 0) continue;

            meterAbgelaufen += Math.Max(0.4, LaufgeschwindigkeitSuchen + Normal(zufall) * 0.15);
            // AR(1): Die Abweichung wandert, statt bei jedem Punkt neu zu würfeln.
            seitlich = 0.93 * seitlich + Normal(zufall) * abweichung * 0.37;
        }

        return (gelegt, abgelaufen);
    }

    /// <summary>Wetter der Demo-Fährten, je Lauf (Legen, Suchen) - kleine Differenzen wie am echten Tag.</summary>
    public static readonly (double LegenC, int LegenFeuchte, double LegenWind, int LegenCode,
        double SuchenC, int SuchenFeuchte, double SuchenWind, int SuchenCode)[] Wetter =
    [
        (14.2, 62, 8, 2, 15.8, 58, 10, 3),
        (11.5, 71, 5, 3, 12.1, 69, 6, 3),
        (9.8, 80, 12, 61, 8.4, 84, 14, 61),
        (13.0, 55, 6, 1, 16.4, 46, 9, 0),
    ];

    private static double Laenge()
    {
        var summe = 0.0;
        for (var i = 1; i < Ecken.Length; i++)
            summe += Math.Sqrt(Math.Pow(Ecken[i].X - Ecken[i - 1].X, 2) + Math.Pow(Ecken[i].Y - Ecken[i - 1].Y, 2));
        return summe;
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
