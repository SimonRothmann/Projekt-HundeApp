using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Planning;

/// <summary>
/// Use Cases für die Zielplanung (siehe FEATURE_MODULE.md "Planning").
/// Erzeugt beim Anlegen eines Ziels automatisch einen Trainingsplan
/// (siehe <see cref="TrainingPlanGenerator"/>). Zugriff ist immer auf
/// Ziele beschränkt, deren Hund dem aufrufenden Benutzer zugeordnet ist.
/// </summary>
public class GoalService(IApplicationDbContext db, TimeProvider timeProvider) : IGoalService
{
    public async Task<Result<IReadOnlyList<GoalDto>>> GetByDogAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<GoalDto>>.Failure("Hund nicht gefunden.");

        var goals = await LoadGoalsQuery()
            .Where(g => g.DogId == dogId)
            .OrderBy(g => g.TargetDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var sportNames = await GetSportNamesAsync(goals, ct);
        return Result<IReadOnlyList<GoalDto>>.Success(goals.Select(g => ToDto(g, sportNames)).ToList());
    }

    public async Task<Result<GoalDto>> GetByIdAsync(Guid userId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await GetOwnedGoalAsync(userId, goalId, ct, track: false);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");

        var sportNames = await GetSportNamesAsync([goal], ct);
        return Result<GoalDto>.Success(ToDto(goal, sportNames));
    }

    public async Task<Result<GoalDto>> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (request.TargetDate <= today)
            return Result<GoalDto>.Failure("Zieldatum muss in der Zukunft liegen.");

        if (!await db.HasDogAccessAsync(userId, request.DogId, ct))
            return Result<GoalDto>.Failure("Hund nicht gefunden.");

        var sportExists = await db.Sports.AnyAsync(s => s.Id == request.SportId, ct);
        if (!sportExists)
            return Result<GoalDto>.Failure("Sportart nicht gefunden.");

        var exercises = await db.Exercises.Where(e => e.SportId == request.SportId).ToListAsync(ct);

        var goal = new Goal
        {
            DogId = request.DogId,
            SportId = request.SportId,
            TargetDate = request.TargetDate,
            Notes = request.Notes
        };

        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal };
        foreach (var item in TrainingPlanGenerator.Generate(today, request.TargetDate, exercises))
        {
            item.TrainingPlanId = plan.Id;
            plan.Items.Add(item);
        }
        goal.TrainingPlan = plan;

        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);

        var created = await GetOwnedGoalAsync(userId, goal.Id, ct, track: false);
        var sportNames = await GetSportNamesAsync([created!], ct);
        return Result<GoalDto>.Success(ToDto(created!, sportNames));
    }

    public async Task<Result<GoalDto>> UpdateStatusAsync(Guid userId, Guid goalId, GoalStatus status, CancellationToken ct = default)
    {
        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");

        goal.Status = status;
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var sportNames = await GetSportNamesAsync([goal], ct);
        return Result<GoalDto>.Success(ToDto(goal, sportNames));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result.Failure("Ziel nicht gefunden.");

        goal.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private IQueryable<Goal> LoadGoalsQuery() =>
        db.Goals
            .Include(g => g.TrainingPlan)
            .ThenInclude(p => p!.Items)
            .ThenInclude(i => i.Exercise);

    // track: false fuer reine Lesezugriffe (kein SaveChangesAsync im selben
    // Aufruf) - vermeidet unnoetiges Change-Tracking. UpdateStatusAsync/
    // DeleteAsync brauchen weiterhin ein getracktes Entity (Default true).
    private async Task<Goal?> GetOwnedGoalAsync(Guid userId, Guid goalId, CancellationToken ct, bool track = true)
    {
        var query = LoadGoalsQuery();
        if (!track) query = query.AsNoTracking();

        return await query
            .Where(g => g.Id == goalId)
            .Where(g =>
                db.DogOwners.Any(o => o.DogId == g.DogId && o.UserId == userId) ||
                db.TrainerAssignments.Any(t => t.DogId == g.DogId && t.TrainerId == userId))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Dictionary<Guid, string>> GetSportNamesAsync(IReadOnlyList<Goal> goals, CancellationToken ct)
    {
        var sportIds = goals.Select(g => g.SportId).Distinct().ToList();
        return await db.Sports
            .Where(s => sportIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    }

    private static GoalDto ToDto(Goal g, IReadOnlyDictionary<Guid, string> sportNames)
    {
        var sportName = sportNames.GetValueOrDefault(g.SportId, string.Empty);
        TrainingPlanDto? planDto = g.TrainingPlan is null
            ? null
            : new TrainingPlanDto(
                g.TrainingPlan.Id,
                g.TrainingPlan.GeneratedAt,
                g.TrainingPlan.Items
                    .OrderBy(i => i.WeekNumber)
                    .Select(i => new TrainingPlanItemDto(i.Id, i.WeekNumber, i.ExerciseId, i.Exercise?.Name, i.RepetitionsTarget, i.IsRestWeek))
                    .ToList());

        return new GoalDto(g.Id, g.DogId, g.SportId, sportName, g.TargetDate, g.Status, g.Notes, planDto);
    }
}
