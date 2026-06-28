using CanisTrack.Application.Common;

namespace CanisTrack.Application.Sports;

/// <summary>
/// Liest den daten-getriebenen Sportarten-Katalog (siehe DATABASE.md
/// "Sportmodell" - keine Sportart wird hartcodiert). Globale Sportarten/
/// Prüfungsordnungen pflegt nur ein Admin, vereinsspezifische Übungen
/// (Exercise.ClubId) ein zugewiesener Vereinstrainer - siehe IExerciseManagementService.
/// </summary>
public interface ISportCatalogService
{
    Task<Result<IReadOnlyList<SportDto>>> GetSportsAsync(CancellationToken ct = default);

    /// <summary>
    /// Liefert globale Übungen plus vereinsspezifische Übungen der Vereine,
    /// denen der Nutzer als Trainer oder Gruppenmitglied zugeordnet ist.
    /// </summary>
    Task<Result<IReadOnlyList<ExerciseDto>>> GetExercisesAsync(Guid sportId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RegulationDto>>> GetRegulationsAsync(Guid sportId, CancellationToken ct = default);
    Task<Result<RegulationDetailDto>> GetRegulationDetailAsync(Guid regulationId, CancellationToken ct = default);
}
