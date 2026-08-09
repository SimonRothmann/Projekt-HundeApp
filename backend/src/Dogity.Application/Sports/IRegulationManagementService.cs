using Dogity.Application.Common;

namespace Dogity.Application.Sports;

/// <summary>
/// Schreibender Zugriff auf Sportart-Stammdaten und Prüfungsordnungen:
/// ein Admin pflegt globale Sportarten/Prüfungsordnungen (Sport.ClubId = null),
/// ein für einen Verein zugewiesener Trainer (siehe ClubTrainer) die seines
/// Vereins. Die Berechtigungsprüfung erfolgt hier im Service (nicht per
/// Rollen-Attribut), da dieselbe Aktion je nach ClubId der Sportart
/// unterschiedliche Rollen erfordert - analog zu <see cref="IExerciseManagementService"/>.
/// Bearbeitet werden die Übungen der jeweils aktuellen (neuesten) Version.
/// </summary>
public interface IRegulationManagementService
{
    Task<Result<SportDto>> UpdateSportAsync(Guid actingUserId, bool isAdmin, Guid sportId, UpdateSportRequest request, CancellationToken ct = default);

    Task<Result<RegulationDto>> UpdateRegulationAsync(Guid actingUserId, bool isAdmin, Guid regulationId, UpdateRegulationRequest request, CancellationToken ct = default);

    Task<Result<RegulationExerciseDto>> AddRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, AddRegulationExerciseRequest request, CancellationToken ct = default);

    Task<Result> UpdateRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, Guid exerciseId, UpdateRegulationExerciseRequest request, CancellationToken ct = default);

    Task<Result> RemoveRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, Guid exerciseId, CancellationToken ct = default);
}
