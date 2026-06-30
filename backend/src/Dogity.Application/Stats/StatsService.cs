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

        var perDog = new List<DogStatsDto>();
        foreach (var dog in dogs)
        {
            var sessionCount = await db.TrainingSessions
                .CountAsync(s => s.DogId == dog.Id, ct);

            var sessionsLast30d = await db.TrainingSessions
                .CountAsync(s => s.DogId == dog.Id && s.Date >= cutoff30d, ct);

            var activeGoals = await db.Goals
                .CountAsync(g => g.DogId == dog.Id && g.Status == GoalStatus.Active, ct);

            var sessionIds30d = await db.TrainingSessions
                .Where(s => s.DogId == dog.Id && s.Date >= cutoff30d)
                .Select(s => s.Id)
                .ToListAsync(ct);

            double? avgRating = null;
            if (sessionIds30d.Count > 0)
            {
                var ratings = await db.TrainingExercises
                    .Where(e => sessionIds30d.Contains(e.TrainingSessionId))
                    .Select(e => (double)e.Rating)
                    .ToListAsync(ct);
                if (ratings.Count > 0)
                    avgRating = Math.Round(ratings.Average(), 1);
            }

            var planItemsTotal = await db.TrainingPlanItems
                .Where(i => i.TrainingPlan!.Goal!.DogId == dog.Id && i.TrainingPlan.Goal.Status == GoalStatus.Active && !i.IsRestWeek)
                .CountAsync(ct);

            var planItemsCompleted = await db.TrainingPlanItems
                .Where(i => i.TrainingPlan!.Goal!.DogId == dog.Id && i.TrainingPlan.Goal.Status == GoalStatus.Active && !i.IsRestWeek)
                .CountAsync(i => db.TrainingExercises.Count(e => e.TrainingPlanItemId == i.Id) >= i.RepetitionsTarget, ct);

            perDog.Add(new DogStatsDto(dog.Id, dog.Name, sessionCount, sessionsLast30d, activeGoals, avgRating, planItemsCompleted, planItemsTotal));
        }

        return Result<DashboardStatsDto>.Success(new DashboardStatsDto(weeklyActivity, perDog));
    }

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
