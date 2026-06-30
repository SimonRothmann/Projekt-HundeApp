using Dogity.Domain.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Planning;

/// <summary>
/// Eine Übung als Kandidat für die Planerstellung. IsMandatory kommt aus
/// RegulationExercise, wenn das Ziel eine konkrete Prüfungsordnung gewählt
/// hat (siehe GoalService.CreateAsync) - ohne gewählte Prüfungsordnung sind
/// alle Übungen der Sportart als Pflicht markiert (Fallback-Verhalten für
/// Sportarten ohne hinterlegte Prüfungsordnung).
/// </summary>
public readonly record struct PlanExerciseCandidate(Guid ExerciseId, string Name, ExerciseDifficulty Difficulty, bool IsMandatory);

/// <summary>
/// Regelbasierter, deterministischer Generator für Wochenpläne (siehe
/// DATABASE.md "training_plans"). Bewusst kein KI/ML-Ansatz - echte
/// Trainingsanalyse ist laut PRODUCT_REQUIREMENTS.md "Nicht-MVP".
///
/// Statt blind über alle Übungen einer Sportart zu rotieren (das mischte
/// z.B. bei "Fährte" alle drei Prüfungsstufen A/B/C durcheinander, da
/// mehrere Prüfungsordnungen dieselbe Sportart teilen können), wird der
/// Plan jetzt aus den Pflichtübungen der gewählten Prüfungsordnung
/// (RegulationExercise.IsMandatory) erzeugt und stellt sicher, dass JEDE
/// Pflichtübung vor dem Zieldatum mindestens einmal vorkommt - das Pensum
/// pro Woche orientiert sich an einem realistischen Trainingsrhythmus
/// (mind. 4 Übungen/Woche, ca. 2 Einheiten à 2 Übungen) und wird nur über
/// diesen Richtwert hinaus erhöht (bis max. 6), wenn sonst nicht alle
/// Pflichtübungen in der verfügbaren Zeit untergebracht werden könnten.
/// Reicht die Zeit für mehr als eine Runde, wiederholen sich die
/// Pflichtübungen zyklisch.
/// </summary>
public static class TrainingPlanGenerator
{
    private const int MaxWeeks = 12;

    // Realistischer Trainingsrhythmus laut Nutzer-Feedback: im Schnitt 2
    // Einheiten pro Woche mit je 2 Übungen = 4 Übungen/Woche als Richtwert,
    // nicht nur "genug, um den Pflichtkatalog einmal abzudecken". MaxItemsPerWeek
    // bleibt als Sicherheitsnetz etwas darüber, damit ein großer
    // Pflichtkatalog bei kurzer Frist trotzdem vollständig vor dem
    // Zieldatum untergebracht werden kann.
    private const int MinItemsPerWeek = 4;
    private const int MaxItemsPerWeek = 6;

    private static int RepetitionsFor(ExerciseDifficulty difficulty) => difficulty switch
    {
        ExerciseDifficulty.Beginner => 3,
        ExerciseDifficulty.Intermediate => 2,
        ExerciseDifficulty.Advanced => 1,
        _ => 2
    };

    public static List<TrainingPlanItem> Generate(DateOnly today, DateOnly targetDate, IReadOnlyList<PlanExerciseCandidate> candidates)
    {
        var daysUntilTarget = Math.Max(1, targetDate.DayNumber - today.DayNumber);
        var weeks = Math.Clamp((int)Math.Ceiling(daysUntilTarget / 7.0), 1, MaxWeeks);

        // Nur Pflichtübungen einplanen, solange welche markiert sind - Kür-
        // Übungen sind beim Ziel "Prüfung bestehen" zweitrangig. Sind gar
        // keine als Pflicht markiert (z.B. kein RegulationExercise-Bezug),
        // wird der gesamte Kandidatenpool verwendet, damit der Plan nicht
        // leer bleibt.
        var pool = candidates.Where(c => c.IsMandatory).ToList();
        if (pool.Count == 0) pool = candidates.ToList();

        var orderedPool = pool
            .OrderBy(c => c.Difficulty)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        var nonRestWeeks = Enumerable.Range(1, weeks).Count(w => w % 4 != 0);

        var items = new List<TrainingPlanItem>();
        if (orderedPool.Count == 0 || nonRestWeeks == 0)
        {
            for (var week = 1; week <= weeks; week++)
                items.Add(new TrainingPlanItem { WeekNumber = week, IsRestWeek = true });
            return items;
        }

        // Mindestens MinItemsPerWeek (realistischer Trainingsrhythmus), mehr
        // nur falls nötig, um den gesamten Pflichtkatalog bis zum Zieldatum
        // unterzubringen - aber nie mehr als MaxItemsPerWeek (sonst wird eine
        // einzelne Woche unrealistisch überladen) und nie mehr, als es
        // überhaupt verschiedene Übungen gibt (sonst käme dieselbe Übung
        // zweimal in derselben Woche vor - bei einem kleineren Pflichtkatalog
        // wird stattdessen jede Übung über RepetitionsTarget öfter trainiert).
        var itemsPerWeek = Math.Clamp(
            Math.Max(MinItemsPerWeek, (int)Math.Ceiling(orderedPool.Count / (double)nonRestWeeks)),
            1,
            Math.Min(MaxItemsPerWeek, orderedPool.Count));

        var slot = 0;
        for (var week = 1; week <= weeks; week++)
        {
            if (week % 4 == 0)
            {
                items.Add(new TrainingPlanItem { WeekNumber = week, IsRestWeek = true });
                continue;
            }

            for (var i = 0; i < itemsPerWeek; i++)
            {
                var exercise = orderedPool[slot % orderedPool.Count];
                slot++;
                items.Add(new TrainingPlanItem
                {
                    WeekNumber = week,
                    ExerciseId = exercise.ExerciseId,
                    RepetitionsTarget = RepetitionsFor(exercise.Difficulty),
                    IsRestWeek = false
                });
            }
        }

        return items;
    }
}
