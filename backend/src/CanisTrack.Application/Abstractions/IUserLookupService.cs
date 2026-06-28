namespace CanisTrack.Application.Abstractions;

public record UserLookupResult(Guid UserId, string Email, string FirstName, string LastName);

public record UserDirectoryEntry(Guid UserId, string Email, string FirstName, string LastName, string[] Roles);

/// <summary>
/// Sucht registrierte Benutzer anhand der E-Mail-Adresse (z.B. um einen
/// Trainer eine Gruppeneinladung an einen bestehenden Benutzer aussprechen
/// zu lassen). Implementierung lebt in Infrastructure (nutzt UserManager).
/// </summary>
public interface IUserLookupService
{
    Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, UserLookupResult>> FindByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserDirectoryEntry>> ListAllAsync(CancellationToken ct = default);
}
