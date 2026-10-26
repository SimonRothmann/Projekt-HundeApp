using Dogity.Domain.Planning;

namespace Dogity.Application.Planning;

/// <summary>Wie sicher eine Übung (oder die Theorie) sitzt.</summary>
public enum ReifeZustand
{
    /// <summary>Noch nie trainiert (bei der Theorie: noch keine Übungsprüfung).</summary>
    Offen,

    /// <summary>Trainiert, aber (noch) nicht sicher.</summary>
    Wackelig,

    /// <summary>Gefestigt, bewertet und nicht zu lange her.</summary>
    Sicher
}

/// <summary>Warum eine trainierte Übung noch wackelt - damit die Oberfläche es in Worte fassen kann.</summary>
public enum ReifeGrund
{
    /// <summary>Noch nicht oft genug gut gelungen (Leitner-Box zu niedrig).</summary>
    SeltenTrainiert,

    /// <summary>Die Bewertungen liegen im Schnitt unter der Grenze.</summary>
    SchwacheBewertung,

    /// <summary>War sicher, das letzte Training liegt aber zu lange zurück ("eingerostet").</summary>
    LangeHer
}

/// <summary>Die Gesamtampel der Prüfungsreife.</summary>
public enum Reifeampel
{
    Gruen,
    Gelb,
    Rot
}

/// <summary>
/// Eine Übung der Prüfungsordnung mit dem Beherrschungs-Stand des Hundes - die
/// Eingabe der Regel. Ohne Mastery-Zeile (nie trainiert) ist
/// <paramref name="SessionCount"/> 0.
/// </summary>
public readonly record struct ReifeUebung(
    Guid ExerciseId,
    string Name,
    string? NameEn,
    bool IsMandatory,
    int MaxPoints,
    int SessionCount,
    int Box,
    double RecentAvgRating,
    DateOnly? LastTrainedOn);

/// <summary>Eine Übung in der Auswertung der Prüfungsreife.</summary>
/// <param name="Grund">Nur bei <see cref="ReifeZustand.Wackelig"/>.</param>
/// <param name="TageSeit">Kalendertage seit dem letzten Training; null, wenn nie trainiert.</param>
/// <param name="PausenTage">Davon Tage, an denen der Hund pausierte (zählen für "sicher" nicht mit).</param>
/// <param name="Bewertung">Durchschnittsbewertung (1-5), auf eine Stelle abgerundet; null, wenn nie trainiert.</param>
public record PruefungsreifeUebungDto(
    Guid ExerciseId,
    string Name,
    string? NameEn,
    bool IsMandatory,
    int MaxPoints,
    ReifeZustand Zustand,
    ReifeGrund? Grund,
    int? TageSeit,
    int PausenTage,
    double? Bewertung);

/// <summary>
/// Die Theoriezeile der Prüfungsreife (Sachkunde bei der BH/VT) - nur für die
/// ansehende Person selbst, nie für betreuende Trainer:innen.
/// </summary>
/// <param name="CatalogCode">Der Katalog, zu dem der Link führt: der der letzten Übungsprüfung, sonst der Erwachsenenkatalog.</param>
/// <param name="Gewertet">Wie viele der letzten Übungsprüfungen in die Bewertung eingehen (0 bis 2).</param>
/// <param name="Bestanden">Davon bestanden.</param>
/// <param name="InFehlerspeicher">Falsch beantwortete Fragen, die noch gelernt werden wollen.</param>
public record TheorieZeileDto(ReifeZustand Zustand, string CatalogCode, int Gewertet, int Bestanden, int InFehlerspeicher);

