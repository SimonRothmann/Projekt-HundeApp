using Dogity.Application.Common;

namespace Dogity.Application.Sports;

/// <summary>
/// Liest den daten-getriebenen Sportarten-Katalog (siehe DATABASE.md
/// "Sportmodell" - keine Sportart wird hartcodiert). Globale Sportarten/
/// Prüfungsordnungen pflegt nur ein Admin, vereinsspezifische Übungen
/// (Exercise.ClubId) ein zugewiesener Vereinstrainer - siehe IExerciseManagementService.
/// </summary>
public interface ISportCatalogService
{
    /// <summary>
    /// Globale Sportarten plus vereinsspezifische Sportarten der Vereine,
    /// in denen der Nutzer Trainer oder aktives Mitglied ist. Für nicht
    /// eingeloggte Aufrufe (userId=null) nur globale Sportarten.
    /// </summary>
    Task<Result<IReadOnlyList<SportDto>>> GetSportsAsync(Guid? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Erstellt eine neue Sportart. Globaler Katalog nur für Admins;
    /// vereinsspezifische Sportarten für zugewiesene Vereinstrainer.
    /// </summary>
    Task<Result<SportDto>> CreateSportAsync(Guid actingUserId, bool isAdmin, CreateSportRequest request, CancellationToken ct = default);

    /// <summary>
    /// Liefert globale Übungen plus vereinsspezifische Übungen der Vereine,
    /// denen der Nutzer als Trainer oder Gruppenmitglied zugeordnet ist.
    /// </summary>
    Task<Result<IReadOnlyList<ExerciseDto>>> GetExercisesAsync(Guid sportId, Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RegulationDto>>> GetRegulationsAsync(Guid sportId, CancellationToken ct = default);
    Task<Result<RegulationDetailDto>> GetRegulationDetailAsync(Guid regulationId, CancellationToken ct = default);
}
