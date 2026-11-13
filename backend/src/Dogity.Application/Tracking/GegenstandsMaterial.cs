namespace Dogity.Application.Tracking;

/// <summary>
/// Woraus ein gelegter Gegenstand sein kann. Gespeichert wird der deutsche Begriff (wie beim
/// Untergrund der Fährte); das Frontend führt dieselbe Liste in <c>lib/gegenstaende.ts</c>
/// und übersetzt sie für die Anzeige. Der Server prüft gegen diese Liste, damit die Fundquote je
/// Material nicht an Schreibweisen zerfällt.
/// </summary>
public static class GegenstandsMaterial
{
    public static readonly IReadOnlyList<string> Alle = ["Leder", "Holz", "Stoff", "Teppich", "Kunststoff", "Sonstiges"];
}
