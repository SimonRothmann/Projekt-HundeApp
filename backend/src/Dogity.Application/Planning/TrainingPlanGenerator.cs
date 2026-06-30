using Dogity.Domain.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Planning;

/// <summary>
/// Regelbasierter, deterministischer Generator für Wochenpläne
/// (siehe DATABASE.md "training_plans"). Bewusst kein KI/ML-Ansatz -
/// echte Trainingsanalyse ist laut PRODUCT_REQUIREMENTS.md "Nicht-MVP".
/// Verteilt die Übungen der Zielsportart per Round-Robin über die
/// verbleibenden Wochen bis zum Zieldatum, mit einer Pausenwoche alle
/// vier Wochen. Pro Trainingswoche werden bis zu <see cref="ItemsPerWeek"/>
/// verschiedene Übungs-Ziele angelegt (echtes Training findet realistisch
/// öfter als einmal pro Woche statt, mit mehreren Übungen je Einheit - ein
/// einzelnes Wochenziel würde das nicht abbilden können). Jedes Ziel-Item
/// trägt zusätzlich <see cref="TrainingPlanItem.RepetitionsTarget"/>, wie
/// oft die Übung diese Woche trainiert werden soll; erfüllt wird das durch
/// reale, damit verknüpfte Tagebucheinträge (TrainingExercise.TrainingPlanItemId),
/// nicht durch ein separates "erledigt"-Flag im Plan selbst.
/// </summary>
public static class TrainingPlanGenerator
{
    private const int MaxWeeks = 12;
    private const int ItemsPerWeek = 2;

    private static int RepetitionsFor(ExerciseDifficulty difficulty) => difficulty switch
    {
        ExerciseDifficulty.Beginner => 3,
        ExerciseDifficulty.Intermediate => 2,
        ExerciseDifficulty.Advanced => 1,
        _ => 2
    };

    public static List<TrainingPlanItem> Generate(DateOnly today, DateOnly targetDate, IReadOnlyList<Exercise> exercises)
    {
        var daysUntilTarget = Math.Max(1, targetDate.DayNumber - today.DayNumber);
        var weeks = Math.Clamp((int)Math.Ceiling(daysUntilTarget / 7.0), 1, MaxWeeks);

        var orderedExercises = exercises
            .OrderBy(e => e.Difficulty)
            .ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();

        // Nicht mehr Ziele pro Woche als es überhaupt unterschiedliche
        // Übungen gibt, sonst würde dieselbe Übung als zwei Zielen derselben
        // Woche doppelt auftauchen.
        var itemsPerWeek = Math.Min(ItemsPerWeek, orderedExercises.Count);

        var items = new List<TrainingPlanItem>();
        for (var week = 1; week <= weeks; week++)
        {
            var isRestWeek = week % 4 == 0;
            if (isRestWeek || itemsPerWeek == 0)
            {
                items.Add(new TrainingPlanItem { WeekNumber = week, IsRestWeek = true });
                continue;
            }

            for (var slot = 0; slot < itemsPerWeek; slot++)
            {
                var exerciseIndex = ((week - 1) * itemsPerWeek + slot) % orderedExercises.Count;
                var exercise = orderedExercises[exerciseIndex];
                items.Add(new TrainingPlanItem
                {
                    WeekNumber = week,
                    ExerciseId = exercise.Id,
                    RepetitionsTarget = RepetitionsFor(exercise.Difficulty),
                    IsRestWeek = false
                });
            }
        }

        return items;
    }
}