/// <summary>
/// Die ausführliche Prüfungsreife eines Ziels. <see cref="Uebungen"/> steht
/// sortiert: erst alles, was noch fehlt (offen und wackelig, nach Gewicht
/// absteigend), dann das Sichere.
/// </summary>
/// <param name="Wochen">Trainingswochen bis zum Zieldatum (aufgerundet, ohne die Pausentage).</param>
/// <param name="Tage">Kalendertage bis zum Zieldatum (0 am Prüfungstag oder danach).</param>
/// <param name="PausenTage">Pausentage zwischen heute und dem Zieldatum, die nicht als Trainingszeit zählen.</param>
/// <param name="ZieldatumVerschieben">Rot und nur noch wenig Zeit: Die Oberfläche zeigt den Hinweis "Zieldatum verschieben?".</param>
/// <param name="Pausiert">Der Hund pausiert heute.</param>
public record PruefungsreifeDto(
    Reifeampel Ampel,
    int Sicher,
    int Wackelig,
    int Offen,
    int Wochen,
    int Tage,
    int PausenTage,
    bool ZieldatumVerschieben,
    bool Pausiert,
    IReadOnlyList<PruefungsreifeUebungDto> Uebungen,
    TheorieZeileDto? Theorie);

/// <summary>
/// Die Kurzfassung für die Zielkarten: Ampel und Zahlen, dazu ob der Hinweis
/// "Zieldatum verschieben?" fällig ist (die Karte zeigt ihn, ohne die ganze
/// Auswertung zu laden).
/// </summary>
public record PruefungsreifeKurzDto(Reifeampel Ampel, int Offen, int Wackelig, int Sicher, bool ZieldatumVerschieben = false);

/// <summary>
/// Prüfungsreife je Ziel: Wie weit ist der Hund bis zur Prüfung? Eine
/// Faustregel aus dem Trainingstagebuch, kein Urteil der Richter:in. Reine
/// Funktionen, damit die Regel ohne Datenbank prüfbar ist (siehe
/// docs/SMART_TRAINING_PLAN.md, Abschnitt "Prüfungsreife").
///
/// Berechnet, nicht gespeichert: alles folgt aus dem Mastery-Stand der Übungen
/// (siehe <see cref="ExerciseMastery"/>), den Pausen des Hundes und dem Zieldatum.
/// </summary>
public static class Pruefungsreife
{
    // ---------- Schwellen ----------

    /// <summary>
    /// Ab dieser Leitner-Box gilt eine Übung als gefestigt. Die Box steigt nur bei
    /// "Erfolg und Bewertung 4 oder 5" um eins, sinkt bei Misserfolg oder Bewertung
    /// bis 2 um eins (mittlere Einheiten ändern nichts), und jeder Start liegt in
    /// Box 1: Box 4 heißt also netto drei Stufen - drei gute Einheiten mehr als
    /// schwache, nicht drei am Stück. Ein einzelner guter Tag macht eine Übung
    /// jedenfalls nicht sicher.
    /// </summary>
    public const int SicherAbBox = 4;

    /// <summary>
    /// Ab diesem gewichteten Schnitt der jüngsten Bewertungen (1-5, siehe
    /// <see cref="ExerciseMastery.RecentAvgRating"/>) gilt eine Übung als gut.
    /// 4 ist im Bewertungsschema "gut"; die Box allein würde eine Übung, die
    /// zuletzt eher mittelmäßig lief, noch sicher nennen.
    /// </summary>
    public const double SicherAbBewertung = 4.0;

    /// <summary>
    /// So viele Tage (ohne Pausentage) darf das letzte Training höchstens
    /// zurückliegen. 28 Tage ist das längste Wiedervorlage-Intervall des
    /// Planers (Box 5): Wer eine Übung länger nicht angefasst hat, als der Plan
    /// sie je ruhen lässt, hat sie nicht mehr "sicher". Pausentage zählen nicht
    /// mit - wer verletzt war, verliert nicht automatisch seine sicheren Übungen.
    /// </summary>
    public const int SicherMaxTage = 28;

    /// <summary>Grün ab diesem Anteil (in Prozent) des Gewichts, der sicher ist - und ohne offene Pflichtübung.</summary>
    public const int GruenAbProzent = 80;

