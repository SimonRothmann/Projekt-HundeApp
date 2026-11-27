namespace Dogity.Application.Tracking;

/// <summary>
/// Die festen Listen und Grenzen der Angaben zur Schweißfährte. Gespeichert wird der deutsche
/// Begriff (wie beim Material eines Gegenstands); das Frontend führt dieselben Listen in
/// <c>lib/spurart.ts</c> und übersetzt sie für die Anzeige.
/// </summary>
public static class Schweissangaben
{
    public static readonly IReadOnlyList<string> Verfahren = ["Tupf", "Tropf", "Fährtenschuh"];

    public static readonly IReadOnlyList<string> Wildarten =
        ["Rotwild", "Damwild", "Schwarzwild", "Sikawild", "Muffelwild", "Gamswild", "Rehwild", "Sonstiges"];

    public const int MaxMl = 1000;

    /// <summary>Längste Mindest-Stehzeit in Stunden (längste Prüfungsfährte 40 h); die Frist zum Arbeiten bleibt 72 h (<c>Ablauffristen</c>).</summary>
    public const int MaxStehzeitStunden = 48;
}
