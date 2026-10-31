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

    /// <summary>
    /// Ob jemand in irgendeinem Verein Mitglieder verwalten darf - ohne Trainer-Rolle
    /// und ohne Gruppentraining. Daran hängt, ob die Startseite "Zu erledigen" für
    /// die Beitrittsanfragen des Vereins zeigt.
    /// </summary>
    public static Task<bool> CanManageMembersAnywhereAsync(this IApplicationDbContext db, Guid userId, CancellationToken ct = default) =>
        db.ClubTrainers.Where(t => t.UserId == userId).MitRolle(ClubRollen.Mitglieder).AnyAsync(ct);

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

    /// <summary>Eine Zeile der Beziehung "<see cref="UserId"/> ist Trainer:in der Gruppe <see cref="GroupId"/>".</summary>
    public sealed class GruppenTrainerin
    {
        public Guid GroupId { get; init; }
        public Guid UserId { get; init; }
    }

    /// <summary>
    /// Wer ist Trainer:in welcher Gruppe - die EINE Definition von "Trainer:in
    /// dieser Gruppe", als Beziehung und damit einbettbar (wie
    /// <see cref="DogAccessQueries.ZugaenglicheHundeIds"/>), auch je Zeile einer
    /// anderen Abfrage. Trainer:in einer Gruppe ist, wer sie leitet
    /// (<see cref="Group.TrainerId"/>), wer als weitere:r Trainer:in angenommen
    /// hat (offene Einladungen blendet der globale Filter aus) und wer alle
    /// Gruppen des Vereins verwaltet (<see cref="ClubRollen.AlleGruppen"/>), dem
    /// die Gruppe gehört. Wer im Verein nur Training hat, ist Trainer:in allein
    /// der eigenen Gruppen.
    /// </summary>
    public static IQueryable<GruppenTrainerin> GruppenTrainerinnen(this IApplicationDbContext db) =>
        db.Groups.Select(g => new GruppenTrainerin { GroupId = g.Id, UserId = g.TrainerId })
            .Union(db.GroupTrainers.Select(t => new GruppenTrainerin { GroupId = t.GroupId, UserId = t.UserId }))
            .Union(
                from t in db.ClubTrainers.MitRolle(ClubRollen.AlleGruppen)
                join g in db.Groups on t.ClubId equals g.ClubId
                select new GruppenTrainerin { GroupId = g.Id, UserId = t.UserId });

    /// <summary>Die Gruppen, in denen <paramref name="userId"/> Trainer:in ist (siehe <see cref="GruppenTrainerinnen"/>).</summary>
    public static IQueryable<Guid> GruppenIdsDerTrainerin(this IApplicationDbContext db, Guid userId) =>
        db.GruppenTrainerinnen().Where(p => p.UserId == userId).Select(p => p.GroupId);

    /// <summary>
    /// Die Gruppe, wenn der/die Nutzer:in sie VERWALTEN darf - sonst null:
    /// wer in ihr Trainer:in ist (<see cref="GruppenTrainerinnen"/>).
    ///
    /// Eine Definition für Gruppenverwaltung und Anmeldungen, damit "wer darf
    /// die Gruppe anfassen" nicht an zwei Stellen verschieden beantwortet wird.
    /// Liefert die getrackte Entität zurück, damit Aufrufer sie direkt ändern
    /// können.
    /// </summary>
    public static Task<Group?> GetManageableGroupAsync(this IApplicationDbContext db, Guid userId, Guid groupId, CancellationToken ct = default) =>
        db.Groups
            .Where(g => g.Id == groupId && db.GruppenIdsDerTrainerin(userId).Contains(g.Id))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Betreuungen, die über keine Gruppe mehr bestehen: Es gibt keine Gruppe,
    /// in der das betreute Mitglied aktiv ist und die betreuende Person
    /// Trainer:in (<see cref="GruppenTrainerinnen"/>). <paramref name="ausser"/>
    /// nimmt eine Gruppe aus der Betrachtung - die, die das Mitglied gerade verlässt.
    /// </summary>
    public static IQueryable<TrainerAssignment> OhneGemeinsameGruppe(this IQueryable<TrainerAssignment> betreuungen, IApplicationDbContext db, Guid ausser = default) =>
        betreuungen.Where(a => !db.GruppenTrainerinnen().Any(p =>
            p.UserId == a.TrainerId
            && p.GroupId != ausser
            && db.GroupMembers.Any(m => m.GroupId == p.GroupId && m.UserId == a.MemberId && m.Status == GroupMemberStatus.Active)));

    /// <summary>
    /// Wer zu einem Verein gehört: mit irgendeiner Rolle (auch Vorstand) oder
    /// als freigegebenes Mitglied. Weiter als <see cref="IsClubTrainerAsync"/>,
    /// denn hier geht es um die Zugehörigkeit, nicht um ein Recht.
    /// </summary>
    public static IQueryable<Guid> VereinsPersonenIds(this IApplicationDbContext db, Guid clubId) =>
        db.ClubTrainers.Where(t => t.ClubId == clubId).Select(t => t.UserId)
            .Union(db.ClubMemberships
                .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Approved)
                .Select(m => m.UserId));

    public static Task<bool> GehoertZumVereinAsync(this IApplicationDbContext db, Guid userId, Guid clubId, CancellationToken ct = default) =>
        db.VereinsPersonenIds(clubId).AnyAsync(id => id == userId, ct);

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
}
