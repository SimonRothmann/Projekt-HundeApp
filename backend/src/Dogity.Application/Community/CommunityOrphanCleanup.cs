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
        // Anmeldungen zu Gruppen, die es nicht mehr gibt (gelöscht auf einem
        // Weg, der sie nicht mitnahm): Sie hängen an keinem Konto, sondern an
        // der Gruppe - und ohne Gruppe sieht sie niemand mehr, löschen kann sie
        // aber auch niemand. Daten von Menschen ohne Konto gehören nicht in
        // eine Datenbank, die keiner mehr verwaltet.
        // IgnoreQueryFilters gilt für die ganze Abfrage, auch für die Gruppen
        // darin - deshalb steht "nicht gelöscht" hier ausdrücklich.
        var anmeldungen = await GroupRegistrationErasure.EntfernenAsync(
            db,
            db.GroupRegistrations.IgnoreQueryFilters()
                .Where(r => !db.Groups.IgnoreQueryFilters().Any(g => g.Id == r.GroupId && g.DeletedAt == null)),
            ct);
        if (anmeldungen > 0) await db.SaveChangesAsync(ct);

        var mitgliedschaften = await db.ClubMemberships.ToListAsync(ct);
        var vereinstrainer = await db.ClubTrainers.ToListAsync(ct);
        var gruppenmitglieder = await db.GroupMembers.ToListAsync(ct);
        // Einschließlich offener Einladungen als Trainer:in.
        var gruppentrainer = await db.GroupTrainers.IgnoreQueryFilters().Where(t => t.DeletedAt == null).ToListAsync(ct);
        var zuweisungen = await db.TrainerAssignments.ToListAsync(ct);

        var betroffene = mitgliedschaften.Select(m => m.UserId)
            .Concat(vereinstrainer.Select(t => t.UserId))
            .Concat(gruppenmitglieder.Select(m => m.UserId))
            .Concat(gruppentrainer.Select(t => t.UserId))
            .Concat(zuweisungen.Select(a => a.TrainerId))
            .Concat(zuweisungen.Select(a => a.MemberId))
            .Distinct()
            .ToList();

        if (betroffene.Count == 0) return anmeldungen;

        var vorhanden = await users.FindByIdsAsync(betroffene, ct);
        var verwaist = betroffene.Where(id => !vorhanden.ContainsKey(id)).ToHashSet();
        if (verwaist.Count == 0) return anmeldungen;

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
        return entfernt + anmeldungen;
    }
}
