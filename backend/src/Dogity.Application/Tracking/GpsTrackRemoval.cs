using Dogity.Application.Abstractions;
using Dogity.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tracking;

/// <summary>
/// Löscht Fährten weich - samt Punkten, Abläufen, Ablaufpunkten, Stockungen, Schenkeln, Winkeln und Gegenstandsangaben.
///
/// Eine Stelle für alle Wege, die eine Fährte mit ihrem Training verschwinden
/// lassen (Training löschen, Aufräumen von Altbestand). Ein Training, das nur
/// selbst ein DeletedAt bekommt, ließ seine Fährten stehen: Sie sind über das
/// Training nicht mehr erreichbar, zählten aber in der Admin-Übersicht weiter
/// als Fährten und hielten ihre GPS-Punkte in der Datenbank.
///
/// Weich und nicht endgültig, wie beim Training selbst - wirklich weg geht es nach der
/// Aufbewahrungsfrist (<c>AufbewahrungCleanup</c>) oder beim Löschen des ganzen Kontos
/// (<c>AccountDataService.PurgeAsync</c>).
///
/// Je Tabelle ein Befehl statt jede Zeile einzeln zu laden und zurückzuschreiben: Eine
/// Fährte bringt tausende Punkte mit. Auf Postgres wirkt er sofort (ExecuteUpdate, ohne
/// Change Tracker); der In-Memory-Ersatz der Tests kann das nicht und geht über den Change
/// Tracker - dort speichert erst der Aufrufer (<see cref="IApplicationDbContext.UnterstuetztMassenbefehle"/>).
/// </summary>
public static class GpsTrackRemoval
{
    /// <returns>Wie viele Fährten dabei neu als gelöscht markiert wurden.</returns>
    public static async Task<int> WeichLoeschenAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> trackIds, DateTimeOffset jetzt, CancellationToken ct = default)
    {
        if (trackIds.Count == 0) return 0;

        // IgnoreQueryFilters überall: Eine Fährte kann schon einzeln gelöscht
        // sein (GpsTrackService.DeleteAsync setzte früher nur ihr eigenes DeletedAt),
        // ihre Punkte stehen dann trotzdem noch. Nur was noch kein Datum hat,
        // bekommt eines - ein früherer Löschzeitpunkt bleibt erhalten.
        var neu = await MarkiereAsync(db, db.GpsTracks.IgnoreQueryFilters().Where(t => trackIds.Contains(t.Id)), jetzt, ct);
        await MarkiereAsync(db, db.GpsPoints.IgnoreQueryFilters().Where(p => trackIds.Contains(p.TrackId)), jetzt, ct);

        var ablaufIds = db.GpsWalkRuns.IgnoreQueryFilters().Where(w => trackIds.Contains(w.TrackId)).Select(w => w.Id);
        await MarkiereAsync(db, db.GpsWalkRuns.IgnoreQueryFilters().Where(w => trackIds.Contains(w.TrackId)), jetzt, ct);
        await MarkiereAsync(db, db.GpsWalkPoints.IgnoreQueryFilters().Where(p => ablaufIds.Contains(p.WalkRunId)), jetzt, ct);
        await MarkiereAsync(db, db.GpsWalkStops.IgnoreQueryFilters().Where(s => ablaufIds.Contains(s.WalkRunId)), jetzt, ct);
        await MarkiereAsync(db, db.GpsWalkRunLegs.IgnoreQueryFilters().Where(l => ablaufIds.Contains(l.WalkRunId)), jetzt, ct);
        await MarkiereAsync(db, db.GpsWalkRunCorners.IgnoreQueryFilters().Where(c => ablaufIds.Contains(c.WalkRunId)), jetzt, ct);
        await MarkiereAsync(db, db.GpsWalkRunArticles.IgnoreQueryFilters().Where(a => ablaufIds.Contains(a.WalkRunId)), jetzt, ct);

        return neu;
    }

    /// <returns>Wie viele Zeilen neu ein Löschdatum bekamen.</returns>
    private static async Task<int> MarkiereAsync<T>(IApplicationDbContext db, IQueryable<T> zeilen, DateTimeOffset jetzt, CancellationToken ct)
        where T : Entity
    {
        var offen = zeilen.Where(z => z.DeletedAt == null);
        if (db.UnterstuetztMassenbefehle)
            return await offen.ExecuteUpdateAsync(s => s.SetProperty(z => z.DeletedAt, jetzt), ct);

        var liste = await offen.ToListAsync(ct);
        foreach (var z in liste)
            z.DeletedAt = jetzt;
        return liste.Count;
    }
}
