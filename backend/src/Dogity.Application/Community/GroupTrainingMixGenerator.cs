using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <summary>
/// Komponiert einen Termin-Inhalt (geordnete Bausteine) nach Kategorie-Regeln
/// aus dem Baustein-Pool eines Vereins (siehe docs/GROUP_TRAINING_SCHEDULE.md):
///  - Welpen: Ankommen → Entspannen → Futterhand → 1+ Übung(en) → Spielen.
///  - Junghunde/Basis: Leinenführigkeit zuerst + Zusatz aus Ablenkung/Ablage/
///    Hinterhand (wechselnd gemischt).
/// Reine, deterministische Funktion (Zufall über den übergebenen
/// <paramref name="rng"/>). Fehlt ein Fokus im Pool, wird der Slot einfach
/// übersprungen – das Ergebnis ist immer ein editierbarer Entwurf.
/// </summary>
internal static class GroupTrainingMixGenerator
{
    public static List<GroupTrainingExercise> Generate(GroupTrainingCategory category, IReadOnlyList<GroupTrainingExercise> pool, Random rng)
    {
        var available = pool.ToList();
        var result = new List<GroupTrainingExercise>();

        // Fokusse in Prioritätsreihenfolge: erst der primäre Fokus, nachfolgende
        // nur als Fallback, falls der vorige keinen Baustein hat.
        GroupTrainingExercise? PickOne(params string[] focuses)
        {
            foreach (var focus in focuses)
            {
                var matches = available.Where(e => FocusIs(e, focus)).ToList();
                if (matches.Count == 0) continue;
                var chosen = matches[rng.Next(matches.Count)];
                available.Remove(chosen);
                return chosen;
            }
            return null;
        }

        void PickMany(int count, Func<GroupTrainingExercise, bool> filter)
        {
            for (var i = 0; i < count; i++)
            {
                var matches = available.Where(filter).ToList();
                if (matches.Count == 0) break;
                var chosen = matches[rng.Next(matches.Count)];
                available.Remove(chosen);
                result.Add(chosen);
            }
        }

        void Add(GroupTrainingExercise? e) { if (e is not null) result.Add(e); }

        if (category == GroupTrainingCategory.Puppy)
        {
            Add(PickOne("Ankommen", "Sozialisierung"));
            Add(PickOne("Entspannung"));
            Add(PickOne("Futterhand", "Impulskontrolle"));
            // Ein bis zwei wechselnde Übungen (nicht aus den festen/schließenden Slots).
            PickMany(2, e => !FocusInAny(e, "Ankommen", "Sozialisierung", "Entspannung", "Futterhand", "Spielen"));
            Add(PickOne("Spielen"));
        }
        else // Junghunde, Basis
        {
            Add(PickOne("Leinenführigkeit", "Freifolge"));
            var before = result.Count;
            // Bevorzugt Ablenkung/Ablage/Hinterhand, dann mit Beliebigem auffüllen.
            PickMany(3, e => FocusInAny(e, "Ablenkung", "Ablage", "Hinterhandarbeit", "Hinterhand"));
            var picked = result.Count - before;
            if (picked < 3)
                PickMany(3 - picked, e => !FocusInAny(e, "Leinenführigkeit", "Freifolge"));
        }

        return result;
    }

    private static bool FocusIs(GroupTrainingExercise e, string focus) =>
        e.Focus is { } f && string.Equals(f.Trim(), focus, StringComparison.OrdinalIgnoreCase);

    private static bool FocusInAny(GroupTrainingExercise e, params string[] focuses) =>
        focuses.Any(f => FocusIs(e, f));
}
