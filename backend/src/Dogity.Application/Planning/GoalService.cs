using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Dogs;
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
public class GoalService(IApplicationDbContext db, TimeProvider timeProvider, INotificationService notifications, IExerciseMasteryService mastery) : IGoalService
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

    public async Task<Result<GoalDto>> UpdateConfigAsync(Guid userId, Guid goalId, int weeklyExerciseCount, int trainingDaysPerWeek, CancellationToken ct = default)
    {
        if (weeklyExerciseCount < 1 || weeklyExerciseCount > 12)
            return Result<GoalDto>.Failure("Übungen pro Woche muss zwischen 1 und 12 liegen.");
        if (trainingDaysPerWeek < 1 || trainingDaysPerWeek > 7)
            return Result<GoalDto>.Failure("Trainingstage pro Woche muss zwischen 1 und 7 liegen.");

        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");

        goal.WeeklyExerciseCount = weeklyExerciseCount;
        goal.TrainingDaysPerWeek = trainingDaysPerWeek;
        goal.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(userId, goalId, ct);
    }

    public async Task<Result<GoalDto>> UpdateWeekConfigAsync(Guid userId, Guid goalId, int weekNumber, int trainingDaysPerWeek, CancellationToken ct = default)
    {
        if (weekNumber < 1)
            return Result<GoalDto>.Failure("Wochennummer muss mindestens 1 sein.");
        if (trainingDaysPerWeek < 1 || trainingDaysPerWeek > 7)
            return Result<GoalDto>.Failure("Trainingstage pro Woche muss zwischen 1 und 7 liegen.");

        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");
        if (goal.TrainingPlan is null)
            return Result<GoalDto>.Failure("Dieses Ziel hat keinen Trainingsplan.");

        var config = goal.TrainingPlan.WeekConfigs.FirstOrDefault(w => w.WeekNumber == weekNumber);
        if (config is null)
        {
            // Neue Überschreibung bewusst über das DbSet anlegen (nicht die
            // getrackte Navigation), sonst würde EF sie per Collection-Fixup als
            // Modified statt Added einstufen (dokumentierter Fallstrick).
            db.TrainingPlanWeekConfigs.Add(new TrainingPlanWeekConfig
            {
                TrainingPlanId = goal.TrainingPlan.Id,
                WeekNumber = weekNumber,
                TrainingDaysPerWeek = trainingDaysPerWeek
            });
        }
        else
        {
            config.TrainingDaysPerWeek = trainingDaysPerWeek;
            config.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Übungen dieser Woche, die auf einem nun entfallenden Tag lägen, auf den
        // letzten gültigen Tag holen (statt sie unsichtbar zu machen).
        foreach (var item in goal.TrainingPlan.Items.Where(i => i.WeekNumber == weekNumber && !i.IsRestWeek && i.DayIndex > trainingDaysPerWeek))
            item.DayIndex = trainingDaysPerWeek;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(userId, goalId, ct);
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
            IsRestWeek = false,
            DayIndex = Math.Clamp(request.DayIndex, 1, EffectiveDaysForWeek(goal, request.WeekNumber)),
            Source = PlanItemSource.Manual
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

        var hasExercise = request.ExerciseId is not null;
        var hasFreeText = !string.IsNullOrWhiteSpace(request.FreeTextLabel);
        if (hasExercise == hasFreeText)
            return Result<GoalDto>.Failure("Entweder eine Übung ODER einen Freitext angeben.");

        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");

        var item = goal.TrainingPlan?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return Result<GoalDto>.Failure("Plan-Ziel nicht gefunden.");
        if (item.IsRestWeek)
            return Result<GoalDto>.Failure("Eine Pausenwoche kann nicht bearbeitet werden.");

        if (request.ExerciseId is { } exerciseId)
        {
            // Zielsport-Konsistenz nur bei Katalog-Übung; Freitext hat keinen
            // Sport-Bezug.
            var exerciseBelongsToSport = await db.Exercises.AnyAsync(e => e.Id == exerciseId && e.SportId == goal.SportId, ct);
            if (!exerciseBelongsToSport)
                return Result<GoalDto>.Failure("Übung gehört nicht zur Sportart dieses Ziels.");
        }

        item.WeekNumber = request.WeekNumber;
        item.RepetitionsTarget = request.RepetitionsTarget;
        item.ExerciseId = request.ExerciseId;
        item.FreeTextLabel = hasFreeText ? request.FreeTextLabel!.Trim() : null;
        item.DayIndex = Math.Clamp(request.DayIndex, 1, EffectiveDaysForWeek(goal, request.WeekNumber));
        // Eine manuell bearbeitete Übung gilt als vom Nutzer festgelegt und wird
        // bei der Wochen-Neugenerierung nicht mehr überschrieben.
        item.Source = PlanItemSource.Manual;
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

    public async Task<Result<GoalDto>> RegenerateWeekAsync(Guid userId, Guid goalId, int weekNumber, CancellationToken ct = default)
    {
        if (weekNumber < 1)
            return Result<GoalDto>.Failure("Wochennummer muss mindestens 1 sein.");

        var goal = await GetOwnedGoalAsync(userId, goalId, ct);
        if (goal is null)
            return Result<GoalDto>.Failure("Ziel nicht gefunden.");
        if (goal.TrainingPlan is null)
            return Result<GoalDto>.Failure("Dieses Ziel hat keinen Trainingsplan.");
        // Individuelle Pläne legt der Nutzer bewusst komplett manuell an - hier
        // wird nichts automatisch generiert (siehe Goal.IsCustom).
        if (goal.IsCustom)
            return Result<GoalDto>.Failure("Ein individueller Plan wird nicht automatisch generiert.");

        await RegenerateWeekCoreAsync(goal, weekNumber, ct);
        return await GetByIdAsync(userId, goalId, ct);
    }

    public async Task<Result<IReadOnlyList<WeightableExerciseDto>>> GetWeightableExercisesAsync(Guid userId, Guid goalId, CancellationToken ct = default)
    {
        var goal = await GetOwnedGoalAsync(userId, goalId, ct, track: false);
        if (goal is null)
            return Result<IReadOnlyList<WeightableExerciseDto>>.Failure("Ziel nicht gefunden.");

        // Individuelle Ziele haben keinen adaptiven Plan - nichts zu gewichten.
        if (goal.IsCustom)
            return Result<IReadOnlyList<WeightableExerciseDto>>.Success([]);

        var pool = await ResolvePlanCandidatesAsync(goal.SportId, goal.RegulationId, ct);
        if (pool.Count == 0)
            return Result<IReadOnlyList<WeightableExerciseDto>>.Success([]);

        var exerciseIds = pool.Select(c => c.ExerciseId).ToList();
        var masteries = await db.ExerciseMasteries
            .Where(m => m.DogId == goal.DogId && exerciseIds.Contains(m.ExerciseId))
            .AsNoTracking()
            .ToDictionaryAsync(m => m.ExerciseId, ct);

        var plannedThisWeek = PlannedThisWeekExerciseIds(goal);

        var list = pool
            .Select(c =>
            {
                masteries.TryGetValue(c.ExerciseId, out var m);
                return new WeightableExerciseDto(
                    c.ExerciseId,
                    c.Name,
                    (int)c.Difficulty,
                    m?.ManualPriority ?? 0,
                    MasteryStatusOf(m),
                    plannedThisWeek.Contains(c.ExerciseId));
            })
            .OrderBy(x => x.ExerciseName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return Result<IReadOnlyList<WeightableExerciseDto>>.Success(list);
    }

    public async Task<Result> SetExercisePriorityAsync(Guid userId, Guid goalId, Guid exerciseId, int value, CancellationToken ct = default)
    {
        if (value < -2 || value > 2)
            return Result.Failure("Gewichtung muss zwischen −2 und +2 liegen.");

        var goal = await GetOwnedGoalAsync(userId, goalId, ct, track: false);
        if (goal is null)
            return Result.Failure("Ziel nicht gefunden.");
        if (goal.IsCustom)
            return Result.Failure("Ein individueller Plan wird nicht adaptiv generiert - eine Gewichtung hätte keine Wirkung.");

        var pool = await ResolvePlanCandidatesAsync(goal.SportId, goal.RegulationId, ct);
        if (pool.All(c => c.ExerciseId != exerciseId))
            return Result.Failure("Übung gehört nicht zu diesem Ziel.");

        await mastery.SetManualPriorityAsync(goal.DogId, exerciseId, value, ct);
        return Result.Success();
    }

    // Übungen der laufenden Planwoche (gleicher Wochen-Anker wie
    // RegenerateDuePlansAsync: volle Wochen seit Goal.CreatedAt).
    private HashSet<Guid> PlannedThisWeekExerciseIds(Goal goal)
    {
        if (goal.TrainingPlan is not { } plan || plan.Items.Count == 0)
            return [];

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var created = DateOnly.FromDateTime(goal.CreatedAt.UtcDateTime);
        var maxWeek = plan.Items.Max(i => i.WeekNumber);
        var currentWeek = Math.Clamp(Math.Max(0, (today.DayNumber - created.DayNumber) / 7) + 1, 1, Math.Max(1, maxWeek));

        return plan.Items
            .Where(i => i.WeekNumber == currentWeek && !i.IsRestWeek && i.ExerciseId is not null)
            .Select(i => i.ExerciseId!.Value)
            .ToHashSet();
    }

    // Leitner-Box -> grober Beherrschungs-Status für die Gewichtungs-Liste.
    private static int MasteryStatusOf(ExerciseMastery? m)
    {
        if (m is null || m.SessionCount == 0) return 0; // nie trainiert
        if (m.Box >= 4) return 3;                        // sitzt
        if (m.Box == 3) return 2;                        // mittel
        return 1;                                        // hängt (Box 1-2)
    }

    public async Task<int> RegenerateDuePlansAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        // Wochen-Kadenz: ein Ziel wird höchstens etwa wöchentlich neu generiert
        // (LastPlanGeneratedAt steuert die Frequenz, der Aufruf-Takt selbst darf
        // öfter sein).
        var cutoff = now.AddDays(-6);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var goals = await LoadGoalsQuery()
            .Where(g => g.Status == GoalStatus.Active && !g.IsCustom && g.TrainingPlan != null)
            .Where(g => g.LastPlanGeneratedAt == null || g.LastPlanGeneratedAt < cutoff)
            .ToListAsync(ct);

        var count = 0;
        foreach (var goal in goals)
        {
            // Zeitanker Goal.CreatedAt: aktuelle Woche = vergangene volle Wochen
            // seit Erstellung + 1. Nur die KOMMENDE Woche adaptiv frisch halten,
            // damit die laufende Woche nicht mitten im Training umgebaut wird.
            var created = DateOnly.FromDateTime(goal.CreatedAt.UtcDateTime);
            var currentWeek = Math.Max(0, (today.DayNumber - created.DayNumber) / 7) + 1;
            var targetWeek = currentWeek + 1;

            var maxWeek = goal.TrainingPlan!.Items.Count == 0 ? 0 : goal.TrainingPlan.Items.Max(i => i.WeekNumber);
            if (targetWeek > maxWeek)
                continue; // keine zukünftige Planwoche mehr (Ziel läuft aus)

            await RegenerateWeekCoreAsync(goal, targetWeek, ct);
            await NotifyOwnersOfAutoRegenerationAsync(goal, targetWeek, ct);
            count++;
        }

        return count;
    }

    // Nur der automatische (Scheduler-)Pfad benachrichtigt die Besitzer - beim
    // manuellen "Neu generieren" hat der Nutzer die Änderung selbst ausgelöst.
    private async Task NotifyOwnersOfAutoRegenerationAsync(Goal goal, int weekNumber, CancellationToken ct)
    {
        var dogName = await db.Dogs.Where(d => d.Id == goal.DogId).Select(d => d.Name).FirstOrDefaultAsync(ct) ?? "deinen Hund";
        var ownerIds = await db.DogOwners
            .Where(o => o.DogId == goal.DogId && o.Role == DogOwnerRole.Owner)
            .Select(o => o.UserId)
            .ToListAsync(ct);

        foreach (var ownerId in ownerIds)
            await notifications.CreateAsync(
                ownerId,
                $"Der Trainingsplan für {dogName} wurde für Woche {weekNumber} automatisch angepasst.",
                $"/dogs/{goal.DogId}",
                ct);
    }

    // Kern der Wochen-Regenerierung (Aufrufer stellt sicher: getracktes,
    // nicht-individuelles Ziel mit Plan). Erhält manuelle/Trainer-Items und
    // Auto-Items mit geloggtem Fortschritt, ersetzt nur fortschrittslose
    // Auto-Items durch eine frische, mastery-basierte Auswahl.
    private async Task RegenerateWeekCoreAsync(Goal goal, int weekNumber, CancellationToken ct)
    {
        var weekItems = goal.TrainingPlan!.Items
            .Where(i => i.WeekNumber == weekNumber && !i.IsRestWeek)
            .ToList();

        var autoItemIds = weekItems.Where(i => i.Source == PlanItemSource.Auto).Select(i => i.Id).ToList();
        var itemIdsWithLogs = autoItemIds.Count == 0
            ? new HashSet<Guid>()
            : (await db.TrainingExercises
                .Where(e => e.TrainingPlanItemId != null && autoItemIds.Contains(e.TrainingPlanItemId!.Value))
                .Select(e => e.TrainingPlanItemId!.Value)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

        var preserved = weekItems
            .Where(i => i.Source != PlanItemSource.Auto || itemIdsWithLogs.Contains(i.Id))
            .ToList();
        var removable = weekItems
            .Where(i => i.Source == PlanItemSource.Auto && !itemIdsWithLogs.Contains(i.Id))
            .ToList();

        foreach (var item in removable)
            item.DeletedAt = DateTimeOffset.UtcNow;

        var preservedExerciseIds = preserved.Where(i => i.ExerciseId is not null).Select(i => i.ExerciseId!.Value).ToHashSet();
        var candidates = await BuildAdaptiveCandidatesAsync(goal, preservedExerciseIds, ct);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var remainingSlots = Math.Max(0, goal.WeeklyExerciseCount - preserved.Count);
        if (remainingSlots > 0 && candidates.Count > 0)
        {
            var config = new AdaptivePlanConfig(remainingSlots, EffectiveDaysForWeek(goal, weekNumber));
            foreach (var generated in AdaptivePlanGenerator.GenerateWeek(today, weekNumber, candidates, config))
            {
                generated.TrainingPlanId = goal.TrainingPlan!.Id;
                // Über das DbSet, nicht die getrackte Navigation: sonst stuft EF
                // die neuen Items per Collection-Fixup als Modified statt Added
                // ein (siehe TrainingService.CreateAsync).
                db.TrainingPlanItems.Add(generated);
            }
        }

        goal.LastPlanGeneratedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    // Baut die Kandidatenliste für den adaptiven Generator: Katalog der Prüfung/
    // Sportart (wie bei der Erstgenerierung) angereichert um den Mastery-Zustand
    // je Übung des Hundes; bereits verplante (erhaltene) Übungen werden
    // ausgeschlossen.
    private async Task<List<AdaptiveCandidate>> BuildAdaptiveCandidatesAsync(Goal goal, HashSet<Guid> excludeExerciseIds, CancellationToken ct)
    {
        var pool = await ResolvePlanCandidatesAsync(goal.SportId, goal.RegulationId, ct);
        var mandatory = pool.Where(c => c.IsMandatory).ToList();
        if (mandatory.Count == 0) mandatory = pool;

        var exerciseIds = mandatory.Select(c => c.ExerciseId).ToList();
        var masteries = await db.ExerciseMasteries
            .Where(m => m.DogId == goal.DogId && exerciseIds.Contains(m.ExerciseId))
            .ToDictionaryAsync(m => m.ExerciseId, ct);

        return mandatory
            .Where(c => !excludeExerciseIds.Contains(c.ExerciseId))
            .Select(c =>
            {
                masteries.TryGetValue(c.ExerciseId, out var m);
                return new AdaptiveCandidate(
                    c.ExerciseId,
                    c.Name,
                    c.Difficulty,
                    m?.SessionCount ?? 0,
                    m?.RecentAvgRating ?? 0,
                    m?.DueAt is { } due ? DateOnly.FromDateTime(due.UtcDateTime) : null,
                    m?.ManualPriority ?? 0);
            })
            .ToList();
    }

    private IQueryable<Goal> LoadGoalsQuery() =>
        db.Goals
            .Include(g => g.TrainingPlan)
            .ThenInclude(p => p!.WeekConfigs)
            .Include(g => g.TrainingPlan)
            .ThenInclude(p => p!.Items)
            .ThenInclude(i => i.Exercise);

    // Effektive Trainingstage einer Woche: Pro-Woche-Überschreibung, sonst
    // der Plan-Default. Auf [1, 7] begrenzt.
    private static int EffectiveDaysForWeek(Goal goal, int weekNumber)
    {
        var days = goal.TrainingPlan?.WeekConfigs.FirstOrDefault(w => w.WeekNumber == weekNumber)?.TrainingDaysPerWeek
                   ?? goal.TrainingDaysPerWeek;
        return Math.Clamp(days, 1, 7);
    }

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
                TrainingExerciseId = e.Id,
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
                    .Select(l => new TrainingPlanItemLogDto(l.TrainingSessionId, l.TrainingExerciseId, l.Date, l.Rating, l.Success, l.Notes))
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
                            logs,
                            i.Reason,
                            i.DayIndex);
                    })
                    .ToList());

        var weekConfigs = g.TrainingPlan?.WeekConfigs
            .OrderBy(w => w.WeekNumber)
            .Select(w => new WeekConfigDto(w.WeekNumber, w.TrainingDaysPerWeek))
            .ToList() ?? new List<WeekConfigDto>();

        return new GoalDto(g.Id, g.DogId, g.SportId, sportName, g.RegulationId, regulationName, g.TargetDate, g.Status, g.Notes, g.IsCustom, g.WeeklyExerciseCount, g.TrainingDaysPerWeek, weekConfigs, planDto);
    }
}
