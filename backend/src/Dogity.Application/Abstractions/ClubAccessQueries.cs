using Dogity.Domain.Community;
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
    /// Ob jemand diesen Verein VERWALTEN darf - Stammdaten ändern,
    /// Trainer:innen berufen und abberufen.
    ///
    /// Eine Definition an einer Stelle, weil sonst jede neue verwaltende
    /// Aktion ihre eigene Auslegung mitbrächte. Trainer:innen mit der Rolle
    /// Training sind ausdrücklich NICHT gemeint: Dürften sie andere
    /// abberufen, könnte auch die Person entfernt werden, die den Verein
    /// angelegt hat.
    /// </summary>
    public static Task<bool> CanManageClubAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId && t.Role == ClubRole.Verwaltung, ct);

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
    /// <summary>
    /// Ob jemand zu irgendeinem Verein gehört - als freigegebenes Mitglied
    /// ODER als Vereinstrainer:in.
    ///
    /// Der Zusatz ist wichtig: Trainer:innen bekommen beim Zuweisen KEINE
    /// Mitgliedschaftszeile (siehe ClubService.AssignTrainerAsync), sie stehen
    /// in einer eigenen Tabelle. Wer nur auf ClubMemberships schaut, hält eine
    /// Vereinstrainerin für vereinslos - und fordert sie auf, einem Verein
    /// beizutreten, den sie leitet.
    /// </summary>
    public static async Task<bool> BelongsToAnyClubAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default)
    {
        if (await db.ClubTrainers.AnyAsync(t => t.UserId == userId, ct)) return true;
        return await db.ClubMemberships
            .AnyAsync(m => m.UserId == userId && m.Status == ClubMembershipStatus.Approved, ct);
    }

    /// <summary>
    /// Ob jemand zu irgendeiner Trainingsgruppe gehört - als aktives Mitglied,
    /// als Hauptverantwortliche:r oder als weitere:r Trainer:in.
    ///
    /// Dieselbe Falle wie oben: Wer eine Gruppe leitet, ist kein "Mitglied"
    /// im Sinne von GroupMembers.
    /// </summary>
    public static async Task<bool> BelongsToAnyGroupAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default)
    {
        if (await db.Groups.AnyAsync(g => g.TrainerId == userId, ct)) return true;
        if (await db.GroupTrainers.AnyAsync(t => t.UserId == userId, ct)) return true;
        return await db.GroupMembers
            .AnyAsync(m => m.UserId == userId && m.Status == GroupMemberStatus.Active, ct);
    }

}
