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
    Pending,

    /// <summary>
    /// Von einer Trainer:in eingeladen, von der Person selbst noch nicht
    /// angenommen.
    ///
    /// Bis 2026-09-21 war jedes per E-Mail hinzugefügte Mitglied sofort aktiv.
    /// Weil die Gruppentrainer:in sich danach selbst als Betreuer:in für die
    /// Hunde des Mitglieds eintragen kann (TrainerAssignment) und damit
    /// Tagebuch, Ziele und Fährten samt Standorten sieht, genügte die
    /// E-Mail-Adresse einer fremden Person, um an deren Daten zu kommen -
    /// jede:r darf eine vereinsfreie Gruppe anlegen. Eine Einladung zählt
    /// deshalb nirgends als Mitgliedschaft; erst das Annehmen macht daraus
    /// eine.
    /// </summary>
    Invited
}

/// <summary>
/// Mitgliedschaft eines Benutzers in einer Gruppe.
/// Status=Pending für Selbstbeitrittsanfragen (analog zu ClubMembership),
/// Status=Invited für Einladungen einer Trainer:in, Status=Active erst, wenn
/// beide Seiten zugestimmt haben (Anfrage freigegeben oder Einladung
/// angenommen).
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
