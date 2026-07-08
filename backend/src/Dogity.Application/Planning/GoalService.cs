using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Planning;

/// <summary>
/// Use Cases für die Zielplanung (siehe FEATURE_MODULE.md "Planning").
/// Erzeugt beim Anlegen eines Ziels automatisch einen Trainingsplan
/// (siehe <see cref="TrainingPlanGenerator"/>) und erlaubt ihn danach
/// manuell zu erweitern (AddPlanItemAsync/RemovePlanItemAsync). Zugriff ist
/// immer auf Ziele beschränkt, deren Hund dem aufrufenden Benutzer
/// zugeordnet ist.
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
        var regulationNames = await GetRegulationNamesAsync(goals, ct);
        var logsByPlanItem = await GetLogsByPlanItemAsync(goals, ct);
        return Result<IReadOnlyList<GoalDto>>.Success(goals.Select(g => ToDto(g, sportNames, regulationNames, logsByPlanItem)).ToList());
    }

    public async Task<Result<GoalDto>> GetByIdAsync(Guid userId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await GetOwnedGoalAsync(userId, goalId, ct, track: false);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");

        var sportNames = await GetSportNamesAsync([goal], ct);
        var regulationNames = await GetRegulationNamesAsync([goal], ct);
        var logsByPlanItem = await GetLogsByPlanItemAsync([goal], ct);
        return Result<GoalDto>.Success(ToDto(goal, sportNames, regulationNames, logsByPlanItem));
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

        // Individueller Plan schließt eine Prüfungsordnung aus - Nutzer legt
        // die Wochenübungen ohnehin manuell fest.
        var regulationId = request.IsCustom ? (Guid?)null : request.RegulationId;
        if (regulationId is { } regId)
        {
            var regulationBelongsToSport = await db.Regulations.AnyAsync(r => r.Id == regId && r.SportId == request.SportId, ct);
            if (!regulationBelongsToSport)
                return Result<GoalDto>.Failure("Prüfungsordnung gehört nicht zu dieser Sportart.");
        }

        var goal = new Goal
        {
            DogId = request.DogId,
            SportId = request.SportId,
            RegulationId = regulationId,
            TargetDate = request.TargetDate,
            Notes = request.Notes,
            IsCustom = request.IsCustom
        };

        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal };
        // Individueller Plan startet leer - der Nutzer legt die Wochenübungen
        // über AddPlanItemAsync selbst an. Auto-Generieren nur bei geführten
        // Zielen mit Sport/Prüfungsordnung.
        if (!request.IsCustom)
        {
            var candidates = await ResolvePlanCandidatesAsync(request.SportId, regulationId, ct);
            foreach (var item in TrainingPlanGenerator.Generate(today, request.TargetDate, candidates))
            {
                item.TrainingPlanId = plan.Id;
                plan.Items.Add(item);
            }
        }
        goal.TrainingPlan = plan;

        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);

        var created = await GetOwnedGoalAsync(userId, goal.Id, ct, track: false);
        var sportNames = await GetSportNamesAsync([created!], ct);
        var regulationNames = await GetRegulationNamesAsync([created!], ct);
        var logsByPlanItem = await GetLogsByPlanItemAsync([created!], ct);
        return Result<GoalDto>.Success(ToDto(created!, sportNames, regulationNames, logsByPlanItem));
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
        var regulationNames = await GetRegulationNamesAsync([goal], ct);
        var logsByPlanItem = await GetLogsByPlanItemAsync([goal], ct);
        return Result<GoalDto>.Success(ToDto(goal, sportNames, regulationNames, logsByPlanItem));
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

    public async Task<Result<GoalDto>> AddPlanItemAsync(Guid userId, Guid goalId, AddTrainingPlanItemRequest request, CancellationToken ct = default)
    {
        if (request.WeekNumber < 1)
            return Result<GoalDto>.Failure("Wochennummer muss mindestens 1 sein.");
        if (request.RepetitionsTarget < 1)
            return Result<GoalDto>.Failure("Zielwert muss mindestens 1 sein.");

        var hasExercise = request.ExerciseId is not null;
        var hasFreeText = !string.IsNullOrWhiteSpace(request.FreeTextLabel);
        if (hasExercise == hasFreeText)
            return Result<GoalDto>.Failure("Entweder eine Übung ODER einen Freitext angeben.");

        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");
        if (goal.TrainingPlan is null)
            return Result<GoalDto>.Failure("Dieses Ziel hat keinen Trainingsplan.");

        if (request.ExerciseId is { } exerciseId)
        {
            // Nur bei zielbezogenem Plan die Sport-Zugehörigkeit prüfen;
            // Freitext hat keinen Sport-Bezug.
            var exerciseBelongsToSport = await db.Exercises.AnyAsync(e => e.Id == exerciseId && e.SportId == goal.SportId, ct);
            if (!exerciseBelongsToSport)
                return Result<GoalDto>.Failure("Übung gehört nicht zur Sportart dieses Ziels.");
        }

        // Reinen Pausenwochen-Platzhalter ersetzen, sobald die Woche eine
        // echte Übung bekommt - sonst stünden "Pause" und eine echte Übung
        // gleichzeitig in derselben Woche (siehe goals-section.tsx, das pro
        // Woche entweder "Pause" ODER die Liste der Übungen anzeigt).
        var restPlaceholder = goal.TrainingPlan.Items.FirstOrDefault(i => i.WeekNumber == request.WeekNumber && i.IsRestWeek);
        if (restPlaceholder is not null)
            restPlaceholder.DeletedAt = DateTimeOffset.UtcNow;

        db.TrainingPlanItems.Add(new TrainingPlanItem
        {
            TrainingPlanId = goal.TrainingPlan.Id,
            WeekNumber = request.WeekNumber,
            ExerciseId = request.ExerciseId,
            FreeTextLabel = hasFreeText ? request.FreeTextLabel!.Trim() : null,
            RepetitionsTarget = request.RepetitionsTarget,
            IsRestWeek = false
        });
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(userId, goalId, ct);
    }

    public async Task<Result<GoalDto>> UpdatePlanItemAsync(Guid userId, Guid goalId, Guid itemId, UpdateTrainingPlanItemRequest request, CancellationToken ct = default)
    {
        if (request.WeekNumber < 1)
            return Result<GoalDto>.Failure("Wochennummer muss mindestens 1 sein.");
        if (request.RepetitionsTarget < 1)
            return Result<GoalDto>.Failure("Zielwert muss mindestens 1 sein.");

        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");

        var item = goal.TrainingPlan?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result<GoalDto>.Failure("Plan-Ziel nicht gefunden.");
        if (item.IsRestWeek)
            return Result<GoalDto>.Failure("Eine Pausenwoche kann nicht bearbeitet werden.");

        item.WeekNumber = request.WeekNumber;
        item.RepetitionsTarget = request.RepetitionsTarget;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(userId, goalId, ct);
    }

    public async Task<Result<GoalDto>> RemovePlanItemAsync(Guid userId, Guid goalId, Guid itemId, CancellationToken ct = default)
    {
        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");

        var item = goal.TrainingPlan?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result<GoalDto>.Failure("Plan-Ziel nicht gefunden.");

        item.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(userId, goalId, ct);
    }

    private IQueryable<Goal> LoadGoalsQuery() =>
        db.Goals
            .Include(g => g.TrainingPlan)
            .ThenInclude(p => p!.Items)
            .ThenInclude(i => i.Exercise);

    // track: false fuer reine Lesezugriffe (kein SaveChangesAsync im selben
    // Aufruf) - vermeidet unnoetiges Change-Tracking. UpdateStatusAsync/
    // DeleteAsync/AddPlanItemAsync/RemovePlanItemAsync brauchen weiterhin
    // ein getracktes Entity (Default true).
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

    // Liefert die Kandidatenliste für den Generator: bei gewählter
    // Prüfungsordnung die Pflicht-/Kür-Übungen ihrer aktuellsten Version
    // (siehe SportCatalogService.GetRegulationDetailAsync - dieselbe
    // "neueste Version per ValidFrom"-Logik), sonst alle Übungen der
    // Sportart als Fallback (z.B. für Sportarten ohne hinterlegte
    // Prüfungsordnung), jeweils als Pflicht behandelt.
    private async Task<List<PlanExerciseCandidate>> ResolvePlanCandidatesAsync(Guid sportId, Guid? regulationId, CancellationToken ct)
    {
        if (regulationId is { } regId)
        {
            var currentVersion = await db.RegulationVersions
                .Where(v => v.RegulationId == regId)
                .OrderByDescending(v => v.ValidFrom)
                .FirstOrDefaultAsync(ct);

            if (currentVersion is not null)
            {
                return await db.RegulationExercises
                    .Where(re => re.RegulationVersionId == currentVersion.Id)
                    .Select(re => new PlanExerciseCandidate(re.ExerciseId, re.Exercise!.Name, re.Exercise!.Difficulty, re.IsMandatory))
                    .ToListAsync(ct);
            }
        }

        // ClubId == null: vereinsspezifische Übungen sind nie Teil einer
        // Prüfungsordnung (siehe Exercise.ClubId) und gehören daher auch
        // nicht in den Fallback-Pool ohne gewählte Prüfung - sonst könnten
        // im generierten Plan sogar Übungen eines fremden Vereins auftauchen,
        // dem der Hundehalter gar nicht angehört.
        return await db.Exercises
            .Where(e => e.SportId == sportId && e.ClubId == null)
            .Select(e => new PlanExerciseCandidate(e.Id, e.Name, e.Difficulty, true))
            .ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, string>> GetSportNamesAsync(IReadOnlyList<Goal> goals, CancellationToken ct)
    {
        var sportIds = goals.Select(g => g.SportId).Distinct().ToList();
        return await db.Sports
            .Where(s => sportIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    }

    private async Task<Dictionary<Guid, string>> GetRegulationNamesAsync(IReadOnlyList<Goal> goals, CancellationToken ct)
    {
        var regulationIds = goals.Where(g => g.RegulationId is not null).Select(g => g.RegulationId!.Value).Distinct().ToList();
        if (regulationIds.Count == 0) return new Dictionary<Guid, string>();

        return await db.Regulations
            .Where(r => regulationIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);
    }

    // Fortschritt eines Plan-Ziels ergibt sich aus echten, damit verknüpften
    // Tagebucheinträgen (TrainingExercise.TrainingPlanItemId) statt aus
    // einem separaten "erledigt"-Flag im Plan selbst - siehe TrainingPlanItem.
    private async Task<Dictionary<Guid, IReadOnlyList<TrainingPlanItemLogDto>>> GetLogsByPlanItemAsync(
        IReadOnlyList<Goal> goals, CancellationToken ct)
    {
        var planItemIds = goals
            .Where(g => g.TrainingPlan is not null)
            .SelectMany(g => g.TrainingPlan!.Items.Select(i => i.Id))
            .ToList();
        if (planItemIds.Count == 0) return new Dictionary<Guid, IReadOnlyList<TrainingPlanItemLogDto>>();

        var logs = await db.TrainingExercises
            .Where(e => e.TrainingPlanItemId != null && planItemIds.Contains(e.TrainingPlanItemId!.Value))
            .Select(e => new
            {
                PlanItemId = e.TrainingPlanItemId!.Value,
                e.TrainingSessionId,
                Date = e.TrainingSession!.Date,
                e.Rating,
                e.Success,
                e.Notes
            })
            .ToListAsync(ct);

        return logs
            .GroupBy(l => l.PlanItemId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TrainingPlanItemLogDto>)g
                    .OrderByDescending(l => l.Date)
                    .Select(l => new TrainingPlanItemLogDto(l.TrainingSessionId, l.Date, l.Rating, l.Success, l.Notes))
                    .ToList());
    }

    private static GoalDto ToDto(
        Goal g,
        IReadOnlyDictionary<Guid, string> sportNames,
        IReadOnlyDictionary<Guid, string> regulationNames,
        IReadOnlyDictionary<Guid, IReadOnlyList<TrainingPlanItemLogDto>> logsByPlanItem)
    {
        var sportName = sportNames.GetValueOrDefault(g.SportId, string.Empty);
        var regulationName = g.RegulationId is { } regId ? regulationNames.GetValueOrDefault(regId) : null;
        TrainingPlanDto? planDto = g.TrainingPlan is null
            ? null
            : new TrainingPlanDto(
                g.TrainingPlan.Id,
                g.TrainingPlan.GeneratedAt,
                g.TrainingPlan.Items
                    .OrderBy(i => i.WeekNumber)
                    .Select(i =>
                    {
                        var logs = logsByPlanItem.GetValueOrDefault(i.Id, Array.Empty<TrainingPlanItemLogDto>());
                        var completedCount = logs.Count(l => l.Success);
                        return new TrainingPlanItemDto(
                            i.Id,
                            i.WeekNumber,
                            i.ExerciseId,
                            i.Exercise?.Name,
                            i.FreeTextLabel,
                            i.RepetitionsTarget,
                            i.IsRestWeek,
                            completedCount,
                            !i.IsRestWeek && completedCount >= i.RepetitionsTarget,
                            logs);
                    })
                    .ToList());

        return new GoalDto(g.Id, g.DogId, g.SportId, sportName, g.RegulationId, regulationName, g.TargetDate, g.Status, g.Notes, g.IsCustom, planDto);
    }
}
