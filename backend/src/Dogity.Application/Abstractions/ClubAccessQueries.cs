using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Zentrale Rechteprüfung rund um Vereine - EINE Definition je Recht, damit
/// Services keine Rollen vergleichen und dieselbe Frage nirgends verschieden
/// beantwortet wird. Welche Rollen (<see cref="ClubRole"/>) zu welchem Recht
/// gehören, steht in <see cref="ClubRollen"/>; die Rechte einer Person
/// addieren sich, <see cref="ClubRole.Verwaltung"/> schließt alle ein.
///
/// Dazu die Sichtbarkeitsprüfung für vereinsspezifische Übungen
/// (<see cref="Domain.Sports.Exercise.ClubId"/>): sichtbar für Trainer:innen,
/// die dem Verein per <see cref="Domain.Community.ClubTrainer"/> zugewiesen
/// sind, sowie für Mitglieder einer Gruppe dieses Vereins.
/// </summary>
public static class ClubAccessQueries
{
    /// <summary>Nur Zeilen, die mindestens eine der <paramref name="rollen"/> tragen.</summary>
    public static IQueryable<ClubTrainer> MitRolle(this IQueryable<ClubTrainer> zeilen, ClubRole rollen) =>
        zeilen.Where(t => (t.Role & rollen) != 0);

    private static Task<bool> HatRolleAsync(IApplicationDbContext db, Guid userId, Guid clubId, ClubRole rollen, CancellationToken ct) =>
        db.ClubTrainers.Where(t => t.ClubId == clubId && t.UserId == userId).MitRolle(rollen).AnyAsync(ct);

    /// <summary>Die Rollen einer Person in einem Verein; leer, wenn sie keine hat.</summary>
    public static async Task<ClubRole> GetClubRolesAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        await db.ClubTrainers
            .Where(t => t.ClubId == clubId && t.UserId == userId)
            .Select(t => (ClubRole?)t.Role)
            .FirstOrDefaultAsync(ct) ?? 0;

