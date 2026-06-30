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
}
