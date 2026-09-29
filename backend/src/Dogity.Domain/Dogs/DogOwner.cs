using Dogity.Domain.Common;

namespace Dogity.Domain.Dogs;

public enum DogOwnerRole
{
    Owner,
    Trainer
}

public enum DogOwnerStatus
{
    Active,

    /// <summary>
    /// Von einer Besitzer:in eingeladen, von der Person selbst noch nicht
    /// angenommen. Solange gilt die Zeile nirgends - kein Zugriff, kein Hund
    /// in der eigenen Liste; der globale Filter in ApplicationDbContext blendet
    /// sie aus.
    ///
    /// Bis 2026-09-28 wurde man per E-Mail-Adresse sofort Mitbesitzer:in -
    /// ohne es zu erfahren. Die Besitzerliste zeigte danach Vor- und Nachnamen:
    /// Wer wissen wollte, wem eine Adresse gehört, trug sie ein, las die Liste
    /// und entfernte die Person wieder, ohne Spur. Und fremde Hunde samt
    /// Fährten landeten ungefragt im eigenen Konto.
    /// </summary>
    Invited
}

/// <summary>
/// Verknüpft einen Benutzer (UserId verweist auf die Identity-Tabelle in
/// Dogity.Infrastructure, bewusst ohne Navigationseigenschaft, damit
/// das Domain-Projekt keine Abhängigkeit zu ASP.NET Identity bekommt)
/// mit einem Hund. Mehrere Benutzer können denselben Hund verwalten.
/// </summary>
public class DogOwner : Entity
{
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    public Guid UserId { get; set; }
    public DogOwnerRole Role { get; set; } = DogOwnerRole.Owner;
    public DogOwnerStatus Status { get; set; } = DogOwnerStatus.Active;

    /// <summary>Wer eingeladen hat - für die Einladungskarte ("Anna lädt dich ein").</summary>
    public Guid? InvitedByUserId { get; set; }

    /// <summary>
    /// Die beim Einladen eingegebene Adresse. Bis zur Zusage sehen die
    /// Besitzer:innen nur sie - nicht eine inzwischen geänderte Adresse.
    /// </summary>
    public string? InvitedEmail { get; set; }
}
