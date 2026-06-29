using Dogity.Domain.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Planning;

/// <summary>
/// Regelbasierter, deterministischer Generator für Wochenpläne
/// (siehe DATABASE.md "training_plans"). Bewusst kein KI/ML-Ansatz -
/// echte Trainingsanalyse ist laut PRODUCT_REQUIREMENTS.md "Nicht-MVP".
/// Verteilt die Übungen der Zielsportart per Round-Robin über die
/// verbleibenden Wochen bis zum Zieldatum, mit einer Pausenwoche alle
/// vier Wochen.
/// </summary>
public static class TrainingPlanGenerator
{
    private const int MaxWeeks = 12;

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

        var items = new List<TrainingPlanItem>();
        for (var week = 1; week <= weeks; week++)
        {
            var isRestWeek = week % 4 == 0;
            if (isRestWeek || orderedExercises.Count == 0)
            {
                items.Add(new TrainingPlanItem { WeekNumber = week, IsRestWeek = true });
                continue;
            }

            var exercise = orderedExercises[(week - 1) % orderedExercises.Count];
            items.Add(new TrainingPlanItem
            {
                WeekNumber = week,
                ExerciseId = exercise.Id,
                RepetitionsTarget = RepetitionsFor(exercise.Difficulty),
                IsRestWeek = false
            });
        }

        return items;
    }
}
