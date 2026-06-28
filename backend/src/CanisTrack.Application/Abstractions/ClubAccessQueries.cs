using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Application.Abstractions;

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
}
