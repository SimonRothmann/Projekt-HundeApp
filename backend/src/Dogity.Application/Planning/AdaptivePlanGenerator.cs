using Dogity.Domain.Planning;
using Dogity.Domain.Sports;

namespace Dogity.Application.Planning;

/// <summary>
/// Ein Kandidat für die adaptive Wochenauswahl: eine Katalog-Übung samt ihrem
/// aktuellen Mastery-Zustand (flach aus <see cref="ExerciseMastery"/> übergeben,
/// damit die Auswahlfunktion pur/deterministisch und gut testbar bleibt).
/// Nie trainiert = <see cref="SessionCount"/> 0.
/// </summary>
public readonly record struct AdaptiveCandidate(
    Guid ExerciseId,
    string Name,
    ExerciseDifficulty Difficulty,
    int SessionCount,
    double RecentAvgRating,
    DateOnly? DueDate,
    int ManualPriority);

/// <summary>Ziel-Konfiguration (siehe <see cref="Goal"/>).</summary>
public readonly record struct AdaptivePlanConfig(int WeeklyExerciseCount, int TrainingDaysPerWeek);

/// <summary>
/// Adaptiver Wochenplan-Generator (P3, siehe docs/SMART_TRAINING_PLAN.md).
/// Reine, deterministische Funktion: wählt für EINE Woche aus den Kandidaten
/// einen abwechslungsreichen Übungs-Mix per Slot-Budget (Schwachstellen /
/// Wiederholung / Neu), rankt innerhalb der Buckets per Score (überfällig +
/// Schwäche + manueller Boost), verteilt auf die Trainingstage und ordnet je
/// Tag nach Schwierigkeit (leicht → schwer). Erzeugt eine Trainings-Woche -
/// ob eine Woche eine Pausenwoche ist, entscheidet die Wiring-/Scheduler-
/// Schicht (P4).
/// </summary>
public static class AdaptivePlanGenerator
{
    private const double WeaknessWeight = 3.0;
    private const double ManualWeight = 3.0;

    // Anteile am Wochenbudget; der Rest (Rundung) geht an "Neu" bzw. wird über
    // die Auffüll-Logik verteilt.
    private const double WeaknessShare = 0.5;
    private const double RepetitionShare = 0.3;

    private static int RepetitionsFor(ExerciseDifficulty d, bool weak)
    {
        var baseReps = d switch
        {
            ExerciseDifficulty.Beginner => 3,
            ExerciseDifficulty.Intermediate => 2,
            ExerciseDifficulty.Advanced => 1,
            _ => 2
        };
        // Schwachstellen bewusst häufiger üben.
        return weak ? baseReps + 1 : baseReps;
    }

    private static double Score(AdaptiveCandidate c, DateOnly today)
    {
        double overdue = c.DueDate is { } due ? Math.Max(0, today.DayNumber - due.DayNumber) : 0;
        double weakness = c.SessionCount == 0 ? 0 : Math.Clamp(3.0 - c.RecentAvgRating, 0, 2);
        return overdue + WeaknessWeight * weakness + ManualWeight * c.ManualPriority;
    }

    private static PlanItemReason BucketOf(AdaptiveCandidate c) =>
        c.SessionCount == 0 ? PlanItemReason.Introduction
        : c.RecentAvgRating < 3.0 ? PlanItemReason.Weakness
        : PlanItemReason.Repetition;

    public static List<TrainingPlanItem> GenerateWeek(
        DateOnly today,
        int weekNumber,
        IReadOnlyList<AdaptiveCandidate> candidates,
        AdaptivePlanConfig config)
    {
        var items = new List<TrainingPlanItem>();
        if (candidates.Count == 0) return items;

        int target = Math.Clamp(config.WeeklyExerciseCount, 1, candidates.Count);
        int days = Math.Max(1, config.TrainingDaysPerWeek);

        // Kandidaten je Bucket ranken. "Neu" zusätzlich nach Schwierigkeit
        // aufsteigend -> leicht zuerst ("nicht alles Schwere sofort").
        List<AdaptiveCandidate> Ranked(PlanItemReason bucket)
        {
            var list = candidates.Where(c => BucketOf(c) == bucket);
            return bucket == PlanItemReason.Introduction
                ? list.OrderBy(c => c.Difficulty).ThenByDescending(c => Score(c, today)).ThenBy(c => c.Name, StringComparer.Ordinal).ToList()
                : list.OrderByDescending(c => Score(c, today)).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
        }

        var pools = new Dictionary<PlanItemReason, Queue<AdaptiveCandidate>>
        {
            [PlanItemReason.Weakness] = new(Ranked(PlanItemReason.Weakness)),
            [PlanItemReason.Repetition] = new(Ranked(PlanItemReason.Repetition)),
            [PlanItemReason.Introduction] = new(Ranked(PlanItemReason.Introduction)),
        };

        int weaknessSlots = (int)Math.Round(target * WeaknessShare);
        int repetitionSlots = (int)Math.Round(target * RepetitionShare);
        int introSlots = Math.Max(0, target - weaknessSlots - repetitionSlots);

        var selected = new List<(AdaptiveCandidate Candidate, PlanItemReason Reason)>();

        void Take(PlanItemReason bucket, int count)
        {
            var q = pools[bucket];
            for (int i = 0; i < count && q.Count > 0; i++)
                selected.Add((q.Dequeue(), bucket));
        }

        Take(PlanItemReason.Weakness, weaknessSlots);
        Take(PlanItemReason.Repetition, repetitionSlots);
        Take(PlanItemReason.Introduction, introSlots);

        // Leergebliebene Slots (zu wenig in einem Bucket) global auffüllen, damit
        // die Woche möglichst voll wird - Rest nach Score.
        if (selected.Count < target)
        {
            var rest = pools
                .SelectMany(kv => kv.Value.Select(c => (Candidate: c, Reason: kv.Key)))
                .OrderByDescending(x => Score(x.Candidate, today))
                .ThenBy(x => x.Candidate.Name, StringComparer.Ordinal)
                .ToList();
            foreach (var r in rest)
            {
                if (selected.Count >= target) break;
                selected.Add(r);
            }
        }

        // Auf Trainingstage verteilen: nach Schwierigkeit sortieren, dann
        // Round-Robin - jeder Tag ausgewogen, innerhalb eines Tages leicht → schwer.
        var ordered = selected
            .OrderBy(s => s.Candidate.Difficulty)
            .ThenBy(s => s.Candidate.Name, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            var (candidate, reason) = ordered[i];
            items.Add(new TrainingPlanItem
            {
                WeekNumber = weekNumber,
                ExerciseId = candidate.ExerciseId,
                RepetitionsTarget = RepetitionsFor(candidate.Difficulty, reason == PlanItemReason.Weakness),
                IsRestWeek = false,
                DayIndex = (i % days) + 1,
                Source = PlanItemSource.Auto,
                Reason = reason,
                Difficulty = candidate.Difficulty
            });
        }

        return items;
    }
}
