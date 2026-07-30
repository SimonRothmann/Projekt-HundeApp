using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record GroupDto(Guid Id, string Name, string? Description, Guid TrainerId, Guid? ClubId, int MemberCount, string? TrainerName = null);

public record GroupMemberDto(Guid UserId, string Email, string FirstName, string LastName, GroupMemberRole Role, DateTimeOffset JoinedAt);

/// <summary>Ein möglicher Gruppen-Trainer (alle Trainer:innen des Vereins der Gruppe).</summary>
public record GroupTrainerOptionDto(Guid UserId, string FirstName, string LastName, string Email);

public record GroupJoinRequestDto(Guid MemberId, string Email, string FirstName, string LastName, DateTimeOffset RequestedAt);

public record MemberDogDto(Guid Id, string Name, string? Breed, bool IsTrainerAssigned);

public record GroupDetailDto(GroupDto Group, IReadOnlyList<GroupMemberDto> Members);

public record CreateGroupRequest(string Name, string? Description, Guid? ClubId = null);

public record UpdateGroupRequest(string Name, string? Description);

public record AssignGroupTrainerRequest(Guid TrainerId);

public record AddMemberRequest(string Email);

public record AssignTrainerRequest(Guid MemberId, Guid DogId);