    /// <summary>Rot unter diesem Anteil (in Prozent) des Gewichts, der sicher ist - oder mit offener Pflichtübung.</summary>
    public const int RotUnterProzent = 50;

    /// <summary>Rot und höchstens so viele Trainingswochen übrig: "Zieldatum verschieben?".</summary>
    public const int VerschiebenBisWochen = 3;

    /// <summary>Wie viele der letzten Übungsprüfungen die Theorie bewerten: "sicher" heißt, alle bestanden.</summary>
    public const int TheorieBilanzLaenge = 2;

    /// <summary>
    /// Rundungstoleranz für den Bewertungsschnitt: Er ist ein gleitender
    /// Mittelwert in Fließkommazahlen, "4,0" soll nicht an der 16. Stelle scheitern.
    /// </summary>
    private const double Toleranz = 1e-9;

    // ---------- Wann es eine Prüfungsreife gibt ----------

    /// <summary>
    /// Nur laufende Ziele mit Prüfungsordnung: Ein eigenes Ziel ohne Ordnung hat
    /// keinen Maßstab, und erreichte oder beendete Ziele sind kein Thema mehr.
    /// </summary>
    public static bool Gilt(Goal ziel) =>
        ziel.Status == GoalStatus.Active && !ziel.IsCustom && ziel.RegulationId is not null;

    // ---------- Zustand einer Übung ----------

    /// <summary>
    /// Der Zustand einer Übung am Tag <paramref name="heute"/>.
    /// <list type="bullet">
    /// <item><b>offen</b>: nie trainiert.</item>
    /// <item><b>sicher</b>: Box ab <see cref="SicherAbBox"/> UND Bewertung ab <see cref="SicherAbBewertung"/> UND
    /// zuletzt vor höchstens <see cref="SicherMaxTage"/> Tagen trainiert (Pausentage nicht mitgezählt).</item>
    /// <item><b>wackelig</b>: alles dazwischen.</item>
    /// </list>
    /// </summary>
    public static PruefungsreifeUebungDto Bewerte(ReifeUebung uebung, DateOnly heute, IEnumerable<PausenZeitraum> pausen)
    {
        if (uebung.SessionCount <= 0)
            return new PruefungsreifeUebungDto(
                uebung.ExerciseId, uebung.Name, uebung.NameEn, uebung.IsMandatory, uebung.MaxPoints,
                ReifeZustand.Offen, null, null, 0, null);

        int? tageSeit = null;
        var pausenTage = 0;
        if (uebung.LastTrainedOn is { } zuletzt)
        {
            tageSeit = Math.Max(0, heute.DayNumber - zuletzt.DayNumber);
            pausenTage = Trainingspausen.PausentageZwischen(pausen, zuletzt, heute);
        }
        var aktiveTage = (tageSeit ?? 0) - pausenTage;

        var bewertungOk = uebung.RecentAvgRating + Toleranz >= SicherAbBewertung;
        var boxOk = uebung.Box >= SicherAbBox;
        var frisch = aktiveTage <= SicherMaxTage;

        ReifeZustand zustand;
        ReifeGrund? grund = null;
        if (bewertungOk && boxOk && frisch)
        {
            zustand = ReifeZustand.Sicher;
        }
        else
        {
            zustand = ReifeZustand.Wackelig;
            // Die aufschlussreichste Ursache zuerst: schlechte Bewertungen, dann die
            // noch fehlende Beständigkeit; "lange her" nur bei einer sonst
            // gefestigten Übung (früher sicher, jetzt eingerostet).
            grund = !bewertungOk ? ReifeGrund.SchwacheBewertung
                : !boxOk ? ReifeGrund.SeltenTrainiert
                : ReifeGrund.LangeHer;
        }

        return new PruefungsreifeUebungDto(
            uebung.ExerciseId, uebung.Name, uebung.NameEn, uebung.IsMandatory, uebung.MaxPoints,
            zustand, grund, tageSeit, pausenTage, Math.Floor(uebung.RecentAvgRating * 10 + Toleranz) / 10);
    }

