using CanisTrack.Application.Common;

namespace CanisTrack.Application.Sports;

/// <summary>
/// Schreibender Zugriff auf den Übungskatalog: ein Admin pflegt globale
/// Übungen (ClubId = null), ein für einen Verein zugewiesener Trainer
/// (siehe ClubTrainer) ausschließlich vereinsspezifische Übungen seines
/// Vereins. Die Berechtigungsprüfung erfolgt hier im Service (nicht per
/// Rollen-Attribut am Controller), da dieselbe Aktion je nach ClubId
/// unterschiedliche Rollen erfordert.
/// </summary>
public interface IExerciseManagementService
{
    Task<Result<ExerciseDto>> CreateAsync(Guid actingUserId, bool isAdmin, CreateExerciseRequest request, CancellationToken ct = default);
    Task<Result<ExerciseDto>> UpdateAsync(Guid actingUserId, bool isAdmin, Guid exerciseId, UpdateExerciseRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid actingUserId, bool isAdmin, Guid exerciseId, CancellationToken ct = default);
}
