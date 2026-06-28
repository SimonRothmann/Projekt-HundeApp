using CanisTrack.Application.Common;

namespace CanisTrack.Application.Dogs;

public interface IDogService
{
    Task<Result<IReadOnlyList<DogDto>>> GetMyDogsAsync(Guid userId, CancellationToken ct = default);
    Task<Result<DogDto>> GetByIdAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result<DogDto>> CreateAsync(Guid userId, CreateDogRequest request, CancellationToken ct = default);
    Task<Result<DogDto>> UpdateAsync(Guid userId, Guid dogId, UpdateDogRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid dogId, CancellationToken ct = default);
}
