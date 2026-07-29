using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <summary>
/// Fachlicher Best-Practice-Starterkatalog, den ein Vereinstrainer per Klick in
/// die eigene Vereins-Bibliothek übernehmen kann (siehe
/// docs/GROUP_TRAINING_LIBRARY.md). Kein Seed - der Katalog kommt nur auf
/// ausdrückliche Übernahme in einen Verein und ist danach frei editier-/
/// löschbar. Idempotent auf Titel-Ebene je Verein (erneutes Übernehmen ergänzt
/// nur Fehlendes, dupliziert nicht).
///
/// Inhalte sind eigene, an gängige Grundausbildungs-/Welpengruppen-Praxis
/// angelehnte Beschreibungen - keine Übernahme aus urheberrechtlich geschützten
/// Quellen. Junghunde- und Basis-Einheiten beginnen bewusst jeweils mit einer
/// anderen Leinenführigkeits-/Freifolge-Übung.
/// </summary>
internal static class GroupTrainingStarterCatalog
{
    internal sealed record ExerciseSpec(
        GroupTrainingCategory Category,
        string Title,
        string Focus,
        int DurationMinutes,
        string Description,
        GroupExamTarget Exams = GroupExamTarget.None);

    internal sealed record UnitSpec(
        GroupTrainingCategory Category,
        string Title,
        string Description,
        string[] ExerciseTitles);

    private const GroupTrainingCategory Welpen = GroupTrainingCategory.Puppy;
    private const GroupTrainingCategory Junghunde = GroupTrainingCategory.YoungDog;
    private const GroupTrainingCategory Basis = GroupTrainingCategory.Basis;

