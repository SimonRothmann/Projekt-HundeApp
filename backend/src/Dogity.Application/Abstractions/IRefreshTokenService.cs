namespace Dogity.Application.Abstractions;

/// <summary>Ergebnis eines Rotations-Versuchs: bei Erfolg der neue Roh-Token + UserId.</summary>
public record RefreshRotationResult(bool Succeeded, Guid UserId = default, string? NewRawToken = null);

/// <summary>
/// Verwaltet langlebige Refresh-Tokens (siehe Infrastructure RefreshToken /
/// TODO.md Roadmap 6). Ermöglicht, dass ein Nutzer eingeloggt bleibt, ohne
/// sich neu anmelden zu müssen, und macht Sessions serverseitig widerrufbar.
/// Implementierung lebt in Infrastructure (DB + Krypto).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Stellt einen neuen Refresh-Token für den Nutzer aus und gibt dessen Rohwert zurück (nur hier einsehbar).</summary>
    Task<string> IssueAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Prüft einen vorgelegten Roh-Token, entwertet ihn und stellt einen neuen
    /// aus (Rotation). Bei Wiedervorlage eines bereits entwerteten Tokens
    /// (Reuse/Diebstahl) werden alle aktiven Tokens des Nutzers widerrufen.
    /// </summary>
    Task<RefreshRotationResult> RotateAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Widerruft einen einzelnen Roh-Token (Logout auf diesem Gerät). Idempotent.</summary>
    Task RevokeAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Widerruft alle aktiven Tokens eines Nutzers (Admin-Sperre, "überall abmelden").</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
