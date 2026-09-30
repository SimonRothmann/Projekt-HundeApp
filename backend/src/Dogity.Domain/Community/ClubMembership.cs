using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

public enum ClubMembershipStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Auf welchem Weg die Beitrittsanfrage entstanden ist. Nur zur Anzeige für
/// die Trainer:innen und zur Auswertung - an den Rechten ändert es nichts.
/// </summary>
public enum ClubMembershipSource
{
    /// <summary>Aus der Vereinsliste angefragt (oder direkt aufgenommen).</summary>
    Directory,
    /// <summary>Über den Einladungslink bzw. QR-Code des Vereins.</summary>
    InviteLink
}

/// <summary>
/// Allgemeine Vereinsmitgliedschaft eines Nutzers, unabhängig von
/// Trainingsgruppen (siehe <see cref="Group"/>/<see cref="GroupMember"/>,
/// die nur die Zuordnung zu einer einzelnen Trainingsgruppe abbilden, nicht
/// die Vereinszugehörigkeit selbst). Ein Nutzer fordert den Beitritt an
/// (Status Pending), ein Trainer des Vereins gibt frei (Approved) oder
/// lehnt ab (Rejected) - eine Zeile pro Nutzer/Verein deckt Anfrage und
/// aktive Mitgliedschaft über den Status ab, statt zwei Tabellen zu pflegen.
/// </summary>
public class ClubMembership : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public Guid UserId { get; set; }
    public ClubMembershipStatus Status { get; set; } = ClubMembershipStatus.Pending;
    public ClubMembershipSource Source { get; set; } = ClubMembershipSource.Directory;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
}
