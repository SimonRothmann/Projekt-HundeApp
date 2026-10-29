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

    /// <summary>
    /// Die Token-Erneuerung der Anmeldung: <see cref="RotateAsync"/> plus
    /// Prüfung des Kontos. Ist es inzwischen gelöscht oder vom Admin dauerhaft
    /// gesperrt, gibt es keinen neuen Token, und alle Sitzungen des Nutzers
    /// enden. Die kurze Sperre nach falschen Passwörtern zählt hier NICHT (siehe
    /// <see cref="Sperren"/>).
    /// </summary>
    Task<RefreshRotationResult> RefreshAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Widerruft einen einzelnen Roh-Token (Logout auf diesem Gerät). Idempotent.</summary>
    Task RevokeAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Widerruft alle aktiven Tokens eines Nutzers (Admin-Sperre, "überall abmelden").</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Widerruft alle aktiven Tokens eines Nutzers bis auf den vorgelegten -
    /// nach einem Passwortwechsel: Wer das alte Passwort kannte und damit eine
    /// Sitzung hat, fliegt raus; das Gerät, auf dem gerade gewechselt wurde,
    /// bleibt angemeldet. Ein fremder oder ungültiger Token schützt nichts.
    /// </summary>
    Task RevokeAllExceptAsync(Guid userId, string? keepRawToken, CancellationToken ct = default);

    /// <summary>
    /// Löscht alle Tokens eines Nutzers endgültig (nicht weich, nicht nur
    /// widerrufen) - für die Kontolöschung: Die Zeilen hätten danach keinen
    /// Zweck mehr. Idempotent.
    /// </summary>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Löscht endgültig alle Tokens, deren Konto es nicht mehr gibt.
    /// Altbestand: Die Kontolöschung hat die Zeilen früher nur widerrufen. Läuft
    /// beim Start und ist idempotent.
    /// </summary>
    /// <returns>Wie viele Tokens entfernt wurden.</returns>
    Task<int> DeleteOrphanedAsync(CancellationToken ct = default);
}
