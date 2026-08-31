using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <summary>
/// Verhältnis der aufrufenden Person zu einer Gruppe. Ohne das zeigte die
/// Vereinsseite an JEDER Gruppe einen "Beitreten"-Knopf - auch der eigenen
/// Trainer:in und bestehenden Mitgliedern.
/// </summary>
public enum GroupRelation
{
    None,
    Pending,
    Member,
    Trainer
}

public record GroupDto(
    Guid Id,
    string Name,
    string? Description,
    Guid TrainerId,
    Guid? ClubId,
    int MemberCount,
    string? TrainerName = null,
    GroupRelation MyRelation = GroupRelation.None);

public record GroupMemberDto(Guid UserId, string Email, string FirstName, string LastName, GroupMemberRole Role, DateTimeOffset JoinedAt);

/// <summary>Ein möglicher Gruppen-Trainer (alle Trainer:innen des Vereins der Gruppe).</summary>
public record GroupTrainerOptionDto(Guid UserId, string FirstName, string LastName, string Email);

/// <summary>
/// Eine:r der Trainer:innen einer Gruppe. <paramref name="IsLead"/> markiert
/// die/den Hauptverantwortliche:n aus <see cref="Domain.Community.Group.TrainerId"/> -
/// alle anderen betreuen gleichberechtigt mit.
/// </summary>
public record GroupTrainerDto(Guid UserId, string Email, string FirstName, string LastName, bool IsLead);

public record GroupJoinRequestDto(Guid MemberId, string Email, string FirstName, string LastName, DateTimeOffset RequestedAt);

public record MemberDogDto(Guid Id, string Name, string? Breed, bool IsTrainerAssigned);

public record GroupDetailDto(GroupDto Group, IReadOnlyList<GroupMemberDto> Members, IReadOnlyList<GroupTrainerDto> Trainers);

public record CreateGroupRequest(string Name, string? Description, Guid? ClubId = null);

public record UpdateGroupRequest(string Name, string? Description);

public record AssignGroupTrainerRequest(Guid TrainerId);

/// <summary>Weitere:n Trainer:in per E-Mail-Adresse zur Gruppe hinzufügen.</summary>
public record AddGroupTrainerRequest(string Email);

public record AddMemberRequest(string Email);

public record AssignTrainerRequest(Guid MemberId, Guid DogId);
