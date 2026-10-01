using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Löscht Anmeldungen, an denen seit zwölf Monaten nichts mehr passiert ist.
///
/// Die Angemeldeten sind Menschen ohne Konto: Niemand pflegt ihre Daten, und
/// niemand wird sie je selbst löschen. Wer nach einem Jahr weder gekommen noch
/// angefasst worden ist, braucht für die Organisation der Gruppe keine
/// Telefonnummer mehr - das steht so in der Datenschutzerklärung.
///
/// "Aktivität" ist das Späteste aus: Anlegen in Dogity, letzte Änderung (auch
/// "bezahlt" und jeder Haken setzen UpdatedAt) und der letzte Haken. Bewusst
/// nicht der Zeitstempel aus dem Google-Import: Eine übernommene Anmeldung von
/// vor 14 Monaten wäre sonst am nächsten Tag wieder weg, ohne dass jemand sie
/// je in Dogity gesehen hätte.
/// </summary>
public interface IGroupRegistrationRetention
{
    /// <returns>Wie viele Anmeldungen entfernt wurden.</returns>
    Task<int> CleanupAsync(DateTimeOffset? jetzt = null, CancellationToken ct = default);
}

/// <inheritdoc />
public class GroupRegistrationRetention(IApplicationDbContext db) : IGroupRegistrationRetention
{
    /// <summary>Aufbewahrungsfrist in Monaten.</summary>
    public const int Monate = 12;

    public async Task<int> CleanupAsync(DateTimeOffset? jetzt = null, CancellationToken ct = default)
    {
        var grenze = (jetzt ?? DateTimeOffset.UtcNow).AddMonths(-Monate);

        // Ohne die globalen Filter: Auch Anmeldungen einer längst gelöschten
        // Gruppe, die ein früherer Löschweg übersehen hat, sind hier fällig.
        var kandidaten = await db.GroupRegistrations.IgnoreQueryFilters()
            .Where(r => r.CreatedAt < grenze && (r.UpdatedAt == null || r.UpdatedAt < grenze))
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (kandidaten.Count == 0) return 0;

        // Ein Haken jüngeren Datums hält die Anmeldung am Leben, auch wenn die
        // Zeile selbst alt aussieht (etwa von Hand in der Datenbank gesetzt).
        var aktiv = await db.GroupRegistrationAttendances.IgnoreQueryFilters()
            .Where(a => kandidaten.Contains(a.RegistrationId) && a.MarkedAt >= grenze)
            .Select(a => a.RegistrationId)
            .Distinct()
            .ToListAsync(ct);
        var faellig = kandidaten.Except(aktiv).ToList();
        if (faellig.Count == 0) return 0;

        await GroupRegistrationErasure.EntfernenAsync(
            db, db.GroupRegistrations.IgnoreQueryFilters().Where(r => faellig.Contains(r.Id)), ct);
        await db.SaveChangesAsync(ct);
        return faellig.Count;
    }
}
