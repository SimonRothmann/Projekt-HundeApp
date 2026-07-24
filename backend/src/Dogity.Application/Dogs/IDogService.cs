using Dogity.Application.Common;

namespace Dogity.Application.Dogs;

public interface IDogService
{
    Task<Result<IReadOnlyList<DogDto>>> GetMyDogsAsync(Guid userId, CancellationToken ct = default);
    Task<Result<DogDto>> GetByIdAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result<DogDto>> CreateAsync(Guid userId, CreateDogRequest request, CancellationToken ct = default);
    Task<Result<DogDto>> UpdateAsync(Guid userId, Guid dogId, UpdateDogRequest request, CancellationToken ct = default);
    Task<Result> SetArchivedAsync(Guid userId, Guid dogId, bool archived, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DogOwnerDto>>> GetOwnersAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result> AddOwnerAsync(Guid userId, Guid dogId, AddDogOwnerRequest request, CancellationToken ct = default);
    Task<Result> RemoveOwnerAsync(Guid userId, Guid dogId, Guid targetUserId, CancellationToken ct = default);
}