    // ---------- Gewicht ----------

    /// <summary>
    /// Das Gewicht jeder Übung in der Ampel, in halben Einheiten (damit die
    /// Prozentgrenzen ganzzahlig und exakt bleiben).
    ///
    /// - Grundgewicht sind die Höchstpunkte. Ordnungen ganz ohne Punkte (Agility,
    ///   Hoopers, Rally Obedience) zählen jede Übung gleich.
    /// - Ohne Punkte in einer Ordnung MIT Punkten (BH: Teil B und Freifolge sind
    ///   Gesamteindruck bzw. unbewertet, aber Pflicht) bekommt die Übung das Gewicht
    ///   der kleinsten bewerteten: Sie ist trainierbar und Teil der Prüfung, darf
    ///   aber nicht unsichtbar sein, nur weil die Ordnung sie nicht mit Punkten bewertet.
    /// - Wahlübungen (<see cref="ReifeUebung.IsMandatory"/> = false) zählen mit
    ///   halbem Gewicht. Im Katalog heißt "keine Pflicht" freiwillig bzw. "kann im
    ///   Parcours vorkommen" (ÖKV-Freifolge, Mehrschildübung der Rally Obedience):
    ///   Man sollte sie können, die Prüfung hängt aber nicht an ihnen. Ganz weglassen
    ///   wäre falsch, sie sollen in der Liste "was noch fehlt" auftauchen.
    ///   Sie lösen nie "rot" durch Offenheit aus (das tun nur Pflichtübungen).
    /// </summary>
    public static int[] Gewichte(IReadOnlyList<ReifeUebung> uebungen)
    {
        var kleinsterPunkt = uebungen.Where(u => u.MaxPoints > 0).Select(u => u.MaxPoints).DefaultIfEmpty(0).Min();
        return uebungen
            .Select(u =>
            {
                var grund = kleinsterPunkt == 0 ? 1 : u.MaxPoints > 0 ? u.MaxPoints : kleinsterPunkt;
                return u.IsMandatory ? grund * 2 : grund;
            })
            .ToArray();
    }

    // ---------- Gesamtampel ----------

    /// <summary>
    /// Die Prüfungsreife eines Ziels; null, wenn die Ordnung keine Übungen hat.
    ///
    /// - <b>Rot</b>: eine Pflichtübung ist offen ODER weniger als <see cref="RotUnterProzent"/> Prozent des Gewichts sicher.
    /// - <b>Grün</b>: keine Pflichtübung offen, mindestens <see cref="GruenAbProzent"/> Prozent des Gewichts sicher und -
    ///   falls die Theorie gefordert ist (<paramref name="theorie"/> gesetzt) - die Theorie sicher.
    /// - <b>Gelb</b>: sonst.
    /// </summary>
    /// <param name="theorie">Die Theoriezeile der ansehenden Person; null für Trainer:innen und Ordnungen ohne Theorie.</param>
    public static PruefungsreifeDto? Berechne(
        IReadOnlyList<ReifeUebung> uebungen,
        DateOnly heute,
        DateOnly zieldatum,
        IReadOnlyList<PausenZeitraum> pausen,
        TheorieZeileDto? theorie = null)
    {
        if (uebungen.Count == 0) return null;

        var bewertet = uebungen.Select(u => Bewerte(u, heute, pausen)).ToList();
        var gewichte = Gewichte(uebungen);

        long gesamt = gewichte.Sum(g => (long)g);
        long sicher = 0;
        var pflichtOffen = false;
        for (var i = 0; i < bewertet.Count; i++)
        {
            if (bewertet[i].Zustand == ReifeZustand.Sicher) sicher += gewichte[i];
            if (bewertet[i].IsMandatory && bewertet[i].Zustand == ReifeZustand.Offen) pflichtOffen = true;
        }

        Reifeampel ampel;
        if (pflichtOffen || sicher * 100 < (long)RotUnterProzent * gesamt)
            ampel = Reifeampel.Rot;
        else if (sicher * 100 >= (long)GruenAbProzent * gesamt && (theorie is null || theorie.Zustand == ReifeZustand.Sicher))
            ampel = Reifeampel.Gruen;
        else
            ampel = Reifeampel.Gelb;

        var (tage, pausenTage, wochen) = ZeitBis(heute, zieldatum, pausen);

        // Fehlendes zuerst, nach Gewicht absteigend; bei gleichem Gewicht Offenes vor
        // Wackeligem, dann nach Name - eine feste Reihenfolge, damit nichts flackert.
        var sortiert = bewertet
            .Select((u, i) => (Uebung: u, Gewicht: gewichte[i]))
            .OrderBy(x => x.Uebung.Zustand == ReifeZustand.Sicher ? 1 : 0)
            .ThenByDescending(x => x.Gewicht)
            .ThenBy(x => x.Uebung.Zustand)
            .ThenBy(x => x.Uebung.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Uebung)
            .ToList();

        return new PruefungsreifeDto(
            ampel,
            bewertet.Count(u => u.Zustand == ReifeZustand.Sicher),
            bewertet.Count(u => u.Zustand == ReifeZustand.Wackelig),
            bewertet.Count(u => u.Zustand == ReifeZustand.Offen),
            wochen,
            tage,
            pausenTage,
            ampel == Reifeampel.Rot && tage > 0 && wochen <= VerschiebenBisWochen,
            Trainingspausen.Pausiert(pausen, heute),
            sortiert,
            theorie);
    }

