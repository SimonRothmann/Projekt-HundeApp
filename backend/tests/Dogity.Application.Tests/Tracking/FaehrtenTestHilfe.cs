using Dogity.Domain.Tracking;

namespace Dogity.Application.Tests.Tracking;

/// <summary>
/// Baut Fährten und Abläufe aus Wegpunkten in Metern (X nach Osten, Y nach Norden ab einem festen
/// Bezugspunkt) - für die Tests der Winkel und Schenkel (FaehrtenGeometrie, GpsTrackEvaluator).
/// </summary>
internal static class FaehrtenTestHilfe
{
    internal static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private const double BaseLat = 50.0;
    private const double BaseLon = 8.0;
    // Dieselbe Erdkugel wie die Projektion des Produktivcodes, damit Meter hier Meter dort sind.
    private const double MetersPerDegree = 6371000 * Math.PI / 180;

    internal static double Lat(double y) => BaseLat + y / MetersPerDegree;
    internal static double Lon(double x) => BaseLon + x / (MetersPerDegree * Math.Cos(BaseLat * Math.PI / 180));

    /// <summary>Gelegte Fährte entlang der Wegpunkte, ein automatischer Punkt je Meter und Sekunde.</summary>
    internal static List<GpsPoint> Legung(params (double X, double Y)[] wege)
    {
        var punkte = new List<GpsPoint>();
        var sekunde = 0;
        punkte.Add(Punkt(wege[0].X, wege[0].Y, sekunde++));
        for (var i = 1; i < wege.Length; i++)
        {
            var (x0, y0) = wege[i - 1];
            var (x1, y1) = wege[i];
            var schritte = (int)Math.Round(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)));
            for (var s = 1; s <= schritte; s++)
                punkte.Add(Punkt(x0 + (x1 - x0) * s / schritte, y0 + (y1 - y0) * s / schritte, sekunde++));
        }

        return punkte;
    }

    private static GpsPoint Punkt(double x, double y, int sekunde) => new()
    {
        Latitude = Lat(y),
        Longitude = Lon(x),
        Timestamp = T0.AddSeconds(sekunde),
        PointType = GpsPointType.Automatic
    };

    internal static GpsPoint Marker(double x, double y, GpsMarkerType art) => new()
    {
        Latitude = Lat(y),
        Longitude = Lon(x),
        Timestamp = T0,
        PointType = GpsPointType.Manual,
        MarkerType = art
    };

    /// <summary>Ein Ablauf, Schritt für Schritt aufgebaut: gehen und stehen, ein Punkt je Sekunde.</summary>
    internal sealed class Lauf
    {
        private double _x, _y;
        private double _sekunde;
        private readonly DateTimeOffset _start;

        internal List<GpsWalkPoint> Punkte { get; } = [];

        internal Lauf(double x, double y, DateTimeOffset? start = null)
        {
            _x = x;
            _y = y;
            _start = start ?? T0.AddHours(1);
            Punkte.Add(Neu());
        }

        /// <summary>Geradeaus zum Ziel in Meter-Schritten; <paramref name="sekundenProMeter"/> bestimmt das Tempo.</summary>
        internal Lauf Gehe(double x, double y, double sekundenProMeter = 1.5)
        {
            var laenge = Math.Sqrt((x - _x) * (x - _x) + (y - _y) * (y - _y));
            var schritte = Math.Max(1, (int)Math.Round(laenge));
            var (x0, y0) = (_x, _y);
            for (var s = 1; s <= schritte; s++)
            {
                _x = x0 + (x - x0) * s / schritte;
                _y = y0 + (y - y0) * s / schritte;
                _sekunde += sekundenProMeter * laenge / schritte;
                Punkte.Add(Neu());
            }

            return this;
        }

        /// <summary>Stehen bleiben: ein Punkt je Sekunde an derselben Stelle.</summary>
        internal Lauf Halt(int sekunden)
        {
            for (var s = 0; s < sekunden; s++)
            {
                _sekunde++;
                Punkte.Add(Neu());
            }

            return this;
        }

        private GpsWalkPoint Neu() => new()
        {
            Latitude = Lat(_y),
            Longitude = Lon(_x),
            Timestamp = _start.AddSeconds(_sekunde)
        };
    }
}
