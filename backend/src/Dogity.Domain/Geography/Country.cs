using Dogity.Domain.Common;

namespace Dogity.Domain.Geography;

/// <summary>
/// Ein wählbarer Geltungsbereich für Prüfungsordnungen.
///
/// Eigene Tabelle und keine Liste im Code: Ein Land soll aufgenommen werden
/// können, ohne dass jemand deployt (siehe
/// docs/VERBAENDE_SPRACHEN_MODULE.md). Aus den vorhandenen Ordnungen ließe
/// sich die Liste nicht ableiten - ein Land ohne Inhalte käme darin gar
/// nicht vor, und genau die sollen wählbar sein.
///
/// Bewusst OHNE Namensfeld: Ländernamen sind in jeder Oberflächensprache
/// andere, und der Browser kennt sie bereits (Intl.DisplayNames). Sie hier
/// zu führen hieße, eine Übersetzungstabelle zu pflegen, die es fertig gibt.
/// </summary>
public class Country : Entity
{
    /// <summary>ISO 3166-1 alpha-2, immer groß ("DE").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Kleinere Zahl steht weiter oben. Damit stehen die Länder mit Inhalten
    /// vorn, statt dass man sie in einer alphabetischen Liste sucht.
    /// </summary>
    public int SortOrder { get; set; }
}
