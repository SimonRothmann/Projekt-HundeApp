using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record GroupDto(Guid Id, string Name, string? Description, Guid TrainerId, Guid? ClubId, int MemberCount);

public record GroupMemberDto(Guid UserId, string Email, string FirstName, string LastName, GroupMemberRole Role, DateTimeOffset JoinedAt);

public record GroupJoinRequestDto(Guid MemberId, string Email, string FirstName, string LastName, DateTimeOffset RequestedAt);

public record MemberDogDto(Guid Id, string Name, string? Breed, bool IsTrainerAssigned);

public record GroupDetailDto(GroupDto Group, IReadOnlyList<GroupMemberDto> Members);

public record CreateGroupRequest(string Name, string? Description, Guid? ClubId = null);

public record AddMemberRequest(string Email);

public record AssignTrainerRequest(Guid MemberId, Guid DogId);
