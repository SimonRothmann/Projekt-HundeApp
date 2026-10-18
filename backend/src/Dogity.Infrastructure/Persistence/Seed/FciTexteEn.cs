using System.Reflection;
using System.Text.Json;

namespace Dogity.Infrastructure.Persistence.Seed;

/// <summary>
/// Die englischen Fassungen der Katalogtexte der FCI-Ordnungen (Paket E4):
/// Bewertungskriterien der Übungen, Beschreibungen der Ordnungen und
/// Bewertungshinweise der Ordnungs-Übungen. Die Namen (NameEn) stehen
/// weiterhin in <see cref="FciKatalog"/>.
///
/// Die Texte liegen als Datei (Data/fci-texte-en.json) und nicht als C#-
/// Literale im Seeder: Es sind rund 35.000 Zeichen, und sie ändern sich aus
/// einem anderen Anlass als der Seeder. Geladen wird wie die Sachkunde-Kataloge
/// aus einer eingebetteten Ressource.
///
/// Es sind unsere eigenen Texte, die Übertragung der eigenen deutschen
/// Beschreibungen in die Terminologie der englischen FCI-IGP 2025 - keine Kopie
/// der offiziellen Ordnung (siehe docs/VERBAENDE_SPRACHEN_MODULE.md, Paket E4).
/// Schlüssel sind der Sportart-Code und die deutschen Namen (wie bei
/// <see cref="FciKatalog.Uebungen"/>); was hier fehlt - nationale Ordnungen,
/// Vereinsübungen, Freitext -, bleibt ohne englischen Text und zeigt Deutsch.
///
/// Bei jedem Eintrag steht zusätzlich der deutsche Text, aus dem die englische
/// Fassung entstand, und der Seeder trägt sie nur ein, wenn die Zeile genau
/// diesen Text hat. Grund: Ein Englisch, das zum angezeigten Deutsch nicht
/// passt (andere Punktzahl, andere Schrittzahl), wäre schlimmer als der
/// deutsche Rückfall. Bei den Übungen kann das jede bestehende Datenbank
/// treffen: Der Seeder legt die Bewertung einer Übung nur an, überschreibt sie
/// aber nie - Datenbanken, die älter sind als ein geänderter Seed-Text (der
/// Katalog wurde mehrfach nachgeschärft), tragen noch die ältere Fassung, und
/// ein Admin kann sie ändern; zu jedem solchen Text gibt es einen Eintrag.
/// Beschreibungen der Ordnungen und Hinweise der Ordnungs-Übungen schreibt der
/// Seeder bei jedem Start selbst und leert dabei das Englisch, sobald sich der
/// deutsche Text ändert; ohne nachgezogenen Eintrag hier zeigt die Zeile dann
/// Deutsch, und KatalogTexteEnTests schlägt fehl - die Pflicht, die Datei bei
/// jeder Textänderung nachzuziehen, ist so ein Test und kein Doku-Hinweis.
/// </summary>
public static class FciTexteEn
{
    private const string ResourceName = "Dogity.Infrastructure.Persistence.Seed.Data.fci-texte-en.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Englische Texte einer Übung samt dem deutschen Text, zu dem sie gehören
    /// (<c>Description</c>, <c>ScoringCriteria</c>); eine Übung kann mehrere
    /// Einträge haben, einen je Fassung des deutschen Textes.
    /// </summary>
    public sealed record UebungTexte(
        string Sport, string Name,
        string? Description, string? DescriptionEn,
        string? ScoringCriteria, string? ScoringCriteriaEn);

    /// <summary>Englische Beschreibung einer Ordnung samt dem deutschen Text, zu dem sie gehört, nach (Sportart-Code, deutscher Name).</summary>
    public sealed record OrdnungTexte(string Sport, string Name, string? Description, string? DescriptionEn);

    /// <summary>Englischer Bewertungshinweis einer Übung innerhalb einer Ordnung samt dem deutschen Text, zu dem er gehört.</summary>
    public sealed record OrdnungsuebungTexte(string Sport, string Ordnung, string Uebung, string? ScoringNotes, string? ScoringNotesEn);

    private sealed record Datei(
        List<UebungTexte> Uebungen,
        List<OrdnungTexte> Ordnungen,
        List<OrdnungsuebungTexte> Ordnungsuebungen);

    private static readonly Lazy<Datei> Gelesen = new(Lesen);

    public static IReadOnlyList<UebungTexte> UebungenListe => Gelesen.Value.Uebungen;
    public static IReadOnlyList<OrdnungTexte> OrdnungenListe => Gelesen.Value.Ordnungen;
    public static IReadOnlyList<OrdnungsuebungTexte> OrdnungsuebungenListe => Gelesen.Value.Ordnungsuebungen;

    public static ILookup<(string Sport, string Name), UebungTexte> Uebungen { get; } =
        UebungenListe.ToLookup(t => (t.Sport, t.Name));

    public static IReadOnlyDictionary<(string Sport, string Name), OrdnungTexte> Ordnungen { get; } =
        OrdnungenListe.ToDictionary(t => (t.Sport, t.Name));

    public static IReadOnlyDictionary<(string Sport, string Ordnung, string Uebung), OrdnungsuebungTexte> Ordnungsuebungen { get; } =
        OrdnungsuebungenListe.ToDictionary(t => (t.Sport, t.Ordnung, t.Uebung));

    private static Datei Lesen()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Eingebettete Ressource {ResourceName} fehlt.");

        return JsonSerializer.Deserialize<Datei>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"{ResourceName} ist leer oder unlesbar.");
    }
}
