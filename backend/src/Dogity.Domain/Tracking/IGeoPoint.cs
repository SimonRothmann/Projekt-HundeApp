namespace Dogity.Domain.Tracking;

/// <summary>
/// Gemeinsames Minimal-Interface für GPS-Koordinaten, implementiert von
/// <see cref="GpsPoint"/> und <see cref="GpsWalkPoint"/> - erlaubt
/// generischen Code (z.B. Linienvereinfachung), der für beide Punkttypen
/// gilt, ohne Domain-Logik zu duplizieren.
/// </summary>
public interface IGeoPoint
{
    double Latitude { get; }
    double Longitude { get; }
}
