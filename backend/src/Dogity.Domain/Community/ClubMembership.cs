using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

public enum ClubMembershipStatus
{
    Pending,
    Approved,
    Rejected,

    /// <summary>
    /// Von einer Trainer:in des Vereins per E-Mail eingeladen, von der Person
    /// selbst noch nicht angenommen.
    ///
    /// Bis 2026-10-15 nahm jede Vereinstrainer:in jede registrierte Person mit
    /// deren E-Mail-Adresse sofort auf. Ein Mitglied sehen aber alle
    /// Trainer:innen des Vereins mit Vor- und Nachname und E-Mail-Adresse, und
    /// die Verwaltung kann es zur Trainer:in machen - die Adresse einer
    /// fremden Person genügte also, um an ihren Namen zu kommen und sie in
    /// einen Verein zu ziehen, von dem sie nichts wusste. Eine Einladung zählt
    /// deshalb nirgends als Mitgliedschaft; erst das Annehmen macht daraus
    /// eine (Muster: <see cref="GroupMemberStatus.Invited"/>).
    /// </summary>
    Invited
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
    InviteLink,
    /// <summary>Von einer Trainer:in des Vereins per E-Mail-Adresse eingeladen.</summary>
    Invitation
}

/// <summary>
/// Allgemeine Vereinsmitgliedschaft eines Nutzers, unabhängig von
/// Trainingsgruppen (siehe <see cref="Group"/>/<see cref="GroupMember"/>,
/// die nur die Zuordnung zu einer einzelnen Trainingsgruppe abbilden, nicht
/// die Vereinszugehörigkeit selbst). Ein Nutzer fordert den Beitritt an
/// (Status Pending), ein Trainer des Vereins gibt frei (Approved) oder
/// lehnt ab (Rejected) - eine Zeile pro Nutzer/Verein deckt Anfrage und
/// aktive Mitgliedschaft über den Status ab, statt zwei Tabellen zu pflegen.
/// Umgekehrt lädt eine Trainer:in ein (Status Invited), und erst die Zusage der
/// Person macht daraus eine Mitgliedschaft.
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

    /// <summary>
    /// Bei einer Einladung: die eingegebene Adresse. Bis zur Zusage zeigt der
    /// Verein nur sie - nicht den Namen und nicht eine später geänderte Adresse.
    /// Mit der Antwort wird sie gelöscht.
    /// </summary>
    public string? InvitedEmail { get; set; }
}
