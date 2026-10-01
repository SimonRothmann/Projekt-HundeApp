using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tracking;

/// <summary>
/// Löscht Fährten weich, deren Training gelöscht ist oder fehlt.
///
/// Bis zur Korrektur an <c>TrainingService.DeleteAsync</c> hat das Löschen eines
/// Trainings seine Fährten stehen lassen (siehe <see cref="GpsTrackRemoval"/>).
/// Dieser Lauf holt den Altbestand einmal nach. Beim Start und nicht beim
/// Löschen, weil es nur Altlast ist - neue Löschungen räumen selbst auf.
///
/// Idempotent: Eine Fährte, die schon ein DeletedAt hat, findet der nächste
/// Lauf nicht mehr.
/// </summary>
public interface IGpsTrackOrphanCleanup
{
    /// <returns>Wie viele Fährten neu als gelöscht markiert wurden.</returns>
    Task<int> CleanupAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public class GpsTrackOrphanCleanup(IApplicationDbContext db) : IGpsTrackOrphanCleanup
{
    /// <summary>
    /// So viele Fährten je Durchgang: Jede bringt tausende Punkte mit, die zum
    /// Markieren geladen werden. Portionen halten einzelne Abfragen und
    /// Speichervorgänge klein; geladene Zeilen bleiben bis zum Ende des Laufs im
    /// Change Tracker, der Speicherbedarf wächst also mit dem Altbestand. Das
    /// ist bei der überschaubaren Menge (einmalig beim Start) in Kauf genommen.
    /// </summary>
    private const int Portion = 50;

    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        var gesamt = 0;
        while (true)
        {
            // IgnoreQueryFilters gilt für die ganze Abfrage, auch für die
            // Trainings darin - deshalb steht "nicht gelöscht" ausdrücklich da.
            var ids = await db.GpsTracks.IgnoreQueryFilters()
                .Where(t => t.DeletedAt == null
                    && !db.TrainingSessions.IgnoreQueryFilters().Any(s => s.Id == t.TrainingSessionId && s.DeletedAt == null))
                .OrderBy(t => t.CreatedAt)
                .Select(t => t.Id)
                .Take(Portion)
                .ToListAsync(ct);
            if (ids.Count == 0) return gesamt;

            gesamt += await GpsTrackRemoval.WeichLoeschenAsync(db, ids, DateTimeOffset.UtcNow, ct);
            await db.SaveChangesAsync(ct);
        }
    }
}
