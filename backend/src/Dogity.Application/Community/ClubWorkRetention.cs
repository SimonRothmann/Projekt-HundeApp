using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Löscht Arbeitseinsätze samt Teilnahmen endgültig, sobald sie vor dem Beginn des drittletzten
/// Kalenderjahres liegen (2026: alles vor dem 01.01.2024) - das steht so in der Datenschutzerklärung.
/// Die Teilnahmen sind Angaben zu Personen; für Dank und Ehrungen genügen die letzten Jahre.
///
/// Im selben Lauf gehen auch Helfer:innen ohne Konto (<see cref="Domain.Community.ClubWorkGuest"/>), bei denen keine
/// Teilnahme mehr übrig ist und die älter als <see cref="GastFrist"/> sind (so lange darf eine frisch angelegte Person
/// auf ihren ersten Haken warten).
/// </summary>
public interface IClubWorkRetention
{
    /// <returns>Wie viele Einsätze entfernt wurden (verwaiste Personen ohne Konto zählen nicht mit).</returns>
    Task<int> CleanupAsync(DateTimeOffset? jetzt = null, CancellationToken ct = default);
}

/// <inheritdoc />
public class ClubWorkRetention(IApplicationDbContext db) : IClubWorkRetention
{
    /// <summary>Der erste Tag, der noch aufbewahrt wird: der 1. Januar des drittletzten Kalenderjahres (das laufende mitgezählt; 2026: 01.01.2024).</summary>
    public static DateOnly Grenze(DateOnly heute) => new(heute.Year - 2, 1, 1);

    /// <summary>So lange bleibt eine Person ohne Konto auch ohne Teilnahme stehen.</summary>
    public static readonly TimeSpan GastFrist = TimeSpan.FromDays(30);

    public async Task<int> CleanupAsync(DateTimeOffset? jetzt = null, CancellationToken ct = default)
    {
        var zeit = jetzt ?? DateTimeOffset.UtcNow;
        var grenze = Grenze(Vereinszeit.Tag(zeit));

        // Ohne die globalen Filter: Auch weich gelöschte Einsätze und ihre Reste sind hier fällig.
        var einsaetze = await db.ClubWorkEvents.IgnoreQueryFilters().Where(e => e.Date < grenze).ToListAsync(ct);
        if (einsaetze.Count > 0)
        {
            var ids = einsaetze.Select(e => e.Id).ToList();
            db.ClubWorkParticipations.RemoveRange(
                await db.ClubWorkParticipations.IgnoreQueryFilters().Where(p => ids.Contains(p.ClubWorkEventId)).ToListAsync(ct));
            db.ClubWorkEvents.RemoveRange(einsaetze);
            // Erst speichern: Die Prüfung auf verbleibende Teilnahmen unten soll das Ergebnis sehen.
            await db.SaveChangesAsync(ct);
        }

        var vor = zeit - GastFrist;
        db.ClubWorkGuests.RemoveRange(await db.ClubWorkGuests.IgnoreQueryFilters()
            .Where(g => g.CreatedAt < vor && !db.ClubWorkParticipations.IgnoreQueryFilters().Any(p => p.GuestId == g.Id))
            .ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        return einsaetze.Count;
    }
}
