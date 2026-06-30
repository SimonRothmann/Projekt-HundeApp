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
/// pro Woche (1-4 Übungen) richtet sich danach, wie viele Pflichtübungen in
/// der verfügbaren Zeit überhaupt untergebracht werden müssen, statt einer
/// festen Konstante. Reicht die Zeit für mehr als eine Runde, wiederholen
/// sich die Pflichtübungen zyklisch.
/// </summary>
public static class TrainingPlanGenerator
{
    private const int MaxWeeks = 12;
    private const int MaxItemsPerWeek = 4;

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

        // Genug Übungen pro Woche, um den gesamten Pflichtkatalog bis zum
        // Zieldatum mindestens einmal unterzubringen, aber nicht mehr als
        // MaxItemsPerWeek (sonst wird eine einzelne Woche unrealistisch
        // überladen) und nicht mehr, als es überhaupt verschiedene Übungen
        // gibt (sonst käme dieselbe Übung zweimal in derselben Woche vor).
        var itemsPerWeek = Math.Clamp(
            (int)Math.Ceiling(orderedPool.Count / (double)nonRestWeeks),
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
