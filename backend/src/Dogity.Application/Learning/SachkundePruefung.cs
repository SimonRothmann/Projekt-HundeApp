using System.Text.Json;
using Dogity.Domain.Learning;

namespace Dogity.Application.Learning;

/// <summary>
/// Die Regeln der Übungsprüfung als reine Funktionen: welche Fragen drankommen
/// und wie gewertet wird. Ohne Datenbank und mit einspritzbarem Zufall, damit
/// sich beides in Tests festnageln lässt.
///
/// Die Wertungsregel ist die auf Vereinsseiten verbreitete - nicht eine, die
/// in den swhv-PDFs steht (siehe docs/SACHKUNDE.md, "Übungsprüfung"). Deshalb
/// heißt das Ergebnis in der Oberfläche "Übung bestanden", nie "Bestanden".
/// </summary>
public static class SachkundePruefung
{
    /// <summary>Punkte für jede richtig angekreuzte Antwort - und Abzug für jede falsch angekreuzte.</summary>
    public const int PunkteJeAntwort = 2;

    /// <summary>Bestanden ab diesem Anteil der möglichen Punkte.</summary>
    public const int BestehensProzent = 70;

    /// <summary>Wie viele Ergebnisse der Verlauf zeigt.</summary>
    public const int VerlaufLaenge = 10;

    /// <summary>Wie viele der letzten Ergebnisse in die Kurzbilanz ("x von 5 bestanden") eingehen.</summary>
    public const int BilanzLaenge = 5;

    // ---------- Auswahl ----------

    /// <summary>
    /// Ob sich die Frage automatisch werten lässt. Freitextfragen nicht - dort
    /// schätzt man sich selbst ein, und das taugt nicht für eine Prüfung.
    /// Eine Auswahlfrage ohne richtige Antwort oder eine Zuordnung ohne Begriffe
    /// (die Verwaltung lässt beides nicht zu) wäre ebenfalls nicht wertbar.
    /// </summary>
    public static bool IstWertbar(QuizQuestion frage) =>
        frage.DeletedAt is null && frage.Kind switch
        {
            QuizQuestionKind.SingleChoice or QuizQuestionKind.MultipleChoice =>
                frage.Options.Any(o => o.DeletedAt is null && o.Kind == QuizOptionKind.Answer && o.IsCorrect),
            QuizQuestionKind.Assignment =>
                frage.Options.Any(o => o.DeletedAt is null && o.Kind == QuizOptionKind.Term),
            _ => false
        };

