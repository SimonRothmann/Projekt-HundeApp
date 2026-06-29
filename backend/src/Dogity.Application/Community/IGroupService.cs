using Dogity.Application.Common;

namespace Dogity.Application.Community;

public interface IGroupService
{
    Task<Result<IReadOnlyList<GroupDto>>> GetMyGroupsAsync(Guid trainerId, CancellationToken ct = default);
    Task<bool> IsTrainerAsync(Guid userId, CancellationToken ct = default);
    Task<Result<GroupDetailDto>> GetDetailAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task<Result<GroupDto>> CreateAsync(Guid trainerId, CreateGroupRequest request, CancellationToken ct = default);
    Task<Result> AddMemberAsync(Guid trainerId, Guid groupId, AddMemberRequest request, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid trainerId, Guid groupId, Guid memberId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MemberDogDto>>> GetMemberDogsAsync(Guid trainerId, Guid groupId, Guid memberId, CancellationToken ct = default);
    Task<Result> AssignTrainerToDogAsync(Guid trainerId, Guid groupId, AssignTrainerRequest request, CancellationToken ct = default);
}
