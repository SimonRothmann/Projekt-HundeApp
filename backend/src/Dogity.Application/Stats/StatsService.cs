using System.Globalization;
using Dogity.Application.Abstractions;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
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
            return Result<IReadOnlyList<DogExerciseStatDto>>.NotFound("Hund nicht gefunden.");

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

    public async Task<Result<DogTrackStatsDto>> GetDogTrackStatsAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<DogTrackStatsDto>.NotFound("Hund nicht gefunden.");

        // Nur die Kennzahlen laden - genau dafür sind sie am Ablauf persistiert
        // (die GPS-Punkte selbst bleiben hier außen vor).
        // GpsTrack kennt nur die TrainingSessionId (keine Navigation), daher
        // explizit verknüpfen.
        var rows = await (
            from run in db.GpsWalkRuns
            join track in db.GpsTracks on run.TrackId equals track.Id
            join session in db.TrainingSessions on track.TrainingSessionId equals session.Id
            where run.EvaluatedAt != null && session.DogId == dogId
            orderby session.Date, run.CreatedAt
            select new DogTrackRunDto(
                session.Date,
                run.AvgDeviationMeters!.Value,
                run.OnTrackPercent!.Value,
                run.ArticlesFound ?? 0,
                run.ArticlesTotal ?? 0,
                run.Stops.Count(s => s.Kind == WalkStopKind.Unexplained)))
            .AsNoTracking()
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Result<DogTrackStatsDto>.Success(new DogTrackStatsDto([], null, null));

        // Anzeige auf die jüngsten 12 Abläufe begrenzen - genug für einen
        // Verlauf, ohne die Karte auf dem Handy zu überfüllen.
        var recent = rows.Count > 12 ? rows.Skip(rows.Count - 12).ToList() : rows;

        double? deviationTrend = null;
        double? onTrackTrend = null;
        if (recent.Count >= 4)
        {
            var half = recent.Count / 2;
            deviationTrend = Math.Round(
                recent.Skip(recent.Count - half).Average(r => r.AvgDeviationMeters)
                - recent.Take(half).Average(r => r.AvgDeviationMeters), 1);
            onTrackTrend = Math.Round(
                recent.Skip(recent.Count - half).Average(r => r.OnTrackPercent)
                - recent.Take(half).Average(r => r.OnTrackPercent), 1);
        }

        var runs = recent
            .Select(r => r with { AvgDeviationMeters = Math.Round(r.AvgDeviationMeters, 1), OnTrackPercent = Math.Round(r.OnTrackPercent, 0) })
            .ToList();

        return Result<DogTrackStatsDto>.Success(new DogTrackStatsDto(runs, deviationTrend, onTrackTrend));
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

    /// <inheritdoc />
    public async Task<Result<DogConditionStatsDto>> GetDogConditionStatsAsync(
        Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<DogConditionStatsDto>.NotFound("Hund nicht gefunden.");

        // Eine Abfrage über alle Einheiten des Hundes: die Auswertung braucht
        // ohnehin ALLE Tage, um zu wissen, an welchen davor trainiert wurde.
        var einheiten = await db.TrainingSessions
            .Where(s => s.DogId == dogId)
            .Select(s => new SessionRow(
                s.Date,
                s.Condition,
                s.Exercises.Count,
                s.Exercises.Count == 0 ? (double?)null : s.Exercises.Average(e => (double)e.Rating),
                s.Exercises.Count == 0 ? (double?)null : s.Exercises.Count(e => e.Success) / (double)s.Exercises.Count))
            .AsNoTracking()
            .ToListAsync(ct);

        var nachVerfassung = einheiten
            .Where(e => e.Condition is not null)
            .GroupBy(e => e.Condition!.Value)
            .OrderBy(g => g.Key)
            .Select(g => new ConditionRatingDto(
                g.Key,
                g.Count(),
                Mittel(g.Select(e => e.AvgRating)),
                Mittel(g.Select(e => e.SuccessRate))))
            .ToList();

        return Result<DogConditionStatsDto>.Success(new DogConditionStatsDto(
            nachVerfassung,
            NachTrainingsdichte(einheiten),
            einheiten.Count(e => e.Condition is not null),
            einheiten.Count));
    }

    /// <summary>
    /// Gruppiert die Einheiten danach, wie viele Tage unmittelbar davor schon
    /// trainiert wurde: 0 (Pause am Vortag), 1, oder 2 und mehr am Stück.
    ///
    /// Gezählt werden zusammenhängende Trainingstage direkt vor dem Tag - nicht
    /// "Trainings der letzten drei Tage". Genau danach fragt man sich im
    /// Alltag: "der dritte Tag in Folge, kein Wunder".
    /// </summary>
    private static IReadOnlyList<TrainingDensityDto> NachTrainingsdichte(List<SessionRow> einheiten)
    {
        var tage = einheiten.Select(e => e.Date).ToHashSet();

        int VortageAmStueck(DateOnly tag)
        {
            var anzahl = 0;
            var vorher = tag.AddDays(-1);
            // Bei zwei ist Schluss: mehr Stufen würden die Gruppen so klein
            // machen, dass der Schnitt nichts mehr aussagt.
            while (anzahl < 2 && tage.Contains(vorher))
            {
                anzahl++;
                vorher = vorher.AddDays(-1);
            }
            return anzahl;
        }

        return einheiten
            .GroupBy(e => VortageAmStueck(e.Date))
            .OrderBy(g => g.Key)
            .Select(g => new TrainingDensityDto(
                g.Key,
                g.Count(),
                Mittel(g.Select(e => e.AvgRating)),
                Anteil(g, e => e.Condition is DogCondition.Tired or DogCondition.Stressed)))
            .ToList();
    }

    /// <summary>Mittelwert über die vorhandenen Werte; null, wenn keiner da ist.</summary>
    private static double? Mittel(IEnumerable<double?> werte)
    {
        var vorhanden = werte.Where(w => w is not null).Select(w => w!.Value).ToList();
        return vorhanden.Count == 0 ? null : Math.Round(vorhanden.Average(), 2);
    }

    /// <summary>
    /// Anteil innerhalb der Einheiten MIT angegebener Verfassung. Einheiten
    /// ohne Angabe bleiben außen vor - sonst sähe ein Hund umso ausgeglichener
    /// aus, je seltener jemand etwas eingetragen hat.
    /// </summary>
    private static double? Anteil(IEnumerable<SessionRow> gruppe, Func<SessionRow, bool> trifftZu)
    {
        var mitAngabe = gruppe.Where(e => e.Condition is not null).ToList();
        return mitAngabe.Count == 0 ? null : Math.Round(mitAngabe.Count(trifftZu) / (double)mitAngabe.Count, 2);
    }

    private sealed record SessionRow(
        DateOnly Date, DogCondition? Condition, int ExerciseCount, double? AvgRating, double? SuccessRate);

}