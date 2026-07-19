using Dogity.Application.Common;

namespace Dogity.Application.Tracking;

public interface IGpsTrackService
{
    Task<Result<IReadOnlyList<GpsTrackDto>>> GetByTrainingSessionAsync(Guid userId, Guid trainingSessionId, CancellationToken ct = default);
    Task<Result<GpsTrackDto>> CreateAsync(Guid userId, CreateGpsTrackRequest request, CancellationToken ct = default);
    Task<Result<GpsWalkRunDto>> AddWalkRunAsync(Guid userId, Guid trackId, CreateGpsWalkRunRequest request, CancellationToken ct = default);
    Task<Result<GpsWalkRunDto>> UpdateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, UpdateGpsWalkRunRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid trackId, CancellationToken ct = default);
}
