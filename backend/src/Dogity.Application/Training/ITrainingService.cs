using Dogity.Application.Common;

namespace Dogity.Application.Training;

public interface ITrainingService
{
    Task<Result<IReadOnlyList<TrainingSessionDto>>> GetByDogAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result<TrainingSessionDto>> GetByIdAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<Result<TrainingSessionDto>> CreateAsync(Guid userId, CreateTrainingSessionRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>Nur für Trainer mit TrainerAssignment auf den Hund - nicht für den Besitzer selbst.</summary>
    Task<Result> SetFeedbackAsync(Guid trainerId, Guid sessionId, SetFeedbackRequest request, CancellationToken ct = default);
}
