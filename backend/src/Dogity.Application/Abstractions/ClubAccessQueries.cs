using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Zentrale Sichtbarkeitsprüfung für vereinsspezifische Übungen
/// (<see cref="Domain.Sports.Exercise.ClubId"/>): sichtbar für Trainer, die
/// dem Verein per <see cref="Domain.Community.ClubTrainer"/> zugewiesen
/// sind, sowie für Mitglieder einer Gruppe dieses Vereins.
/// </summary>
public static class ClubAccessQueries
{
    public static async Task<HashSet<Guid>> GetVisibleClubIdsAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default)
    {
        var trainerClubIds = await db.ClubTrainers
            .Where(t => t.UserId == userId)
            .Select(t => t.ClubId)
            .ToListAsync(ct);

        var memberClubIds = await db.Groups
            .Where(g => g.ClubId != null && g.Members.Any(m => m.UserId == userId))
            .Select(g => g.ClubId!.Value)
            .ToListAsync(ct);

        return trainerClubIds.Concat(memberClubIds).ToHashSet();
    }

    public static Task<bool> IsClubTrainerAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId, ct);

    /// <summary>
    /// Ob jemand überhaupt irgendwo Trainer:in ist: als Hauptverantwortliche:r
    /// einer Gruppe, als weitere:r Trainer:in einer Gruppe oder als Trainer:in
    /// eines Vereins.
    ///
    /// Eine Definition an EINER Stelle, weil daran zwei Dinge hängen, die nicht
    /// auseinanderlaufen dürfen: die Trainer-Perspektive im Frontend und die
    /// Identity-Rolle TRAINER, die in der Admin-Übersicht angezeigt wird.
    /// </summary>
    public static async Task<bool> IsAnyTrainerAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default)
    {
        if (await db.Groups.AnyAsync(g => g.TrainerId == userId, ct)) return true;
        if (await db.GroupTrainers.AnyAsync(t => t.UserId == userId, ct)) return true;
        return await db.ClubTrainers.AnyAsync(t => t.UserId == userId, ct);
    }
}