    internal static readonly ExerciseSpec[] Exercises =
    [
        // ---------------- Welpen ----------------
        new(Welpen, "Begrüßung & ruhiges Ankommen", "Ankommen", 8,
            "Welpen und Halter kommen an lockerer Leine an, wahren Abstand und lernen die Umgebung ruhig kennen. Keine wilde Begrüßung – der Trainer steuert Tempo und Distanz."),
        new(Welpen, "Positives Handling", "Handling", 8,
            "Pfoten, Ohren und Fang berühren, jede Berührung mit Leckerli verknüpfen. Grundlage für Tierarzt und Pflege."),
        new(Welpen, "Sozialkontakt in Kleingruppen", "Sozialisierung", 10,
            "Zwei bis drei passende Welpen spielen unter Aufsicht, mit bewussten Spielpausen durch den Trainer."),
        new(Welpen, "Untergründe & Umweltreize erkunden", "Umweltgewöhnung", 8,
            "Verschiedene Untergründe (Plane, Gitterrost, Wackelbrett) und leise Geräusche freiwillig erkunden – loben, nie zwingen."),
        new(Welpen, "Namensspiel & Aufmerksamkeit", "Bindung", 7,
            "Name sagen, Welpe schaut zum Halter, markern und belohnen – baut freiwilligen Blickkontakt auf."),
        new(Welpen, "Futter in der Hand – Impulskontrolle", "Impulskontrolle", 6,
            "Der Welpe lernt an der geschlossenen Hand mit Futter kurz zu warten; Belohnung erst beim Zurückweichen statt Drängeln."),
        new(Welpen, "Erste Hinterhandwahrnehmung", "Hinterhandarbeit", 6,
            "Über niedrige Cavaletti oder auf ein flaches Podest steigen – der Welpe nimmt spielerisch seine Hinterpfoten wahr."),
        new(Welpen, "Alltagstraining Welpe", "Alltag", 8,
            "Leine an-/ablegen, kurzes ruhiges Autotraining, entspannte Türsituation – Alltag positiv verankern."),
        new(Welpen, "Folgen an der Futterhand", "Futterhand", 6,
            "Der Welpe folgt der Futterhand ein paar Schritte – erster Aufbau von Aufmerksamkeit und Folgen in Bewegung."),
        new(Welpen, "Freies Spiel zum Abschluss", "Spielen", 8,
            "Kurzes, gut moderiertes Spiel unter passenden Welpen als positiver Ausklang der Stunde."),
        new(Welpen, "Ruhe & Entspannung", "Entspannung", 7,
            "Auf der Decke neben dem Halter herunterfahren – Ruhe in einer Reizumgebung lernen."),

        // ---------------- Junghunde ----------------
        new(Junghunde, "Leinenführigkeit – lockere Leine", "Leinenführigkeit", 10,
            "Gehen ohne Zug; bei Leinenspannung stehenbleiben, Belohnung in der Position neben dem Halter."),
        new(Junghunde, "Leinenführigkeit – Richtungs- & Tempowechsel", "Leinenführigkeit", 10,
            "Häufige Richtungs- und Tempowechsel; der Hund bleibt aufmerksam am Halter orientiert."),
        new(Junghunde, "Leinenführigkeit – an Ablenkung vorbei", "Leinenführigkeit", 10,
            "An aufgebauten Reizen (Futter, Spielzeug, Personen) ohne Zug vorbeigehen; Abstand nach Können wählen."),
        new(Junghunde, "Rückruf – Aufbau mittlere Distanz", "Rückruf", 10,
            "Distanz aufbauen und freudig abrufen, hochwertige Belohnung; Schleppleine als Absicherung."),
        new(Junghunde, "Sitz & Platz aus der Bewegung", "Grundsignale", 8,
            "Signale im Gehen geben und sauber ausführen lassen – Übergang von statisch zu dynamisch."),
        new(Junghunde, "Impulskontrolle am Wegrand", "Impulskontrolle", 8,
            "Futter/Spielzeug liegt am Boden; der Hund bleibt beim Halter statt sich zu bedienen."),
        new(Junghunde, "Hinterhandkontrolle – Pivot am Podest", "Hinterhandarbeit", 10,
            "Vorderpfoten auf niedrigem Podest, der Hund kreist mit der Hinterhand herum – Bewusstsein für die Hinterläufe."),
        new(Junghunde, "Rückwärts gehen", "Hinterhandarbeit", 6,
            "Wenige, saubere Schritte rückwärts fördern Körpergefühl und Koordination."),
        new(Junghunde, "Bleib mit Ablenkung", "Ablenkung", 8,
            "Dauer und Distanz steigern, während der Trainer vorbeigeht – Kriterien einzeln erhöhen."),
        new(Junghunde, "Ablage auf Distanz", "Ablage", 8,
            "Sauberes Abliegen und Halten auf ein Signal, auch mit etwas Distanz zum Halter."),
        new(Junghunde, "Alltagstraining Junghund", "Alltag", 8,
            "Warten an Bordstein/Tür, ruhiges Passieren von Hunden und Menschen, Begegnungen souverän meistern."),
        new(Junghunde, "Ruhe in der Gruppe", "Entspannung", 6,
            "Ablegen und Entspannen auf der Decke inmitten der Gruppensituation."),

        // ---------------- Basis (Richtung BH/IBGH) ----------------
        new(Basis, "Leinenführigkeit – Grundlage", "Leinenführigkeit", 10,
            "Korrekte Grundposition an lockerer Leine, aufmerksames Mitgehen, Anhalten mit Sitz.",
            GroupExamTarget.BH | GroupExamTarget.IBGH1),
        new(Basis, "Leinenführigkeit – Wendungen & Kehrtwende", "Leinenführigkeit", 10,
            "Rechts-, Links- und Kehrtwendung sauber ausführen; der Hund hält die Position.",
            GroupExamTarget.BH | GroupExamTarget.IBGH1),
        new(Basis, "Freifolge – ohne Leine Grundlage", "Freifolge", 10,
            "Wie Leinenführigkeit, aber ohne Leine – Aufmerksamkeit und Position halten.",
            GroupExamTarget.BH | GroupExamTarget.IBGH1 | GroupExamTarget.IBGH2),
        new(Basis, "Freifolge – mit Tempowechsel", "Freifolge", 10,
            "Normal-, Lauf- und Langsamschritt in der Freifolge; präzise Übergänge, Hund bleibt in Position.",
            GroupExamTarget.IBGH1 | GroupExamTarget.IBGH2 | GroupExamTarget.IBGH3),
        new(Basis, "Sitz / Platz / Steh aus der Bewegung", "Grundsignale", 10,
            "Positionen aus der Bewegung auf ein Hörzeichen, Halter geht weiter – sauberes, schnelles Einnehmen.",
            GroupExamTarget.BH | GroupExamTarget.IBGH1),
        new(Basis, "Ablegen unter Ablenkung", "Ablage", 10,
            "Abliegen und Bleiben, während ein anderer Hund arbeitet – Ruhe und Zuverlässigkeit aufbauen.",
            GroupExamTarget.BH | GroupExamTarget.IBGH1 | GroupExamTarget.IBGH2),
        new(Basis, "Ablenkung an Reizen", "Ablenkung", 8,
            "Neutral bleiben an aufgebauten Reizen (Futter, Spielzeug, Personen) – Grundlage für Prüfungssicherheit.",
            GroupExamTarget.BH | GroupExamTarget.IBGH1),
        new(Basis, "Voraussenden mit Platz", "Fortgeschritten", 10,
            "Der Hund läuft auf Hörzeichen geradlinig voraus und legt sich auf Signal ab.",
            GroupExamTarget.IBGH2 | GroupExamTarget.IBGH3),
        new(Basis, "Fußarbeit mit Tempowechsel", "Freifolge", 8,
            "Fußarbeit mit sauberen Tempowechseln und Aufmerksamkeit – Feinschliff für die Prüfung.",
            GroupExamTarget.IBGH1 | GroupExamTarget.IBGH2 | GroupExamTarget.IBGH3),
        new(Basis, "Hinterhandarbeit – Präzision für Wendungen", "Hinterhandarbeit", 8,
            "Pivot und Hinterhandkontrolle für exakte Wendungen und sauberes Angrundsitzen."),
        new(Basis, "Alltagstraining & Umweltsicherheit", "Alltag", 8,
            "Durch eine Personengruppe gehen, Geräusche und Verkehr neutral erleben – Alltags- und BH-Teil-B-Nähe.",
            GroupExamTarget.BH),
        new(Basis, "Ablegen am Rand – Ruhe", "Entspannung", 6,
            "Ruhiges Ablegen am Platzrand, während andere arbeiten – Entspannung trotz Aktivität."),
    ];

