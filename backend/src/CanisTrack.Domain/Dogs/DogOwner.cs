using CanisTrack.Domain.Common;

namespace CanisTrack.Domain.Dogs;

public enum DogOwnerRole
{
    Owner,
    Trainer
}

/// <summary>
/// Verknüpft einen Benutzer (UserId verweist auf die Identity-Tabelle in
/// CanisTrack.Infrastructure, bewusst ohne Navigationseigenschaft, damit
/// das Domain-Projekt keine Abhängigkeit zu ASP.NET Identity bekommt)
/// mit einem Hund. Mehrere Benutzer können denselben Hund verwalten.
/// </summary>
public class DogOwner : Entity
{
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    public Guid UserId { get; set; }
    public DogOwnerRole Role { get; set; } = DogOwnerRole.Owner;
}
