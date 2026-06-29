namespace Dogity.Domain.Common;

/// <summary>
/// Basisklasse für alle Domain-Entitäten. Enthält nur Konventionen aus
/// DATABASE.md (UUID-Id, created_at/updated_at, Soft Delete) - keine
/// Datenbank- oder Frameworkabhängigkeiten.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}
