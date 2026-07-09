namespace Dogity.Application.Abstractions;

public record UserLookupResult(Guid UserId, string Email, string FirstName, string LastName);

public record UserDirectoryEntry(Guid UserId, string Email, string FirstName, string LastName, string[] Roles, bool IsLockedOut);

/// <summary>
/// Sucht registrierte Benutzer anhand der E-Mail-Adresse (z.B. um einen
/// Trainer eine Gruppeneinladung an einen bestehenden Benutzer aussprechen
/// zu lassen) und deckt Admin-Nutzerverwaltung ab (sperren/entsperren/
/// löschen). Implementierung lebt in Infrastructure (nutzt UserManager) -
/// Application bleibt dadurch frei von einer direkten Identity-Abhängigkeit.
/// </summary>
public interface IUserLookupService
{
    Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, UserLookupResult>> FindByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserDirectoryEntry>> ListAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<UserDirectoryEntry> Users, int TotalCount)> ListPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>Sperrt einen Benutzer dauerhaft (kein Login mehr möglich). False, falls Benutzer nicht existiert.</summary>
    Task<bool> LockUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Hebt eine Sperrung auf. False, falls Benutzer nicht existiert.</summary>
    Task<bool> UnlockUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Löscht das Identity-Konto hart. False, falls Benutzer nicht existiert oder Löschung fehlschlägt.</summary>
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>UserIds aller Benutzer in der angegebenen Rolle (z.B. um alle Admins zu benachrichtigen).</summary>
    Task<IReadOnlyList<Guid>> ListUserIdsInRoleAsync(string role, CancellationToken ct = default);

    /// <summary>
    /// Setzt das Passwort eines Benutzers administrativ neu (ohne den
    /// Token-basierten Self-Service-Reset). Gibt bei Fehlschlag die
    /// Identity-Fehlermeldungen zurück (z.B. Passwortrichtlinie verletzt).
    /// </summary>
    Task<(bool Success, string[] Errors)> SetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);
}
