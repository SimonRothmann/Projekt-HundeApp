using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Vereins-Trainingsbibliothek (siehe docs/GROUP_TRAINING_LIBRARY.md):
/// verein-weit geteilte, wiederverwendbare Übungs-Bausteine und daraus
/// zusammengestellte Einheiten. Jede:r Vereinstrainer:in (ClubTrainer) darf
/// alles sehen, anlegen, bearbeiten und löschen.
/// </summary>
public interface IGroupTrainingService
{
    Task<Result<GroupTrainingLibraryDto>> GetLibraryAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// Übernimmt den fachlichen Best-Practice-Starterkatalog in die Bibliothek
    /// des Vereins (siehe <see cref="GroupTrainingStarterCatalog"/>). Idempotent
    /// auf Titel-Ebene: bereits vorhandene Bausteine/Einheiten werden nicht
    /// dupliziert. Gibt die aktualisierte Bibliothek zurück.
    /// </summary>
    Task<Result<GroupTrainingLibraryDto>> ImportStarterCatalogAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    Task<Result<GroupTrainingExerciseDto>> CreateExerciseAsync(Guid userId, Guid clubId, UpsertExerciseRequest request, CancellationToken ct = default);
    Task<Result<GroupTrainingExerciseDto>> UpdateExerciseAsync(Guid userId, Guid exerciseId, UpsertExerciseRequest request, CancellationToken ct = default);
    Task<Result> DeleteExerciseAsync(Guid userId, Guid exerciseId, CancellationToken ct = default);

    Task<Result<GroupTrainingUnitDto>> CreateUnitAsync(Guid userId, Guid clubId, UpsertUnitRequest request, CancellationToken ct = default);
    Task<Result<GroupTrainingUnitDto>> UpdateUnitAsync(Guid userId, Guid unitId, UpsertUnitRequest request, CancellationToken ct = default);
    Task<Result> DeleteUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default);
    Task<Result<GroupTrainingUnitDto>> DuplicateUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default);
}