    /// <summary>
    /// Die Zeit bis zur Prüfung: Kalendertage, davon Pausentage, und die
    /// verbleibenden Trainingswochen (aufgerundet). Wie bei der Planwoche
    /// (<see cref="Trainingspausen"/>) zählen nur Tage ohne Pause; eine Pause
    /// "bis auf Weiteres" gilt dabei bis heute, ihr weiteres Ende ist unbekannt.
    /// </summary>
    public static (int Tage, int PausenTage, int Wochen) ZeitBis(DateOnly heute, DateOnly zieldatum, IEnumerable<PausenZeitraum> pausen)
    {
        var tage = Math.Max(0, zieldatum.DayNumber - heute.DayNumber);
        if (tage == 0) return (0, 0, 0);

        var pausenTage = Math.Min(tage, Trainingspausen.PausentageZwischen(Trainingspausen.Bekannte(pausen, heute), heute, zieldatum));
        var trainingstage = tage - pausenTage;
        return (tage, pausenTage, (trainingstage + 6) / 7);
    }

    /// <summary>Die Kurzfassung für die Zielkarte.</summary>
    public static PruefungsreifeKurzDto Kurz(PruefungsreifeDto reife) =>
        new(reife.Ampel, reife.Offen, reife.Wackelig, reife.Sicher, reife.ZieldatumVerschieben);

    // ---------- Theorie ----------

    /// <summary>
    /// Der Theoriestand aus den letzten Übungsprüfungen (neueste zuerst):
    /// <b>sicher</b>, wenn die letzten <see cref="TheorieBilanzLaenge"/> bestanden sind; <b>wackelig</b>, wenn
    /// mindestens eine abgelegt ist, aber nicht die letzten beiden bestanden; <b>offen</b> ohne Übungsprüfung.
    /// </summary>
    public static ReifeZustand TheorieZustand(IReadOnlyList<bool> letzteBestanden)
    {
        if (letzteBestanden.Count == 0) return ReifeZustand.Offen;
        var gewertet = letzteBestanden.Take(TheorieBilanzLaenge).ToList();
        return gewertet.Count == TheorieBilanzLaenge && gewertet.All(b => b)
            ? ReifeZustand.Sicher
            : ReifeZustand.Wackelig;
    }
}
