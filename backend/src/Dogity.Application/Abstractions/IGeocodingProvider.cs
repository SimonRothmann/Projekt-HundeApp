namespace Dogity.Application.Abstractions;

/// <summary>
/// Ein Treffer der Ortssuche. <paramref name="Name"/> ist die Zeile, die man
/// wiedererkennt ("Hundesportverein"), <paramref name="Detail"/> die Einordnung
/// darunter ("Pforzheimer Straße 78 · 76275 Ettlingen"). Beides wird bereits
/// serverseitig zusammengesetzt, damit das Frontend nur zwei Zeilen ausgibt.
/// </summary>
public record GeocodeResult(string Name, string? Detail, double Latitude, double Longitude);

/// <summary>
/// Ortssuche für die Trainings-Erfassung. Bewusst getrennt vom
/// <see cref="IWeatherProvider"/>: das sind zwei verschiedene Dienste, und die
/// Anforderungen decken sich nicht.
///
/// Gesucht werden vor allem HUNDEPLÄTZE - also benannte Orte, keine Gemeinden.
/// Ein reines Ortsverzeichnis (wie das Geocoding von Open-Meteo) findet
/// "Hundeplatz Musterstadt" nicht einmal ansatzweise; es kennt nur Städte und
/// Dörfer. Deshalb liegt hier eine Suche über OpenStreetMap-Daten dahinter, wo
/// Hundeplätze sogar eine eigene Kategorie haben (<c>amenity=animal_training</c>).
/// </summary>
public interface IGeocodingProvider
{
    /// <summary>
    /// Sucht Orte zum Suchbegriff. <paramref name="nearLatitude"/>/
    /// <paramref name="nearLongitude"/> gewichten das Ergebnis auf die Umgebung
    /// - "Hundeplatz" gibt es hundertfach, gemeint ist praktisch immer einer in
    /// der Nähe. Bei Nichterreichbarkeit kommt eine leere Liste zurück, keine
    /// Ausnahme: eine fehlgeschlagene Suche darf die Eingabe nicht blockieren,
    /// der Ort lässt sich auch von Hand eintragen.
    /// </summary>
    Task<IReadOnlyList<GeocodeResult>> SearchAsync(
        string query,
        double? nearLatitude = null,
        double? nearLongitude = null,
        CancellationToken ct = default);

    /// <summary>
    /// Umgekehrt: Koordinaten zu einer Bezeichnung. Gebraucht für "Aktuellen
    /// Standort verwenden" - ohne das hieße jeder so gesetzte Ort gleich, und
    /// die Liste der zuletzt genutzten Orte wäre wertlos.
    ///
    /// Liefert <c>null</c>, wenn nichts Sinnvolles ermittelbar ist; der Name
    /// ist immer nur ein Vorschlag und bleibt von Hand änderbar.
    /// </summary>
    Task<GeocodeResult?> ReverseAsync(double latitude, double longitude, CancellationToken ct = default);
}