    /// <summary>
    /// Die Fragen einer Übungsprüfung.
    ///
    /// Verlangt der Katalog mehr Fragen, als wertbare da sind, kommen alle
    /// dran. Sonst anteilig nach Komplex (Verfahren der größten
    /// Reste, siehe <see cref="Verteilen"/>), innerhalb des Komplexes zufällig.
    /// Die Reihenfolge der fertigen Prüfung ist gemischt.
    /// </summary>
    public static IReadOnlyList<QuizQuestion> WaehleFragen(IEnumerable<QuizQuestion> fragen, int anzahl, Random zufall)
    {
        // Nach SortOrder vorsortiert, damit ein festgelegter Zufall dasselbe
        // ergibt, egal in welcher Reihenfolge die Datenbank liefert.
        var wertbar = fragen.Where(IstWertbar).OrderBy(q => q.SortOrder).ToList();

        List<QuizQuestion> auswahl;
        if (anzahl >= wertbar.Count)
        {
            auswahl = wertbar;
        }
        else
        {
            var komplexe = wertbar
                .GroupBy(q => q.Section, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();
            var anteile = Verteilen(komplexe.Select(g => g.Count()).ToList(), anzahl);

            auswahl = [];
            for (var i = 0; i < komplexe.Count; i++)
                auswahl.AddRange(Mischen(komplexe[i].ToList(), zufall).Take(anteile[i]));
        }

        return Mischen(auswahl, zufall);
    }

    /// <summary>
    /// Verteilt <paramref name="anzahl"/> Plätze anteilig auf Gruppen (Verfahren
    /// der größten Reste): jede Gruppe bekommt den abgerundeten Anteil, die
    /// übrigen Plätze gehen an die größten Nachkommastellen. Bei Gleichstand
    /// gewinnt die frühere Gruppe - damit das Ergebnis nicht vom Zufall abhängt.
    ///
    /// Erwachsene: 25 aus A 23 · B 19 · C 10 · D 10 · E 7 ergibt 8 · 7 · 4 · 4 · 2.
    /// </summary>
    public static IReadOnlyList<int> Verteilen(IReadOnlyList<int> groessen, int anzahl)
    {
        var gesamt = groessen.Sum();
        if (gesamt == 0 || anzahl <= 0) return groessen.Select(_ => 0).ToList();
        if (anzahl >= gesamt) return groessen.ToList();

        // Ganzzahlig gerechnet: der Rest ist (anzahl * größe) mod gesamt, der
        // Anteil (anzahl * größe) / gesamt - keine Gleitkommafehler an der Kante.
        var anteile = groessen.Select(g => (int)((long)anzahl * g / gesamt)).ToArray();
        var reste = groessen.Select((g, i) => (Index: i, Nachkomma: (long)anzahl * g % gesamt)).ToList();

        var uebrig = anzahl - anteile.Sum();
        foreach (var treffer in reste.OrderByDescending(r => r.Nachkomma).ThenBy(r => r.Index).Take(uebrig))
            anteile[treffer.Index]++;

        return anteile;
    }

    private static List<QuizQuestion> Mischen(List<QuizQuestion> liste, Random zufall)
    {
        // Fisher-Yates.
        for (var i = liste.Count - 1; i > 0; i--)
        {
            var j = zufall.Next(i + 1);
            (liste[i], liste[j]) = (liste[j], liste[i]);
        }
        return liste;
    }

    // ---------- Wertung ----------

    /// <summary>
    /// Die Antworten, die bei einer Frage richtig sind (nur Auswahlfragen).
    /// </summary>
    public static IReadOnlyList<Guid> RichtigeAntworten(QuizQuestion frage) =>
        frage.Options
            .Where(o => o.DeletedAt is null && o.Kind == QuizOptionKind.Answer && o.IsCorrect)
            .OrderBy(o => o.SortOrder)
            .Select(o => o.Id)
            .ToList();

    /// <summary>
    /// Was eine Frage höchstens bringt: bei Auswahlfragen 2 Punkte je richtiger
    /// Antwort (im Katalog ist es genau eine, also 2), bei einer Zuordnung 2.
    /// </summary>
    public static int HoechstPunkte(QuizQuestion frage) =>
        frage.Kind == QuizQuestionKind.Assignment
            ? PunkteJeAntwort
            : PunkteJeAntwort * RichtigeAntworten(frage).Count;

    /// <summary>
    /// Punkte einer Auswahlfrage: 2 je richtig angekreuzter Antwort, 2 Abzug je
    /// falsch angekreuzter. Eine Frage bringt aber nie weniger als 0 - ein
    /// Minus je Frage gibt es nicht, sonst könnte eine einzelne Fehlentscheidung
    /// mehr kosten, als die Frage wert war.
    /// </summary>
    /// <param name="richtige">Die richtigen Antworten der Frage.</param>
    /// <param name="angekreuzt">Was angekreuzt wurde (Doppelte zählen einmal).</param>
    public static int PunkteAuswahl(IReadOnlyCollection<Guid> richtige, IEnumerable<Guid> angekreuzt)
    {
        var gewaehlt = angekreuzt.Distinct().ToList();
        var richtigGekreuzt = gewaehlt.Count(id => richtige.Contains(id));
        var falschGekreuzt = gewaehlt.Count - richtigGekreuzt;
        return Math.Max(0, PunkteJeAntwort * (richtigGekreuzt - falschGekreuzt));
    }

    /// <summary>
    /// Punkte einer Zuordnung: 2, wenn ALLE Begriffe stimmen, sonst 0 - dieselbe
    /// Regel wie im Lernmodus ("nur ganz"). Ein fehlender Begriff ist falsch.
    /// </summary>
    public static int PunkteZuordnung(
        IEnumerable<QuizOption> begriffe, IReadOnlyDictionary<Guid, string>? zuordnung) =>
        begriffe.All(b => zuordnung is not null
                          && zuordnung.TryGetValue(b.Id, out var schluessel)
                          && string.Equals(schluessel?.Trim(), b.MatchKey, StringComparison.OrdinalIgnoreCase))
            ? PunkteJeAntwort
            : 0;

    /// <summary>
    /// Bestanden ab 70 % der möglichen Punkte. Ganzzahlig gerechnet
    /// (punkte * 100 &gt;= max * 70, gekürzt punkte * 10 &gt;= max * 7): an der
    /// Grenze darf keine Rundung entscheiden.
    /// </summary>
    public static bool Bestanden(int punkte, int hoechstpunkte) =>
        hoechstpunkte > 0 && (long)punkte * 100 >= (long)hoechstpunkte * BestehensProzent;

    /// <summary>Die kleinste Punktzahl, die noch besteht (35 von 50, 21 von 30).</summary>
    public static int Bestehensgrenze(int hoechstpunkte) =>
        (int)(((long)hoechstpunkte * BestehensProzent + 99) / 100);

    /// <summary>Ganzer Prozentwert, abgerundet - eine nicht bestandene Prüfung zeigt nie "70 %".</summary>
    public static int Prozent(int punkte, int hoechstpunkte) =>
        hoechstpunkte <= 0 ? 0 : (int)((long)punkte * 100 / hoechstpunkte);

    // ---------- Ablage der Komplexergebnisse ----------

    /// <summary>Die Komplexergebnisse als Text für die Spalte <see cref="QuizExamAttempt.SectionResultsJson"/>.</summary>
    public static string SchreibeAbschnitte(IReadOnlyList<QuizExamSectionResultDto> abschnitte) =>
        JsonSerializer.Serialize(abschnitte);

    public static IReadOnlyList<QuizExamSectionResultDto> LeseAbschnitte(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<QuizExamSectionResultDto>>(json) ?? [];
        }
        catch (JsonException)
        {
            // Ein unlesbarer Verlaufseintrag soll den Verlauf und die Auskunft
            // nicht sprengen - Punkte und Ergebnis stehen ohnehin in eigenen Spalten.
            return [];
        }
    }
}
