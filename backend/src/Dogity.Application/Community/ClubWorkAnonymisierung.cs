using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Löst Teilnahmen an Arbeitseinsätzen von ihrer Person: Verlässt jemand den Verein, oder wird das Konto
/// gelöscht, wird UserId der Teilnahme leer. Die Minuten bleiben für die Summen; die Anzeige schreibt
/// "Ehemaliges Mitglied". Die Methoden speichern nicht - der Aufrufer tut es mit seinen eigenen Änderungen.
///
/// Ohne Query-Filter: Auch Teilnahmen eines gelöschten Einsatzes sollen die Person nicht mehr tragen.
/// </summary>
public static class ClubWorkAnonymisierung
{
    /// <summary>Die Teilnahmen einer Person an den Einsätzen EINES Vereins (Austritt).</summary>
    public static async Task MitgliedAusVereinAsync(IApplicationDbContext db, Guid clubId, Guid userId, CancellationToken ct = default) =>
        Loesen(await db.ClubWorkParticipations.IgnoreQueryFilters()
            .Where(p => p.UserId == userId && db.ClubWorkEvents.IgnoreQueryFilters().Any(e => e.Id == p.ClubWorkEventId && e.ClubId == clubId))
            .ToListAsync(ct));

    /// <summary>Die Teilnahmen von Personen in ALLEN Vereinen (Konto gelöscht).</summary>
    public static async Task MitgliederEntfernenAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        Loesen(await db.ClubWorkParticipations.IgnoreQueryFilters()
            .Where(p => p.UserId != null && userIds.Contains(p.UserId.Value))
            .ToListAsync(ct));

    private static void Loesen(List<Domain.Community.ClubWorkParticipation> teilnahmen)
    {
        foreach (var p in teilnahmen) p.UserId = null;
    }
}
