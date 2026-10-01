using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Entfernt Anmeldungen samt Anwesenheit endgültig aus der Datenbank.
///
/// Eine Stelle für alle Löschwege (Gruppe löschen, Konto der letzten
/// Trainer:in gelöscht, Aufräumen verwaister Zeilen, Aufbewahrungsfrist):
/// Die Angemeldeten haben kein Konto und können ihre Daten nicht selbst
/// löschen - der Verein muss sie wirklich loswerden können (Art. 17 DSGVO).
/// Ein DeletedAt-Vermerk ließe Namen und Telefonnummern stehen.
///
/// Speichert nicht selbst: Der Aufrufer bündelt es mit seinen übrigen
/// Änderungen in einem SaveChanges.
/// </summary>
public static class GroupRegistrationErasure
{
    public static Task<int> EntfernenFuerGruppenAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> groupIds, CancellationToken ct = default) =>
        EntfernenAsync(db, db.GroupRegistrations.IgnoreQueryFilters().Where(r => groupIds.Contains(r.GroupId)), ct);

    /// <returns>Wie viele Anmeldungen entfernt wurden.</returns>
    public static async Task<int> EntfernenAsync(
        IApplicationDbContext db,
        IQueryable<Domain.Community.GroupRegistration> anmeldungen,
        CancellationToken ct = default)
    {
        var zeilen = await anmeldungen.ToListAsync(ct);
        if (zeilen.Count == 0) return 0;

        var ids = zeilen.Select(r => r.Id).ToList();
        // Auch weich entfernte Haken: Sie hängen an derselben Person.
        var haken = await db.GroupRegistrationAttendances.IgnoreQueryFilters()
            .Where(a => ids.Contains(a.RegistrationId))
            .ToListAsync(ct);
        db.GroupRegistrationAttendances.RemoveRange(haken);
        db.GroupRegistrations.RemoveRange(zeilen);
        return zeilen.Count;
    }
}