    internal static readonly UnitSpec[] Units =
    [
        new(Welpen, "Welpen – Ankommen & Sozialisierung",
            "Erste Gruppenstunde: ruhiges Ankommen, positive Sozialkontakte, Handling und Aufmerksamkeit.",
            ["Begrüßung & ruhiges Ankommen", "Positives Handling", "Sozialkontakt in Kleingruppen", "Untergründe & Umweltreize erkunden", "Namensspiel & Aufmerksamkeit", "Ruhe & Entspannung"]),
        new(Welpen, "Welpen – Impulskontrolle & Alltag",
            "Impulskontrolle-Basics, erste Hinterhandwahrnehmung und Alltagstraining.",
            ["Namensspiel & Aufmerksamkeit", "Futter in der Hand – Impulskontrolle", "Erste Hinterhandwahrnehmung", "Alltagstraining Welpe", "Sozialkontakt in Kleingruppen", "Ruhe & Entspannung"]),

        new(Junghunde, "Junghunde – Einheit 1: Leinenführigkeit & Grundsignale",
            "Start mit lockerer Leinenführigkeit, dann Grundsignale, Impulskontrolle und Rückruf.",
            ["Leinenführigkeit – lockere Leine", "Sitz & Platz aus der Bewegung", "Impulskontrolle am Wegrand", "Rückruf – Aufbau mittlere Distanz", "Ruhe in der Gruppe"]),
        new(Junghunde, "Junghunde – Einheit 2: Körpergefühl & Hinterhand",
            "Start mit Richtungs-/Tempowechsel an der Leine, dann Hinterhandarbeit und Alltag.",
            ["Leinenführigkeit – Richtungs- & Tempowechsel", "Hinterhandkontrolle – Pivot am Podest", "Rückwärts gehen", "Alltagstraining Junghund", "Ruhe in der Gruppe"]),
        new(Junghunde, "Junghunde – Einheit 3: Ablenkung & Alltag",
            "Start mit Leinenführigkeit an Ablenkung, dann Bleib, Rückruf und Alltag.",
            ["Leinenführigkeit – an Ablenkung vorbei", "Bleib mit Ablenkung", "Rückruf – Aufbau mittlere Distanz", "Alltagstraining Junghund", "Ruhe in der Gruppe"]),

        new(Basis, "Basis – Einheit 1 (Richtung BH)",
            "Start mit Leinenführigkeit-Grundlage, Positionen aus der Bewegung, Ablegen unter Ablenkung und Alltag/Umweltsicherheit.",
            ["Leinenführigkeit – Grundlage", "Sitz / Platz / Steh aus der Bewegung", "Ablegen unter Ablenkung", "Alltagstraining & Umweltsicherheit", "Ablegen am Rand – Ruhe"]),
        new(Basis, "Basis – Einheit 2 (Richtung IBGH)",
            "Start mit Leinenführigkeit-Wendungen, dann Freifolge, Fußarbeit und Hinterhand-Präzision.",
            ["Leinenführigkeit – Wendungen & Kehrtwende", "Freifolge – ohne Leine Grundlage", "Fußarbeit mit Tempowechsel", "Hinterhandarbeit – Präzision für Wendungen", "Ablegen am Rand – Ruhe"]),
    ];
}
