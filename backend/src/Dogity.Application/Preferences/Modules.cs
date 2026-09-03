namespace Dogity.Application.Preferences;

/// <summary>
/// Die Module, die sich abschalten lassen.
///
/// Die Schlüssel sind Zeichenketten und liegen bewusst NICHT als Enum vor:
/// Ein Verband soll später eigene Module mitbringen können, ohne dass dafür
/// Code geändert wird (siehe docs/VERBAENDE_SPRACHEN_MODULE.md). Diese Liste
/// ist deshalb nur die heute bekannte Auswahl, keine abschließende Menge -
/// unbekannte Schlüssel werden beim Speichern verworfen, nicht abgelehnt,
/// damit ein älterer Client keinen Fehler auslöst.
/// </summary>
public static class Modules
{
    public const string Faehrte = "faehrte";
    public const string Sachkunde = "sachkunde";
    public const string Gruppentraining = "gruppentraining";
    public const string Wetter = "wetter";
    public const string Statistik = "statistik";

    public static readonly IReadOnlyList<string> Bekannt =
        [Faehrte, Sachkunde, Gruppentraining, Wetter, Statistik];
}
