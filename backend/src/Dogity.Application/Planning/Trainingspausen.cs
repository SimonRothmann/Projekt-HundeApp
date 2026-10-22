namespace Dogity.Application.Planning;

/// <summary>Ein Pausenzeitraum für die Planrechnung; beide Grenzen einschließlich, ohne Ende = "bis auf Weiteres".</summary>
public readonly record struct PausenZeitraum(DateOnly Start, DateOnly? Ende);

/// <summary>
/// Wie eine Trainingspause den Trainingsplan anhält (siehe
/// docs/SMART_TRAINING_PLAN.md, Abschnitt "Pausen"). Reine Funktionen, damit
/// die Regel ohne Datenbank prüfbar ist; das Frontend rechnet dieselbe Regel in
/// <c>lib/trainingsplan.ts</c> nach.
///
/// Die Regel: Die Planwoche zählt nur die Tage, an denen der Hund NICHT
/// pausiert. Planwoche = 1 + (Tage seit Planbeginn - Pausentage) / 7, ganze
/// Tage, abgerundet. Daraus folgt:
/// - Während einer Pause steht die Woche still; nichts gilt als versäumt, weil
///   es keine verstreichende Planzeit gibt.
/// - Nach der Pause geht es dort weiter, wo der Plan stand - auch mitten in
///   der Woche (Tag 3 von Woche 4 bleibt Tag 3 von Woche 4). Eine Rundung auf
///   ganze Pausenwochen entfällt bewusst: Sie ließe bei einer Pause von neun
///   Tagen entweder zwei Tage Plan verfallen oder erfände zwei hinzu.
/// - Pausen vor dem Planbeginn und nach dem betrachteten Tag haben keine
///   Wirkung.
/// </summary>
public static class Trainingspausen
{
    /// <summary>Wie viele Tage von <paramref name="von"/> (einschließlich) bis <paramref name="bis"/> (ausschließlich) pausiert wird.</summary>
    public static int PausentageZwischen(IEnumerable<PausenZeitraum> pausen, DateOnly von, DateOnly bis)
    {
        if (bis <= von) return 0;
        var letzter = bis.AddDays(-1);

        // Auf das Fenster zuschneiden und zusammenfassen: Überschneidungen
        // gibt es im Betrieb nicht (der Dienst lehnt sie ab), aber eine Zählung
        // darf auch bei Altbeständen oder Randfällen keinen Tag doppelt nehmen.
        var stuecke = pausen
            .Select(p => (Start: p.Start < von ? von : p.Start, Ende: p.Ende is { } e && e < letzter ? e : letzter))
            .Where(s => s.Start <= s.Ende)
            .OrderBy(s => s.Start)
            .ToList();

        var summe = 0;
        DateOnly? bisher = null;
        foreach (var (start, ende) in stuecke)
        {
            var ab = bisher is { } b && start <= b ? b.AddDays(1) : start;
            if (ab <= ende) summe += ende.DayNumber - ab.DayNumber + 1;
            if (bisher is null || ende > bisher) bisher = ende;
        }
        return summe;
    }

    /// <summary>Die Planwoche (ab 1) an <paramref name="heute"/>; vor dem Planbeginn die erste.</summary>
    public static int Planwoche(DateOnly planBeginn, DateOnly heute, IEnumerable<PausenZeitraum> pausen)
    {
        var vergangen = heute.DayNumber - planBeginn.DayNumber;
        if (vergangen <= 0) return 1;
        var aktiv = vergangen - PausentageZwischen(pausen, planBeginn, heute);
        return Math.Max(0, aktiv) / 7 + 1;
    }

    /// <summary>Ob der Hund an diesem Tag pausiert.</summary>
    public static bool Pausiert(IEnumerable<PausenZeitraum> pausen, DateOnly tag) =>
        pausen.Any(p => p.Start <= tag && (p.Ende is null || tag <= p.Ende));

    /// <summary>
    /// Ob die Planzeit bis zum Zieldatum wegen der Pausen nicht mehr reicht:
    /// Am Zieldatum hätte der Plan dann seine letzte Woche noch nicht erreicht.
    ///
    /// Es zählen nur Pausen, die den Plan wirklich anhalten. Eine Pause "bis
    /// auf Weiteres" gilt dabei nur bis heute - ihr Ende ist nicht bekannt, und
    /// ein Hinweis, der von der ersten Stunde an mahnt, hülfe niemandem. Er
    /// kommt, sobald die verstrichene Zeit (oder ein eingetragenes Ende) nicht
    /// mehr reicht.
    /// Ohne wirksame Pause gibt es nie einen Hinweis.
    /// </summary>
    public static bool ZieldatumZuKnapp(
        DateOnly planBeginn, DateOnly heute, DateOnly zieldatum, int letztePlanwoche, IEnumerable<PausenZeitraum> pausen)
    {
        var bekannt = Bekannte(pausen, heute);
        if (PausentageZwischen(bekannt, planBeginn, zieldatum) == 0) return false;
        return Planwoche(planBeginn, zieldatum, bekannt) < letztePlanwoche;
    }

    /// <summary>
    /// Die Pausen, mit denen man vorausplant: eine Pause "bis auf Weiteres"
    /// gilt bis heute (bzw. ihren Beginn, falls der noch bevorsteht), alle
    /// anderen unverändert.
    /// </summary>
    public static List<PausenZeitraum> Bekannte(IEnumerable<PausenZeitraum> pausen, DateOnly heute) =>
        pausen
            .Select(p => p.Ende is null ? new PausenZeitraum(p.Start, p.Start > heute ? p.Start : heute) : p)
            .ToList();

    /// <summary>
    /// Eine Fälligkeit (Wiedervorlage einer Übung), um die Pausentage seither
    /// nach hinten geschoben: Nach drei Wochen Verletzung ist nicht jede Übung
    /// "drei Wochen überfällig" - die Planzeit stand still.
    /// </summary>
    public static DateOnly FaelligkeitNachPausen(DateOnly faellig, DateOnly heute, IEnumerable<PausenZeitraum> pausen) =>
        faellig.AddDays(PausentageZwischen(pausen, faellig, heute));
}
