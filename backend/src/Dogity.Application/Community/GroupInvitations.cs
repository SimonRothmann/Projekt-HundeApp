using Dogity.Application.Abstractions;
using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <summary>Was <see cref="GroupInvitations.PrepareAsync"/> mit der Person und der Gruppe gemacht hat.</summary>
public enum GroupInvitationOutcome
{
    /// <summary>Eine Einladung wurde angelegt - Mitglied wird die Person erst mit ihrer Zusage.</summary>
    Invited,

    /// <summary>Die Person hatte selbst um Aufnahme gebeten - jetzt aktiv, eine Einladung braucht es nicht mehr.</summary>
    JoinedDirectly,

    /// <summary>Die Person ist schon aktives Mitglied; nichts geändert.</summary>
    AlreadyMember,

    /// <summary>Die Person ist schon eingeladen; nichts geändert.</summary>
    AlreadyInvited,
}

/// <summary>
/// Der gemeinsame Kern von "jemanden in eine Gruppe einladen" - für die
/// Einladung per E-Mail (<see cref="GroupService.AddMemberAsync"/>) und für die
/// Freigabe eines Vereinsbeitritts mit Gruppe (<see cref="ClubService.DecideJoinRequestAsync"/>).
///
/// Eine Einladung, keine Aufnahme: Mitglied wird erst, wer annimmt (siehe
/// <see cref="GroupMemberStatus.Invited"/>), weil Gruppenmitgliedschaft den
/// Trainer:innen den Weg zur Betreuung der Hunde öffnet. Ein Vereinsbeitritt
/// ist keine Zustimmung zur Gruppe - deshalb gilt dieselbe Regel auch dort.
/// </summary>
public static class GroupInvitations
{
    /// <summary>
    /// Legt die Einladung an (oder nimmt eine eigene Anfrage an), OHNE zu
    /// speichern. So kann der Aufrufer sie zusammen mit seiner eigenen
    /// Änderung in einem Rutsch speichern - alles oder nichts.
    /// </summary>
    public static async Task<GroupInvitationOutcome> PrepareAsync(
        IApplicationDbContext db, Guid groupId, Guid userId, string? invitedEmail, CancellationToken ct = default)
    {
        // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index, siehe
        // SoftDeleteRevival) - sonst scheitert das erneute Aufnehmen eines
        // zuvor entfernten Mitglieds mit einem 500er.
        var (existing, isActive) = await db.GroupMembers
            .FindIncludingRemovedAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
        if (isActive)
        {
            switch (existing!.Status)
            {
                case GroupMemberStatus.Active:
                    return GroupInvitationOutcome.AlreadyMember;
                case GroupMemberStatus.Invited:
                    return GroupInvitationOutcome.AlreadyInvited;
                default:
                    // Die Person hat selbst um Aufnahme gebeten - beide Seiten
                    // wollen es, also gleich aufnehmen statt noch einmal
                    // einzuladen.
                    existing.Status = GroupMemberStatus.Active;
                    existing.JoinedAt = DateTimeOffset.UtcNow;
                    return GroupInvitationOutcome.JoinedDirectly;
            }
        }

        // Das gilt auch für jemanden, der früher schon einmal Mitglied war -
        // wer gegangen ist oder entfernt wurde, hat damit nicht für immer
        // zugestimmt.
        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.Status = GroupMemberStatus.Invited;
            existing.JoinedAt = DateTimeOffset.UtcNow;
            existing.InvitedEmail = invitedEmail;
        }
        else
        {
            db.GroupMembers.Add(new GroupMember
            {
                GroupId = groupId,
                UserId = userId,
                Status = GroupMemberStatus.Invited,
                InvitedEmail = invitedEmail,
            });
        }

        return GroupInvitationOutcome.Invited;
    }
}