    public static async Task<HashSet<Guid>> GetVisibleClubIdsAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default)
    {
        // Nur Trainer-Rollen: Mitgliederverwaltung und Vorstand gehören zum
        // Verein, aber ihre Rollen geben keinen Einblick in dessen Katalog.
        var trainerClubIds = await db.ClubTrainers
            .Where(t => t.UserId == userId)
            .MitRolle(ClubRollen.Trainer)
            .Select(t => t.ClubId)
            .ToListAsync(ct);

        var memberClubIds = await db.Groups
            // Nur aktive Mitglieder: Eine offene Anfrage oder eine noch nicht
            // angenommene Einladung macht niemanden zum Teil des Vereins.
            .Where(g => g.ClubId != null && g.Members.Any(m => m.UserId == userId && m.Status == GroupMemberStatus.Active))
            .Select(g => g.ClubId!.Value)
            .ToListAsync(ct);

        return trainerClubIds.Concat(memberClubIds).ToHashSet();
    }

    /// <summary>
    /// Ob jemand in diesem Verein Trainer:in ist: Training, Ausbildungswart:in
    /// oder Verwaltung. Das schaltet den Vereinskatalog, Gruppen anlegen und
    /// die Trainer-Perspektive frei - NICHT die Gruppen anderer (dafür
    /// <see cref="CanManageAllGroupsAsync"/>) und nicht die Mitgliederverwaltung.
    /// </summary>
    public static Task<bool> IsClubTrainerAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        HatRolleAsync(db, userId, clubId, ClubRollen.Trainer, ct);

    /// <summary>
    /// Ob jemand ALLE Gruppen des Vereins verwalten darf (Ausbildungswart:in,
    /// Verwaltung) samt deren Terminen. Wer nur Training hat, verwaltet allein
    /// die Gruppen, die er/sie leitet oder mit-betreut.
    /// </summary>
    public static Task<bool> CanManageAllGroupsAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        HatRolleAsync(db, userId, clubId, ClubRollen.AlleGruppen, ct);

    /// <summary>
    /// Ob jemand Beitrittsanfragen entscheiden, einladen, den Einladungslink
    /// verwalten und die Mitgliederliste sehen darf (Mitgliederverwaltung,
    /// Verwaltung) - ohne damit Trainer:in zu sein.
    /// </summary>
    public static Task<bool> CanManageMembersAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        HatRolleAsync(db, userId, clubId, ClubRollen.Mitglieder, ct);

    /// <summary>Ob jemand Trainer:innen (Rolle Training) berufen und abberufen darf (Ausbildungswart:in, Verwaltung).</summary>
    public static Task<bool> CanAppointTrainersAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        HatRolleAsync(db, userId, clubId, ClubRollen.TrainerBerufen, ct);

    /// <summary>Ob jemand die Vereinsübersicht lesen darf (Vorstand, Verwaltung) - wird erst mit der Vorstand-Übersicht genutzt.</summary>
    public static Task<bool> CanViewClubOverviewAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        HatRolleAsync(db, userId, clubId, ClubRollen.Uebersicht, ct);

    /// <summary>
    /// Ob jemand diesen Verein VERWALTEN darf - Stammdaten ändern, alle
    /// Rollen vergeben und entziehen.
    ///
    /// Eine Definition an einer Stelle, weil sonst jede neue verwaltende
    /// Aktion ihre eigene Auslegung mitbrächte. Alle anderen Rollen sind
    /// ausdrücklich NICHT gemeint: Dürften sie Rollen entziehen, könnte auch
    /// die Person entfernt werden, die den Verein angelegt hat.
    /// </summary>
    public static Task<bool> CanManageClubAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        HatRolleAsync(db, userId, clubId, ClubRole.Verwaltung, ct);

    /// <summary>
    /// Die Gruppe, wenn der/die Nutzer:in sie VERWALTEN darf - sonst null.
    /// Verwalten darf: wer die Gruppe leitet (<see cref="Group.TrainerId"/>), wer
    /// als weitere:r Trainer:in angenommen hat (offene Einladungen blendet der
    /// globale Filter aus) und wer alle Gruppen des Vereins verwaltet
    /// (<see cref="CanManageAllGroupsAsync"/>), dem die Gruppe gehört. Wer im
    /// Verein nur Training hat, verwaltet allein die eigenen Gruppen.
    ///
    /// Eine Definition für Gruppenverwaltung und Anmeldungen, damit "wer darf
    /// die Gruppe anfassen" nicht an zwei Stellen verschieden beantwortet wird.
    /// Liefert die getrackte Entität zurück, damit Aufrufer sie direkt ändern
    /// können.
    /// </summary>
    public static async Task<Group?> GetManageableGroupAsync(this IApplicationDbContext db, Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return null;
        if (group.TrainerId == userId) return group;
        if (await db.GroupTrainers.AnyAsync(t => t.GroupId == groupId && t.UserId == userId, ct)) return group;
        if (group.ClubId is { } clubId && await db.CanManageAllGroupsAsync(userId, clubId, ct))
            return group;
        return null;
    }

    /// <summary>
    /// Ob jemand überhaupt irgendwo Trainer:in ist: als Hauptverantwortliche:r
    /// einer Gruppe, als weitere:r Trainer:in einer Gruppe oder mit einer
    /// Trainer-Rolle in einem Verein (nicht: nur Mitgliederverwaltung/Vorstand).
    ///
    /// Eine Definition an EINER Stelle, weil daran zwei Dinge hängen, die nicht
    /// auseinanderlaufen dürfen: die Trainer-Perspektive im Frontend und die
    /// Identity-Rolle TRAINER, die in der Admin-Übersicht angezeigt wird.
    /// </summary>
    public static async Task<bool> IsAnyTrainerAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default)
    {
        if (await db.Groups.AnyAsync(g => g.TrainerId == userId, ct)) return true;
        if (await db.GroupTrainers.AnyAsync(t => t.UserId == userId, ct)) return true;
        return await db.ClubTrainers.Where(t => t.UserId == userId).MitRolle(ClubRollen.Trainer).AnyAsync(ct);
    }

    /// <summary>
    /// Ob jemand zu irgendeinem Verein gehört - als freigegebenes Mitglied
    /// ODER mit einer Rolle im Verein (jede, auch Vorstand).
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
