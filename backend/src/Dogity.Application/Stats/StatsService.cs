using System.Globalization;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Stats;

public class StatsService(IApplicationDbContext db) : IStatsService
{
    public async Task<Result<DashboardStatsDto>> GetDashboardAsync(Guid userId, CancellationToken ct = default)
    {
        var dogIds = await db.DogOwners
            .Where(o => o.UserId == userId)
            .Select(o => o.DogId)
            .ToListAsync(ct);

        if (dogIds.Count == 0)
            return Result<DashboardStatsDto>.Success(new DashboardStatsDto(BuildEmptyWeeks(), []));

        var cutoff12w = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-84));
        var cutoff30d = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var recentDates = await db.TrainingSessions
            .Where(s => dogIds.Contains(s.DogId) && s.Date >= cutoff12w)
            .Select(s => s.Date)
            .ToListAsync(ct);

        var weeklyActivity = BuildWeeklyActivity(recentDates);

        var dogs = await db.Dogs
            .Where(d => dogIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .AsNoTracking()
            .ToListAsync(ct);

        // Eine Batch-Abfrage je Kennzahl über alle Hunde statt einer
        // Schleife mit mehreren Roundtrips pro Hund (N+1) - bei mehreren
        // Hunden sonst spürbar langsam, gerade auf Mobilfunk.
        var sessionCounts = await db.TrainingSessions
            .Where(s => dogIds.Contains(s.DogId))
            .GroupBy(s => s.DogId)
            .Select(g => new { DogId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DogId, g => g.Count, ct);

        var sessionsLast30dCounts = await db.TrainingSessions
            .Where(s => dogIds.Contains(s.DogId) && s.Date >= cutoff30d)
            .GroupBy(s => s.DogId)
            .Select(g => new { DogId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DogId, g => g.Count, ct);

        var activeGoalCounts = await db.Goals
            .Where(g => dogIds.Contains(g.DogId) && g.Status == GoalStatus.Active)
            .GroupBy(g => g.DogId)
            .Select(g => new { DogId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.DogId, g => g.Count, ct);

        var avgRatings = await db.TrainingExercises
            .Where(e => e.TrainingSession!.Date >= cutoff30d && dogIds.Contains(e.TrainingSession.DogId))
            .GroupBy(e => e.TrainingSession!.DogId)
            .Select(g => new { DogId = g.Key, Avg = g.Average(e => (double)e.Rating) })
            .ToDictionaryAsync(g => g.DogId, g => g.Avg, ct);

        var activePlanItems = await db.TrainingPlanItems
            .Where(i => dogIds.Contains(i.TrainingPlan!.Goal!.DogId) && i.TrainingPlan.Goal.Status == GoalStatus.Active && !i.IsRestWeek)
            .Select(i => new { i.Id, DogId = i.TrainingPlan!.Goal!.DogId, i.RepetitionsTarget })
            .ToListAsync(ct);

        var planItemIds = activePlanItems.Select(i => i.Id).ToList();
        var completedCountsByItem = await db.TrainingExercises
            .Where(e => e.TrainingPlanItemId != null && planItemIds.Contains(e.TrainingPlanItemId.Value))
            .GroupBy(e => e.TrainingPlanItemId!.Value)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ItemId, g => g.Count, ct);

        var planItemsTotalByDog = activePlanItems
            .GroupBy(i => i.DogId)
            .ToDictionary(g => g.Key, g => g.Count());

        var planItemsCompletedByDog = activePlanItems
            .GroupBy(i => i.DogId)
            .ToDictionary(g => g.Key, g => g.Count(i => completedCountsByItem.GetValueOrDefault(i.Id) >= i.RepetitionsTarget));

        var perDog = dogs
            .Select(dog => new DogStatsDto(
                dog.Id,
                dog.Name,
                sessionCounts.GetValueOrDefault(dog.Id),
                sessionsLast30dCounts.GetValueOrDefault(dog.Id),
                activeGoalCounts.GetValueOrDefault(dog.Id),
                avgRatings.TryGetValue(dog.Id, out var avg) ? Math.Round(avg, 1) : null,
                planItemsCompletedByDog.GetValueOrDefault(dog.Id),
                planItemsTotalByDog.GetValueOrDefault(dog.Id)))
            .ToList();

        return Result<DashboardStatsDto>.Success(new DashboardStatsDto(weeklyActivity, perDog));
    }

    public async Task<Result<IReadOnlyList<DogExerciseStatDto>>> GetDogExerciseStatsAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<DogExerciseStatDto>>.Failure("Hund nicht gefunden.");

        // Nur die für die Aggregation nötigen Felder laden. Der Anzeigename ist
        // die Katalog-Übung oder - bei Freitext-Einträgen (ExerciseId null) -
        // der Freitext selbst; nach diesem Namen wird gruppiert.
        var rows = await db.TrainingExercises
            .Where(e => e.TrainingSession!.DogId == dogId)
            .Select(e => new ExerciseRow(
                e.Exercise != null ? e.Exercise.Name : e.FreeTextLabel!,
                e.Rating,
                e.Success,
                e.TrainingSession!.Date))
            .AsNoTracking()
            .ToListAsync(ct);

        var stats = rows
            .GroupBy(r => r.Name)
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.Date).ToList();
                var count = ordered.Count;
                var avg = ordered.Average(x => (double)x.Rating);
                var successRate = ordered.Count(x => x.Success) / (double)count;

                // Trend nur bei genug Datenpunkten (>= 4): jüngere vs. ältere
                // Hälfte. So wird eine einzelne gute/schlechte Einheit nicht als
                // Trend fehlgedeutet.
                double? trend = null;
                if (count >= 4)
                {
                    var half = count / 2;
                    var older = ordered.Take(half).Average(x => (double)x.Rating);
                    var recent = ordered.Skip(count - half).Average(x => (double)x.Rating);
                    trend = Math.Round(recent - older, 1);
                }

                return new DogExerciseStatDto(
                    g.Key,
                    count,
                    Math.Round(avg, 1),
                    Math.Round(successRate, 2),
                    trend,
                    ordered[^1].Date);
            })
            // Schwächste Übung zuerst - die Reihenfolge ist zugleich die
            // regelbasierte "Fokus"-Empfehlung ohne externe KI.
            .OrderBy(s => s.AvgRating)
            .ThenByDescending(s => s.Count)
            .ToList();

        return Result<IReadOnlyList<DogExerciseStatDto>>.Success(stats);
    }

    private record ExerciseRow(string Name, int Rating, bool Success, DateOnly Date);

    private static IReadOnlyList<WeeklyActivityDto> BuildWeeklyActivity(List<DateOnly> dates)
    {
        var grouped = dates
            .GroupBy(d => (
                Year: ISOWeek.GetYear(d.ToDateTime(TimeOnly.MinValue)),
                Week: ISOWeek.GetWeekOfYear(d.ToDateTime(TimeOnly.MinValue))))
            .ToDictionary(g => g.Key, g => g.Count());

        var weeks = new List<WeeklyActivityDto>();
        for (int i = 11; i >= 0; i--)
        {
            var weekDate = DateTime.UtcNow.AddDays(-7 * i);
            var year = ISOWeek.GetYear(weekDate);
            var week = ISOWeek.GetWeekOfYear(weekDate);
            grouped.TryGetValue((year, week), out var count);
            weeks.Add(new WeeklyActivityDto($"{year}-KW{week:D2}", count));
        }
        return weeks;
    }

    private static IReadOnlyList<WeeklyActivityDto> BuildEmptyWeeks()
    {
        var weeks = new List<WeeklyActivityDto>();
        for (int i = 11; i >= 0; i--)
        {
            var weekDate = DateTime.UtcNow.AddDays(-7 * i);
            var year = ISOWeek.GetYear(weekDate);
            var week = ISOWeek.GetWeekOfYear(weekDate);
            weeks.Add(new WeeklyActivityDto($"{year}-KW{week:D2}", 0));
        }
        return weeks;
    }
}
