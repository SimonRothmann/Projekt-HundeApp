using Dogity.Application.Abstractions;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

public interface IClubRoleBackfill
{
    Task<int> BackfillAsync(CancellationToken ct = default);
}

/// <summary>
/// Gibt bestehenden Trainer-Zuweisungen die Rolle <c>Verwaltung</c>.
///
/// Vor der Einführung von <see cref="ClubRole"/> durfte jede zugewiesene
/// Trainerin alles, was ein Verein mit sich selbst tun kann. Bekämen alle
/// beim Umstieg die schwächere Rolle, verlöre jeder bestehende Verein von
/// einem Tag auf den anderen die Fähigkeit, sich zu verwalten - und niemand
/// außer einem globalen Admin könnte das zurückholen. Deshalb erben alle,
/// die vorher da waren, die stärkere Rolle.
///
/// Läuft einmalig beim Start und ist idempotent: Sobald jeder Verein
/// mindestens eine verwaltende Person hat, tut der Lauf nichts mehr.
/// </summary>
public class ClubRoleBackfill(IApplicationDbContext db) : IClubRoleBackfill
{
    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        // Nur Vereine ohne jede verwaltende Person anfassen. So bleiben
        // spätere, bewusst vergebene Trainer-Rollen unangetastet - der Lauf
        // darf eine Herabstufung nicht wieder aufheben.
        var vereineOhneVerwaltung = await db.ClubTrainers
            .GroupBy(t => t.ClubId)
            .Where(g => g.All(t => t.Role != ClubRole.Verwaltung))
            .Select(g => g.Key)
            .ToListAsync(ct);

        if (vereineOhneVerwaltung.Count == 0) return 0;

        var betroffene = await db.ClubTrainers
            .Where(t => vereineOhneVerwaltung.Contains(t.ClubId))
            .ToListAsync(ct);

        foreach (var zuweisung in betroffene)
        {
            zuweisung.Role = ClubRole.Verwaltung;
            zuweisung.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return betroffene.Count;
    }
}
