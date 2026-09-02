using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Räumt Vereins- und Gruppenzeilen weg, deren Nutzer es nicht mehr gibt.
///
/// Bis zur Korrektur an <c>AdminService.DeleteUserAsync</c> hat das Löschen
/// eines Kontos seine Mitgliedschaften, Trainerzeilen und Zuweisungen stehen
/// lassen - sie verweisen nur über die UserId auf das Konto, ohne
/// Fremdschlüssel. Übrig blieben unter anderem Beitrittsanfragen, die in der
/// Liste einer Trainerin als "(unbekannt)" stehen und sich nicht mehr auflösen
/// lassen (auf der Testumgebung waren es zwölf).
///
/// Läuft einmal beim Start und ist idempotent: Was einmal weg ist, findet der
/// nächste Lauf nicht mehr.
/// </summary>
public interface ICommunityOrphanCleanup
{
    /// <returns>Wie viele Zeilen entfernt wurden.</returns>
    Task<int> CleanupAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public class CommunityOrphanCleanup(IApplicationDbContext db, IUserLookupService users) : ICommunityOrphanCleanup
{
    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        var mitgliedschaften = await db.ClubMemberships.ToListAsync(ct);
        var vereinstrainer = await db.ClubTrainers.ToListAsync(ct);
        var gruppenmitglieder = await db.GroupMembers.ToListAsync(ct);
        var gruppentrainer = await db.GroupTrainers.ToListAsync(ct);
        var zuweisungen = await db.TrainerAssignments.ToListAsync(ct);

        var betroffene = mitgliedschaften.Select(m => m.UserId)
            .Concat(vereinstrainer.Select(t => t.UserId))
            .Concat(gruppenmitglieder.Select(m => m.UserId))
            .Concat(gruppentrainer.Select(t => t.UserId))
            .Concat(zuweisungen.Select(a => a.TrainerId))
            .Concat(zuweisungen.Select(a => a.MemberId))
            .Distinct()
            .ToList();

        if (betroffene.Count == 0) return 0;

        var vorhanden = await users.FindByIdsAsync(betroffene, ct);
        var verwaist = betroffene.Where(id => !vorhanden.ContainsKey(id)).ToHashSet();
        if (verwaist.Count == 0) return 0;

        var jetzt = DateTimeOffset.UtcNow;
        var entfernt = 0;

        void Weg(IEnumerable<Domain.Common.Entity> zeilen)
        {
            foreach (var zeile in zeilen)
            {
                zeile.DeletedAt = jetzt;
                entfernt++;
            }
        }

        Weg(mitgliedschaften.Where(m => verwaist.Contains(m.UserId)));
        Weg(vereinstrainer.Where(t => verwaist.Contains(t.UserId)));
        Weg(gruppenmitglieder.Where(m => verwaist.Contains(m.UserId)));
        Weg(gruppentrainer.Where(t => verwaist.Contains(t.UserId)));
        Weg(zuweisungen.Where(a => verwaist.Contains(a.TrainerId) || verwaist.Contains(a.MemberId)));

        if (entfernt > 0) await db.SaveChangesAsync(ct);
        return entfernt;
    }
}
