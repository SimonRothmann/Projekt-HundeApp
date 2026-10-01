using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Admin;

/// <summary>
/// Findet und zählt Daten von Konten, die es nicht mehr gibt.
///
/// Das Löschen eines Kontos hat bis zum 13.09.2026 nur Vereins- und Gruppenzeilen
/// mitgenommen (siehe <c>AdminService.DeleteUserAsync</c>); Hunde, Trainings,
/// Benachrichtigungen und die kleinen Verweise blieben stehen. Die Zeilen
/// verweisen nur über die UserId auf das Konto, ohne Fremdschlüssel - deshalb
/// ist "verwaist" hier: Die Id steht in den Fachdaten, aber in keinem Konto.
///
/// Gelöscht wird hier NICHTS. Die Bereinigung ruft für jede verwaiste Id den
/// vorhandenen <c>AccountDataService.PurgeAsync</c> auf - es gibt keine zweite
/// Löschlogik, die von der echten abweichen könnte.
/// </summary>
internal static class VerwaisteKontodaten
{
    /// <summary>
    /// Die UserIds, auf die Fachdaten verweisen und die es als Konto nicht gibt.
    ///
    /// Zwei Sicherungen, weil ein Fehler hier alles löschen ließe:
    /// - Liefert die Konto-Liste nichts, ist das ein Fehlschlag und keine
    ///   "leere Menge Konten". Sonst wäre jede Id verwaist.
    /// - Die aufrufende Person muss selbst in der Liste stehen (sie ist ja
    ///   angemeldet) und gilt nie als verwaist. Fehlt sie, ist die Liste
    ///   unvollständig oder falsch gelesen.
    ///
    /// Ein vorhandenes Konto bleibt vollständig unberührt: Hat es früher auf
    /// einem Hund trainiert, der nur noch verwaisten Konten gehört (etwa als
    /// ehemalige:r Mitbesitzer:in), würde <c>PurgeAsync</c> diese Trainings mit
    /// dem Hund löschen. Die Besitzer:innen solcher Hunde werden deshalb nicht
    /// bereinigt und nicht gezählt; ihre Daten bleiben stehen, bis jemand sie
    /// von Hand klärt. Lieber etwas übrig lassen als fremde Trainings löschen.
    /// Wirft die Liste eine Ausnahme, läuft sie ungefangen nach oben: Es wird
    /// dann nichts gemeldet und nichts gelöscht.
    /// </summary>
    public static async Task<Result<IReadOnlyList<Guid>>> VerwaisteIdsAsync(
        IApplicationDbContext db, IUserLookupService userLookup, Guid aufrufer, CancellationToken ct)
    {
        var konten = (await userLookup.ListAllUserIdsAsync(ct)).ToHashSet();
        if (konten.Count == 0 || !konten.Contains(aufrufer))
            return Result<IReadOnlyList<Guid>>.Failure("Die Konten konnten nicht zuverlässig gelesen werden. Es wurde nichts verändert.");

        var verwiesen = new HashSet<Guid>();
        async Task Sammeln(IQueryable<Guid> ids) => verwiesen.UnionWith(await ids.Distinct().ToListAsync(ct));

        // Alles, was PurgeAsync für ein Konto entfernt oder umhängt - jeweils
        // ohne Query-Filter, denn auch weich Gelöschtes trägt noch die Id.
        await Sammeln(db.DogOwners.IgnoreQueryFilters().Select(o => o.UserId));
        await Sammeln(db.TrainingSessions.IgnoreQueryFilters().Select(s => s.UserId));
        await Sammeln(db.UserPreferences.IgnoreQueryFilters().Select(p => p.UserId));
        await Sammeln(db.QuizMasteries.IgnoreQueryFilters().Select(m => m.UserId));
        await Sammeln(db.Notifications.IgnoreQueryFilters().Select(n => n.UserId));
        await Sammeln(db.ClubMemberships.IgnoreQueryFilters().Select(m => m.UserId));
        await Sammeln(db.ClubTrainers.IgnoreQueryFilters().Select(t => t.UserId));
        await Sammeln(db.ClubRegistrations.IgnoreQueryFilters().Select(r => r.RequestedByUserId));
        await Sammeln(db.GroupMembers.IgnoreQueryFilters().Select(m => m.UserId));
        await Sammeln(db.GroupTrainers.IgnoreQueryFilters().Select(t => t.UserId));
        // Nur offene Gruppen: Ohne zweite Trainer:in schließt PurgeAsync die Gruppe
        // (DeletedAt) und lässt die TrainerId stehen. Zählte die geschlossene
        // mit, bliebe das Konto für immer "verwaist" und die Bereinigung nie fertig.
        await Sammeln(db.Groups.IgnoreQueryFilters().Where(g => g.DeletedAt == null).Select(g => g.TrainerId));
        await Sammeln(db.TrainerAssignments.IgnoreQueryFilters().Select(a => a.TrainerId));
        await Sammeln(db.TrainerAssignments.IgnoreQueryFilters().Select(a => a.MemberId));
        await Sammeln(db.GroupTrainingSessionTrainers.IgnoreQueryFilters().Select(t => t.UserId));
        await Sammeln(db.GroupTrainingSessionResponses.IgnoreQueryFilters().Select(r => r.UserId));

        var verwaist = verwiesen.Where(id => id != aufrufer && !konten.Contains(id)).ToList();

        // Gegen die ursprüngliche Menge gerechnet ist das sicher: Was am Ende
        // mitgeht, hat nur Besitzer:innen aus dieser Menge - und deren Hunde
        // sind hier bereits erfasst. Zu streng kann es nur werden, nie zu lasch.
        var zurueckgehalten = await BesitzerMitFremdenTrainingsAsync(db, verwaist, ct);
        return Result<IReadOnlyList<Guid>>.Success(verwaist.Where(id => !zurueckgehalten.Contains(id)).ToList());
    }

