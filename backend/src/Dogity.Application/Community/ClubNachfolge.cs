using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>Wer zur Verwaltung eines Vereins aufgerückt ist.</summary>
public record Befoerderung(Guid UserId, string VereinName);

/// <summary>
/// Nachfolge, wenn die letzte verwaltende Person eines Vereins wegfällt
/// (Kontolöschung in <c>AccountDataService.PurgeAsync</c>, Aufräumen in
/// <c>CommunityOrphanCleanup</c>): Die dienstälteste verbleibende Person mit
/// einer Trainer-Rolle wird zur Verwaltung (Mitgliederverwaltung und Vorstand
/// behält sie). Ohne Nachfolge bliebe ein Verein zurück, den niemand mehr
/// verwalten kann - nur ein globaler Admin käme heran.
///
/// Dienstalter ist das Anlagedatum der Trainer-Zuweisung; bei Gleichstand
/// entscheidet die Id, damit das Ergebnis nicht vom Zufall abhängt. Gibt es
/// keine Trainer:in mehr, bleibt der Verein ohne Verwaltung (ein Admin setzt
/// dann jemanden ein) - das ist kein Fehler. Eine zweite Verwaltung genügt:
/// Dann rückt niemand nach.
///
/// Zwei Schritte, weil die Rolle zusammen mit dem Entfernen der
/// ausscheidenden Person gespeichert werden muss (sonst bliebe bei einem
/// Abbruch dazwischen ein Verein ohne Verwaltung), die Meldung aber erst nach
/// dem Speichern hinausgehen darf. Speichert nicht selbst.
/// </summary>
public static class ClubNachfolge
{
    /// <summary>
    /// Befördert in jedem Verein, in dem eine der <paramref name="wegfallende"/>
    /// Personen verwaltet und danach niemand mehr verwaltet. Die Zeilen der
    /// ausscheidenden Personen müssen dafür noch nicht entfernt sein.
    /// </summary>
    public static async Task<IReadOnlyList<Befoerderung>> BefoerdernAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> wegfallende,
        CancellationToken ct = default)
    {
        var weg = wegfallende.ToList();
        var befoerderungen = new List<Befoerderung>();

        var vereine = await db.ClubTrainers
            .Where(t => weg.Contains(t.UserId))
            .MitRolle(ClubRole.Verwaltung)
            .Select(t => t.ClubId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var vereinId in vereine)
        {
            var verein = await db.Clubs.Where(c => c.Id == vereinId).Select(c => c.Name).FirstOrDefaultAsync(ct);
            if (verein is null) continue;

            var uebrige = await db.ClubTrainers
                .Where(t => t.ClubId == vereinId && !weg.Contains(t.UserId))
                .ToListAsync(ct);
            if (uebrige.Any(t => t.Role.HasFlag(ClubRole.Verwaltung))) continue;

            // Nur wer Trainer:in ist: Mitgliederverwaltung und Vorstand sind
            // keine Nachfolge für die Leitung eines Vereins.
            var nachfolge = uebrige.Where(t => (t.Role & ClubRollen.Trainer) != 0).OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).FirstOrDefault();
            if (nachfolge is null) continue;

            // Verwaltung schließt die Trainer-Rollen ein - sie ersetzen sich, wie bisher
            // Training durch Verwaltung ersetzt wurde. Mitgliederverwaltung und Vorstand
            // bleiben, sie sind nicht eingeschlossen.
            nachfolge.Role = (nachfolge.Role & ~ClubRollen.Trainer) | ClubRole.Verwaltung;
            nachfolge.UpdatedAt = DateTimeOffset.UtcNow;
            befoerderungen.Add(new Befoerderung(nachfolge.UserId, verein));
        }

        return befoerderungen;
    }

    /// <summary>Sagt den Aufgerückten Bescheid - nach dem Speichern aufrufen.</summary>
    public static async Task MeldenAsync(
        INotificationService notifications,
        IReadOnlyList<Befoerderung> befoerderungen,
        CancellationToken ct = default)
    {
        foreach (var b in befoerderungen)
            await notifications.CreateAsync(b.UserId, Meldung.Von("Du verwaltest jetzt den Verein \"{verein}\".", ("verein", b.VereinName)), "/trainer", ct);
    }
}
