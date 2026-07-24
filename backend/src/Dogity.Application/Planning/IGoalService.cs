using Dogity.Application.Common;
using Dogity.Domain.Planning;

namespace Dogity.Application.Planning;

public interface IGoalService
{
    Task<Result<IReadOnlyList<GoalDto>>> GetByDogAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result<GoalDto>> GetByIdAsync(Guid userId, Guid goalId, CancellationToken ct = default);
    Task<Result<GoalDto>> CreateAsync(Guid userId, CreateGoalRequest request, CancellationToken ct = default);
    Task<Result<GoalDto>> UpdateStatusAsync(Guid userId, Guid goalId, GoalStatus status, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid goalId, CancellationToken ct = default);

    /// <summary>
    /// Fügt dem Plan manuell ein weiteres Wochenziel hinzu (siehe TODO.md
    /// "Trainingsplan überarbeitet") - z.B. eine zweite Übung in derselben
    /// Woche oder eine zusätzliche Übungseinheit. Ersetzt einen reinen
    /// Pausenwochen-Platzhalter in der Zielwoche, falls vorhanden.
    /// </summary>
    Task<Result<GoalDto>> AddPlanItemAsync(Guid userId, Guid goalId, AddTrainingPlanItemRequest request, CancellationToken ct = default);
    Task<Result<GoalDto>> UpdatePlanItemAsync(Guid userId, Guid goalId, Guid itemId, UpdateTrainingPlanItemRequest request, CancellationToken ct = default);
    Task<Result<GoalDto>> RemovePlanItemAsync(Guid userId, Guid goalId, Guid itemId, CancellationToken ct = default);

    /// <summary>
    /// Generiert eine Woche des Plans adaptiv neu (siehe
    /// docs/SMART_TRAINING_PLAN.md, P4). Erhält manuelle/Trainer-Items und
    /// Auto-Items mit geloggtem Fortschritt; ersetzt nur fortschrittslose
    /// Auto-Items durch eine frische, mastery-basierte Auswahl.
    /// </summary>
    Task<Result<GoalDto>> RegenerateWeekAsync(Guid userId, Guid goalId, int weekNumber, CancellationToken ct = default);

    /// <summary>
    /// System-Pass (kein Benutzerkontext): regeneriert für alle aktiven,
    /// nicht-individuellen Ziele die KOMMENDE Woche adaptiv neu, sofern sie
    /// fällig ist (LastPlanGeneratedAt null oder älter als ~6 Tage). Wird vom
    /// Hintergrund-Scheduler aufgerufen. Gibt die Anzahl regenerierter Ziele
    /// zurück.
    /// </summary>
    Task<int> RegenerateDuePlansAsync(CancellationToken ct = default);
}
