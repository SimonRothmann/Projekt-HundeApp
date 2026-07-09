using Dogity.Application.Abstractions;

namespace Dogity.Application.Tests.TestSupport;

/// <summary>
/// Minimaler Fake für IUserLookupService in Tests, die nur ID-Lookups
/// brauchen (gibt Email/Name aus einem vorbefüllten Dictionary zurück).
/// Kein echtes Identity-System nötig.
/// </summary>
public class FakeUserLookupService : IUserLookupService
{
    private readonly Dictionary<Guid, (string Email, string FirstName, string LastName)> _users = [];

    public void Register(Guid id, string email, string firstName = "", string lastName = "")
        => _users[id] = (email, firstName, lastName);

    public Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var hit = _users.FirstOrDefault(kvp => kvp.Value.Email == email);
        return Task.FromResult<UserLookupResult?>(
            hit.Key == default ? null : new UserLookupResult(hit.Key, hit.Value.Email, hit.Value.FirstName, hit.Value.LastName));
    }

    public Task<IReadOnlyDictionary<Guid, UserLookupResult>> FindByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var result = userIds
            .Where(_users.ContainsKey)
            .ToDictionary(id => id, id => new UserLookupResult(id, _users[id].Email, _users[id].FirstName, _users[id].LastName));
        return Task.FromResult<IReadOnlyDictionary<Guid, UserLookupResult>>(result);
    }

    public Task<int> CountAsync(CancellationToken ct = default) => Task.FromResult(_users.Count);

    public Task<IReadOnlyList<UserDirectoryEntry>> ListAllAsync(CancellationToken ct = default)
    {
        var entries = _users.Select(kvp =>
            new UserDirectoryEntry(kvp.Key, kvp.Value.Email, kvp.Value.FirstName, kvp.Value.LastName, [], false))
            .ToList();
        return Task.FromResult<IReadOnlyList<UserDirectoryEntry>>(entries);
    }

    public Task<(IReadOnlyList<UserDirectoryEntry> Users, int TotalCount)> ListPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var all = _users.Select(kvp =>
            new UserDirectoryEntry(kvp.Key, kvp.Value.Email, kvp.Value.FirstName, kvp.Value.LastName, [], false))
            .OrderBy(u => u.Email)
            .ToList();
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<UserDirectoryEntry>, int)>((paged, all.Count));
    }

    public Task<bool> LockUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(_users.ContainsKey(userId));
    public Task<bool> UnlockUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(_users.ContainsKey(userId));
    public Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(_users.Remove(userId));

    public Task<IReadOnlyList<Guid>> ListUserIdsInRoleAsync(string role, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task<(bool Success, string[] Errors)> SetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
        => Task.FromResult(_users.ContainsKey(userId) ? (true, Array.Empty<string>()) : (false, new[] { "Benutzer nicht gefunden." }));
}
