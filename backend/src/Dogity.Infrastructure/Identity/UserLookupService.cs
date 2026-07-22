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

    public async Task<(IReadOnlyList<UserDirectoryEntry> Users, int TotalCount)> ListPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var totalCount = await userManager.Users.CountAsync(ct);
        var rawUsers = await userManager.Users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var now = timeProvider.GetUtcNow();
        var entries = new List<UserDirectoryEntry>(rawUsers.Count);
        foreach (var user in rawUsers)
        {
            var roles = await userManager.GetRolesAsync(user);
            var isLockedOut = user.LockoutEnd is not null && user.LockoutEnd > now;
            entries.Add(new UserDirectoryEntry(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToArray(), isLockedOut));
        }
        return (entries, totalCount);
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

    public async Task<IReadOnlyList<Guid>> ListUserIdsInRoleAsync(string role, CancellationToken ct = default)
    {
        var users = await userManager.GetUsersInRoleAsync(role);
        return users.Select(u => u.Id).ToList();
    }

    public async Task<(bool Success, string[] Errors)> SetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return (false, ["Benutzer nicht gefunden."]);

        // Token statt RemovePassword+AddPassword: ResetPasswordAsync erzeugt
        // einen gültigen Reset-Token und wendet ihn sofort an - so läuft die
        // Passwortrichtlinien-Prüfung genau wie beim Self-Service-Reset, und
        // ein etwaiger Lockout wird zurückgesetzt (SecurityStamp erneuert).
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }
}
