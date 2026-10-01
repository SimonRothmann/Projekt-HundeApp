using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tracking;

/// <summary>
/// Löscht Fährten weich - samt Punkten, Abläufen, Ablaufpunkten und Stockungen.
///
/// Eine Stelle für alle Wege, die eine Fährte mit ihrem Training verschwinden
/// lassen (Training löschen, Aufräumen von Altbestand). Ein Training, das nur
/// selbst ein DeletedAt bekommt, ließ seine Fährten stehen: Sie sind über das
/// Training nicht mehr erreichbar, zählten aber in der Admin-Übersicht weiter
/// als Fährten und hielten ihre GPS-Punkte in der Datenbank.
///
/// Weich und nicht endgültig, wie beim Training selbst - nur beim Löschen des
/// ganzen Kontos (<c>AccountDataService.PurgeAsync</c>) geht es wirklich weg.
///
/// Speichert nicht selbst: Der Aufrufer bündelt es mit seinen übrigen
/// Änderungen in einem SaveChanges.
/// </summary>
public static class GpsTrackRemoval
{
    /// <returns>Wie viele Fährten dabei neu als gelöscht markiert wurden.</returns>
    public static async Task<int> WeichLoeschenAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> trackIds, DateTimeOffset jetzt, CancellationToken ct = default)
    {
        if (trackIds.Count == 0) return 0;

        // IgnoreQueryFilters überall: Eine Fährte kann schon einzeln gelöscht
        // sein (GpsTrackService.DeleteAsync setzt nur ihr eigenes DeletedAt),
        // ihre Punkte stehen dann trotzdem noch. Nur was noch kein Datum hat,
        // bekommt eines - ein früherer Löschzeitpunkt bleibt erhalten.
        var fahrten = await db.GpsTracks.IgnoreQueryFilters().Where(t => trackIds.Contains(t.Id)).ToListAsync(ct);
        var neu = 0;
        foreach (var f in fahrten.Where(f => f.DeletedAt is null))
        {
            f.DeletedAt = jetzt;
            neu++;
        }

        foreach (var p in await db.GpsPoints.IgnoreQueryFilters()
                     .Where(p => trackIds.Contains(p.TrackId) && p.DeletedAt == null).ToListAsync(ct))
            p.DeletedAt = jetzt;

        var ablaeufe = await db.GpsWalkRuns.IgnoreQueryFilters().Where(w => trackIds.Contains(w.TrackId)).ToListAsync(ct);
        var ablaufIds = ablaeufe.Select(w => w.Id).ToList();
        foreach (var w in ablaeufe.Where(w => w.DeletedAt is null))
            w.DeletedAt = jetzt;
        foreach (var p in await db.GpsWalkPoints.IgnoreQueryFilters()
                     .Where(p => ablaufIds.Contains(p.WalkRunId) && p.DeletedAt == null).ToListAsync(ct))
            p.DeletedAt = jetzt;
        foreach (var s in await db.GpsWalkStops.IgnoreQueryFilters()
                     .Where(s => ablaufIds.Contains(s.WalkRunId) && s.DeletedAt == null).ToListAsync(ct))
            s.DeletedAt = jetzt;

        return neu;
    }
}
