using Dogity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Infrastructure.Identity;

public class UserLookupService(UserManager<ApplicationUser> userManager, TimeProvider timeProvider) : IUserLookupService
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
        var now = timeProvider.GetUtcNow();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var isLockedOut = user.LockoutEnd is not null && user.LockoutEnd > now;
            entries.Add(new UserDirectoryEntry(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToArray(), isLockedOut));
        }

        return entries;
    }

    public async Task<bool> LockUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return false;

        await userManager.SetLockoutEnabledAsync(user, true);
        var result = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        return result.Succeeded;
    }

    public async Task<bool> UnlockUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return false;

        var result = await userManager.SetLockoutEndDateAsync(user, null);
        return result.Succeeded;
    }

    public async Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return false;

        var result = await userManager.DeleteAsync(user);
        return result.Succeeded;
    }
}
