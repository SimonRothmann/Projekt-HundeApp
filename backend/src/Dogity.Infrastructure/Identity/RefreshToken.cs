namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Ein langlebiger Refresh-Token, mit dem der Client lautlos einen neuen
/// kurzlebigen Access-Token (JWT) holt, ohne dass sich der Nutzer neu
/// anmelden muss (siehe TODO.md Roadmap 6). Gespeichert wird nur der
/// SHA-256-Hash des Tokens - der Rohwert existiert nur einmalig in der
/// Login-/Refresh-Antwort, sodass ein DB-Leak keine gültigen Tokens preisgibt.
///
/// Rotation: Jeder Refresh entwertet den benutzten Token (RevokedAt gesetzt)
/// und stellt einen neuen aus (ReplacedByTokenHash verweist darauf). Wird ein
/// bereits entwerteter Token erneut vorgelegt (Reuse/Diebstahl), werden alle
/// aktiven Tokens des Nutzers widerrufen - siehe RefreshTokenService.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>SHA-256-Hash (Base64) des Roh-Tokens - nie der Rohwert selbst.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Gesetzt, sobald der Token benutzt (rotiert) oder aktiv widerrufen wurde.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Hash des Nachfolge-Tokens (nur bei Rotation gesetzt) - für die Reuse-Erkennung.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
