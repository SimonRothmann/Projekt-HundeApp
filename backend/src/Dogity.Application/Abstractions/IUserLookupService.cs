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

    /// <summary>Sperrt einen Benutzer dauerhaft (kein Login mehr möglich). False, falls Benutzer nicht existiert.</summary>
    Task<bool> LockUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Hebt eine Sperrung auf. False, falls Benutzer nicht existiert.</summary>
    Task<bool> UnlockUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Löscht das Identity-Konto hart. False, falls Benutzer nicht existiert oder Löschung fehlschlägt.</summary>
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);
}
