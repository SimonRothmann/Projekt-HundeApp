using Dogity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Infrastructure.Identity;

public class UserLookupService(UserManager<ApplicationUser> userManager) : IUserLookupService
{
    public async Task<UserLookupResult?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        return user is null ? null : new UserLookupResult(user.Id, user.Email!, user.FirstName, user.LastName);
    }

    public async Task<IReadOnlyDictionary<Guid, UserLookupResult>> FindByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        return await userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserLookupResult(u.Id, u.Email!, u.FirstName, u.LastName))
            .ToDictionaryAsync(r => r.UserId, ct);
    }

    public Task<int> CountAsync(CancellationToken ct = default) => userManager.Users.CountAsync(ct);

    public async Task<IReadOnlyList<UserDirectoryEntry>> ListAllAsync(CancellationToken ct = default)
    {
        var users = await userManager.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var entries = new List<UserDirectoryEntry>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            entries.Add(new UserDirectoryEntry(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToArray()));
        }

        return entries;
    }
}
