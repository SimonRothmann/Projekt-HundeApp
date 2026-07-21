using Dogity.Application.Common;

namespace Dogity.Application.Stats;

public interface IStatsService
{
    Task<Result<DashboardStatsDto>> GetDashboardAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Übungs-Kennzahlen eines Hundes (Ø-Bewertung, Erfolgsquote, Trend),
    /// schwächste zuerst. Zugriff: Besitzer oder zugewiesener Trainer des Hundes.
    /// </summary>
    Task<Result<IReadOnlyList<DogExerciseStatDto>>> GetDogExerciseStatsAsync(Guid userId, Guid dogId, CancellationToken ct = default);
}
