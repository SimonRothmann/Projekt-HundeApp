using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Betrieb;

/// <summary>
/// Wie lange Daten bleiben, bevor sie endgültig verschwinden (Entscheidung des Betreibers,
/// 16.11.2026; die Datenschutzerklärung nennt dieselben Fristen).
/// </summary>
public static class Aufbewahrungsfristen
{
    /// <summary>Gelöschte GPS-Daten (Fährten samt Punkten, Abläufen und Halten): Tage nach dem Löschen.</summary>
    public const int GeloeschteGpsDatenTage = 30;

    /// <summary>Benachrichtigungen (Glocke), gelesen oder nicht: Tage nach dem Anlegen.</summary>
    public const int BenachrichtigungenTage = 90;

    /// <summary>Was vor diesem Zeitpunkt gelöscht wurde, ist fällig.</summary>
    public static DateTimeOffset GrenzeGeloeschteGpsDaten(DateTimeOffset jetzt) => jetzt.AddDays(-GeloeschteGpsDatenTage);

    /// <summary>Was vor diesem Zeitpunkt angelegt wurde, ist fällig.</summary>
    public static DateTimeOffset GrenzeBenachrichtigungen(DateTimeOffset jetzt) => jetzt.AddDays(-BenachrichtigungenTage);
}

/// <summary>
/// Entfernt, was die Aufbewahrungsfristen (<see cref="Aufbewahrungsfristen"/>) hinter sich hat.
/// Idempotent: Ein zweiter Lauf findet nichts mehr.
/// </summary>
public interface IAufbewahrungCleanup
{
    Task<AufbewahrungErgebnis> CleanupAsync(DateTimeOffset? jetzt = null, CancellationToken ct = default);
}

/// <summary>Wie viele Fährten (die Punkte und Abläufe gehen mit) und Benachrichtigungen entfernt wurden.</summary>
public record AufbewahrungErgebnis(int Faehrten, int Benachrichtigungen);

/// <inheritdoc />
public class AufbewahrungCleanup(IApplicationDbContext db) : IAufbewahrungCleanup
{
    /// <summary>
    /// So viele Fährten bzw. Benachrichtigungen je Durchgang: Eine Fährte bringt tausende Punkte mit.
    /// Portionen halten die einzelnen Befehle kurz - der Lauf liegt nachts, aber der erste trifft
    /// einen Altbestand von Wochen.
    /// </summary>
    public const int Portion = 100;

    public async Task<AufbewahrungErgebnis> CleanupAsync(DateTimeOffset? jetzt = null, CancellationToken ct = default)
    {
        var zeit = jetzt ?? DateTimeOffset.UtcNow;
        return new AufbewahrungErgebnis(
            await FaehrtenEntfernenAsync(Aufbewahrungsfristen.GrenzeGeloeschteGpsDaten(zeit), ct),
            await BenachrichtigungenEntfernenAsync(Aufbewahrungsfristen.GrenzeBenachrichtigungen(zeit), ct));
    }

    private async Task<int> FaehrtenEntfernenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        var gesamt = 0;
        while (true)
        {
            // Nach dem Löschdatum der Fährte selbst, nicht nach dem ihrer Punkte: Eine Fährte, die vor der
            // gemeinsamen Löschroutine (GpsTrackRemoval) einzeln gelöscht wurde, hat Punkte ohne Datum.
            var ids = await db.GpsTracks.IgnoreQueryFilters()
                .Where(t => t.DeletedAt != null && t.DeletedAt < grenze)
                .OrderBy(t => t.DeletedAt)
                .Select(t => t.Id)
                .Take(Portion)
                .ToListAsync(ct);
            if (ids.Count == 0) return gesamt;

            var auswahl = db.GpsTracks.IgnoreQueryFilters().Where(t => ids.Contains(t.Id));
            if (db.UnterstuetztMassenbefehle)
            {
                // Punkte, Abläufe, Halte, Schenkel, Winkel und Gegenstandsangaben hängen mit ON DELETE
                // CASCADE an der Fährte - ein Befehl räumt alles.
                await auswahl.ExecuteDeleteAsync(ct);
            }
            else
            {
                // In-Memory-Ersatz der Tests: Das Löschen folgt den geladenen Verknüpfungen.
                db.GpsTracks.RemoveRange(await auswahl
                    .Include(t => t.Points)
                    .Include(t => t.WalkRuns).ThenInclude(r => r.Points)
                    .Include(t => t.WalkRuns).ThenInclude(r => r.Stops)
                    .Include(t => t.WalkRuns).ThenInclude(r => r.Legs)
                    .Include(t => t.WalkRuns).ThenInclude(r => r.Corners)
                    .Include(t => t.WalkRuns).ThenInclude(r => r.Articles)
                    .AsSplitQuery()
                    .ToListAsync(ct));
                await db.SaveChangesAsync(ct);
            }
            gesamt += ids.Count;
        }
    }

    private async Task<int> BenachrichtigungenEntfernenAsync(DateTimeOffset grenze, CancellationToken ct)
    {
        var gesamt = 0;
        while (true)
        {
            // Auch ausgeblendete (DeletedAt gesetzt): Die Frist gilt für alle.
            var ids = await db.Notifications.IgnoreQueryFilters()
                .Where(n => n.CreatedAt < grenze)
                .OrderBy(n => n.CreatedAt)
                .Select(n => n.Id)
                .Take(Portion)
                .ToListAsync(ct);
            if (ids.Count == 0) return gesamt;

            var auswahl = db.Notifications.IgnoreQueryFilters().Where(n => ids.Contains(n.Id));
            if (db.UnterstuetztMassenbefehle)
            {
                await auswahl.ExecuteDeleteAsync(ct);
            }
            else
            {
                db.Notifications.RemoveRange(await auswahl.ToListAsync(ct));
                await db.SaveChangesAsync(ct);
            }
            gesamt += ids.Count;
        }
    }
}
