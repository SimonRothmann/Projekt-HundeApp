using Dogity.Application.Common;

namespace Dogity.Application.Preferences;

public interface IPreferenceService
{
    Task<Result<UserPreferenceDto>> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result> UpdateModulesAsync(Guid userId, UpdateModulesRequest request, CancellationToken ct = default);
    Task<Result> UpdateSportsAsync(Guid userId, UpdateSportsRequest request, CancellationToken ct = default);
    Task<Result> UpdateLocaleAsync(Guid userId, UpdateLocaleRequest request, CancellationToken ct = default);

    /// <summary>
    /// Die Sportarten, die für diesen Hund tatsächlich gelten - Auswahl des
    /// Hundes, sonst die des Menschen, sonst alle.
    /// </summary>
    Task<Result<IReadOnlyList<Guid>>> GetEffectiveDogSportsAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    Task<Result> UpdateDogSportsAsync(Guid userId, Guid dogId, UpdateDogSportsRequest request, CancellationToken ct = default);
}
