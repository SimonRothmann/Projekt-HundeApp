using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

public enum GroupMemberRole
{
    Member,
    Trainer
}

public enum GroupMemberStatus
{
    Active,
    Pending
}

/// <summary>
/// Mitgliedschaft eines Benutzers in einer Gruppe.
/// Status=Pending für Selbstbeitrittsanfragen (analog zu ClubMembership),
/// Status=Active für vom Trainer aufgenommene oder freigegebene Mitglieder.
/// </summary>
public class GroupMember : Entity
{
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    public Guid UserId { get; set; }
    public GroupMemberRole Role { get; set; } = GroupMemberRole.Member;
    public GroupMemberStatus Status { get; set; } = GroupMemberStatus.Active;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