    /// <summary>
    /// Die Hunde, die bei der Bereinigung dieser Konten mitgehen: Niemand außer
    /// den verwaisten Konten hat sie angenommen (wie bei PurgeAsync).
    /// </summary>
    private static async Task<List<Guid>> MitgehendeHundeAsync(IApplicationDbContext db, IReadOnlyList<Guid> verwaist, CancellationToken ct)
    {
        var ihreHunde = await db.DogOwners.IgnoreQueryFilters()
            .Where(o => verwaist.Contains(o.UserId) && o.Status == DogOwnerStatus.Active)
            .Select(o => o.DogId)
            .Distinct()
            .ToListAsync(ct);
        var mitAnderenBesitzern = await db.DogOwners.IgnoreQueryFilters()
            .Where(o => ihreHunde.Contains(o.DogId) && !verwaist.Contains(o.UserId)
                && o.DeletedAt == null && o.Status == DogOwnerStatus.Active)
            .Select(o => o.DogId)
            .Distinct()
            .ToListAsync(ct);
        return ihreHunde.Except(mitAnderenBesitzern).ToList();
    }

    /// <summary>
    /// Verwaiste Konten, deren mitgehende Hunde Trainings eines vorhandenen
    /// Kontos tragen (auch weich gelöschte - es sind trotzdem fremde Daten).
    /// </summary>
    private static async Task<HashSet<Guid>> BesitzerMitFremdenTrainingsAsync(
        IApplicationDbContext db, IReadOnlyList<Guid> verwaist, CancellationToken ct)
    {
        if (verwaist.Count == 0) return [];

        var hunde = await MitgehendeHundeAsync(db, verwaist, ct);
        if (hunde.Count == 0) return [];

        var geschuetzteHunde = await db.TrainingSessions.IgnoreQueryFilters()
            .Where(s => hunde.Contains(s.DogId) && !verwaist.Contains(s.UserId))
            .Select(s => s.DogId)
            .Distinct()
            .ToListAsync(ct);
        if (geschuetzteHunde.Count == 0) return [];

        var besitzer = await db.DogOwners.IgnoreQueryFilters()
            .Where(o => geschuetzteHunde.Contains(o.DogId) && verwaist.Contains(o.UserId) && o.Status == DogOwnerStatus.Active)
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync(ct);
        return [.. besitzer];
    }

    /// <summary>Zählt, was die Bereinigung für diese Konten entfernen würde (ohne Ids).</summary>
    public static async Task<OrphanedDataDto> ZaehlenAsync(IApplicationDbContext db, IReadOnlyList<Guid> verwaist, CancellationToken ct)
    {
        if (verwaist.Count == 0) return new OrphanedDataDto(0, 0, 0, 0, 0, 0);

        var hunde = await MitgehendeHundeAsync(db, verwaist, ct);

        var trainings = await db.TrainingSessions.IgnoreQueryFilters()
            .CountAsync(s => verwaist.Contains(s.UserId) || hunde.Contains(s.DogId), ct);
        var ziele = await db.Goals.IgnoreQueryFilters().CountAsync(g => hunde.Contains(g.DogId), ct);
        var benachrichtigungen = await db.Notifications.IgnoreQueryFilters().CountAsync(n => verwaist.Contains(n.UserId), ct);

        var sonstige =
            await db.DogOwners.IgnoreQueryFilters().CountAsync(o => verwaist.Contains(o.UserId), ct)
            + await db.UserPreferences.IgnoreQueryFilters().CountAsync(p => verwaist.Contains(p.UserId), ct)
            + await db.QuizMasteries.IgnoreQueryFilters().CountAsync(m => verwaist.Contains(m.UserId), ct)
            + await db.ClubMemberships.IgnoreQueryFilters().CountAsync(m => verwaist.Contains(m.UserId), ct)
            + await db.ClubTrainers.IgnoreQueryFilters().CountAsync(t => verwaist.Contains(t.UserId), ct)
            + await db.ClubRegistrations.IgnoreQueryFilters().CountAsync(r => verwaist.Contains(r.RequestedByUserId), ct)
            + await db.GroupMembers.IgnoreQueryFilters().CountAsync(m => verwaist.Contains(m.UserId), ct)
            + await db.GroupTrainers.IgnoreQueryFilters().CountAsync(t => verwaist.Contains(t.UserId), ct)
            + await db.Groups.IgnoreQueryFilters().CountAsync(g => g.DeletedAt == null && verwaist.Contains(g.TrainerId), ct)
            + await db.TrainerAssignments.IgnoreQueryFilters()
                .CountAsync(a => verwaist.Contains(a.TrainerId) || verwaist.Contains(a.MemberId), ct)
            + await db.GroupTrainingSessionTrainers.IgnoreQueryFilters().CountAsync(t => verwaist.Contains(t.UserId), ct)
            + await db.GroupTrainingSessionResponses.IgnoreQueryFilters().CountAsync(r => verwaist.Contains(r.UserId), ct);

        return new OrphanedDataDto(verwaist.Count, trainings, hunde.Count, ziele, benachrichtigungen, sonstige);
    }
}
