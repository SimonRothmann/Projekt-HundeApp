using CanisTrack.Domain.Common;

namespace CanisTrack.Domain.Community;

public enum GroupMemberRole
{
    Member,
    Trainer
}

/// <summary>
/// Mitgliedschaft eines Benutzers in einer Gruppe
/// (siehe DATABASE.md "group_members": group_id, user_id, role, joined_at).
/// </summary>
public class GroupMember : Entity
{
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    public Guid UserId { get; set; }
    public GroupMemberRole Role { get; set; } = GroupMemberRole.Member;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
