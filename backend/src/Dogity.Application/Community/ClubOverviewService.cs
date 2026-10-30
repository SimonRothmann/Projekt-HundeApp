using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Die Vereinsübersicht für Vorstand und Verwaltung (siehe
/// <see cref="ClubOverviewDto"/>). Rechnet nur mit Vereinsdaten: Mitgliedschaften,
/// Rollen, Gruppen, Gruppentermine und Zusagen - keine Trainingstagebücher und
/// nichts zur App-Nutzung einzelner Personen. Damit entsteht keine neue
/// Datenart, nur ein neuer Empfängerkreis (der Vorstand).
///
/// Wie in AdminUsageService fassen Datenbank-Abfragen (GroupBy/Count) zusammen;
/// nur Dinge, die je Zeile von der Zeitzone abhängen (Monat eines Termins),
/// rechnen danach im Speicher - die Menge ist klein (6 Monate eines Vereins).
/// Gelöschte Mitgliedschaften, Gruppen und Termine blenden die globalen Filter
/// aus; Gruppendaten gelöschter Gruppen werden ausdrücklich ausgeschlossen,
/// weil ihre Mitglieder-/Termin-Zeilen stehen bleiben.
/// </summary>
public class ClubOverviewService(IApplicationDbContext db, IUserLookupService userLookup, TimeProvider timeProvider) : IClubOverviewService
{
    private const int Monate = 6;

    /// <summary>Die einzelnen Rollen in Anzeigereihenfolge der Oberfläche.</summary>
    private static readonly ClubRole[] Rollen =
    [
        ClubRole.Training, ClubRole.Ausbildungswart, ClubRole.Mitgliederverwaltung, ClubRole.Vorstand, ClubRole.Verwaltung,
    ];

    /// <summary>Recht prüfen; ein fremder oder fehlender Verein ist "nicht gefunden", wie bei den übrigen Vereinsendpunkten.</summary>
    private async Task<bool> DarfLesenAsync(Guid callerId, bool isAdmin, Guid clubId, CancellationToken ct)
    {
        if (!isAdmin && !await db.CanViewClubOverviewAsync(callerId, clubId, ct)) return false;
        return await db.Clubs.AnyAsync(c => c.Id == clubId, ct);
    }

    public async Task<Result<ClubOverviewDto>> GetOverviewAsync(Guid callerId, bool isAdmin, Guid clubId, CancellationToken ct = default)
    {
        if (!await DarfLesenAsync(callerId, isAdmin, clubId, ct))
            return Result<ClubOverviewDto>.NotFound("Verein nicht gefunden.");

        var jetzt = timeProvider.GetUtcNow();
        var seit30 = jetzt.AddDays(-30);

        var nachStatus = await db.ClubMemberships
            .Where(m => m.ClubId == clubId)
            .GroupBy(m => m.Status)
            .Select(g => new
            {
                Status = g.Key,
                Anzahl = g.Count(),
                Neu = g.Count(m => (m.DecidedAt ?? m.RequestedAt) >= seit30),
            })
            .ToListAsync(ct);
        var freigegeben = nachStatus.FirstOrDefault(s => s.Status == ClubMembershipStatus.Approved);

        var rollenmengen = await db.ClubTrainers.Where(t => t.ClubId == clubId).Select(t => t.Role).ToListAsync(ct);
        var rollen = Rollen.Select(r => new ClubOverviewRoleDto(r, rollenmengen.Count(m => (m & r) != 0))).ToList();

        var (gruppen, gruppenJePerson) = await GruppenAsync(clubId, ct);
        var freigegebeneIds = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Approved)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        return Result<ClubOverviewDto>.Success(new ClubOverviewDto(
            freigegeben?.Anzahl ?? 0,
            freigegeben?.Neu ?? 0,
            nachStatus.FirstOrDefault(s => s.Status == ClubMembershipStatus.Pending)?.Anzahl ?? 0,
            nachStatus.FirstOrDefault(s => s.Status == ClubMembershipStatus.Invited)?.Anzahl ?? 0,
            rollen,
            gruppen,
            freigegebeneIds.Count(id => !gruppenJePerson.ContainsKey(id)),
            await MonateAsync(clubId, jetzt, ct)));
    }

    /// <summary>
    /// Die Gruppen des Vereins mit Zahlen - und je Person die Namen der Gruppen, in
    /// denen sie Mitglied (aktiv) oder Trainer:in (Leitung, angenommene Mitbetreuung) ist.
    /// </summary>
    private async Task<(List<ClubOverviewGroupDto> Gruppen, Dictionary<Guid, List<string>> JePerson)> GruppenAsync(Guid clubId, CancellationToken ct)
    {
        var gruppen = await db.Groups
            .Where(g => g.ClubId == clubId)
            .Select(g => new { g.Id, g.Name, g.TrainerId })
            .AsNoTracking()
            .ToListAsync(ct);
        var ids = gruppen.Select(g => g.Id).ToList();
        var mitglieder = await db.GroupMembers
            .Where(m => ids.Contains(m.GroupId) && m.Status == GroupMemberStatus.Active)
            .Select(m => new { m.GroupId, m.UserId })
            .AsNoTracking()
            .ToListAsync(ct);
        // Der globale Filter lässt nur angenommene Mitbetreuung durch.
        var mitTrainer = await db.GroupTrainers
            .Where(t => ids.Contains(t.GroupId))
            .Select(t => new { t.GroupId, t.UserId })
            .AsNoTracking()
            .ToListAsync(ct);

        var jePerson = new Dictionary<Guid, List<string>>();
        var namen = gruppen.ToDictionary(g => g.Id, g => g.Name);
        void Zuordnen(Guid userId, Guid groupId)
        {
            if (!jePerson.TryGetValue(userId, out var liste)) jePerson[userId] = liste = [];
            if (!liste.Contains(namen[groupId])) liste.Add(namen[groupId]);
        }
        foreach (var g in gruppen) Zuordnen(g.TrainerId, g.Id);
        foreach (var m in mitglieder) Zuordnen(m.UserId, m.GroupId);
        foreach (var t in mitTrainer) Zuordnen(t.UserId, t.GroupId);

        var dtos = gruppen
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new ClubOverviewGroupDto(
                g.Id,
                g.Name,
                mitglieder.Count(m => m.GroupId == g.Id),
                // Die Leitung steht nicht zusätzlich bei den Mitbetreuenden (siehe GroupTrainer).
                1 + mitTrainer.Count(t => t.GroupId == g.Id && t.UserId != g.TrainerId)))
            .ToList();
        return (dtos, jePerson);
    }

    /// <summary>
    /// Termine und Zusagen je Kalendermonat (deutsche Zeit) für die letzten 6
    /// Monate bis einschließlich des laufenden. Zusagen zählen nur bei nicht
    /// abgesagten Terminen und nur von Personen, die jetzt aktives Mitglied der
    /// Gruppe sind (wie bei der Termin-Ansicht der Trainer:innen).
    /// </summary>
    private async Task<List<ClubOverviewMonthDto>> MonateAsync(Guid clubId, DateTimeOffset jetzt, CancellationToken ct)
    {
        var laufend = Vereinszeit.Tag(jetzt);
        var erster = new DateOnly(laufend.Year, laufend.Month, 1).AddMonths(-(Monate - 1));
        var von = Vereinszeit.TagesBeginn(erster);
        var bis = Vereinszeit.TagesBeginn(new DateOnly(laufend.Year, laufend.Month, 1).AddMonths(1));

        var termine = await db.GroupTrainingSessions
            .Where(s => s.ClubId == clubId && s.StartsAt >= von && s.StartsAt < bis)
            // Gelöschte Gruppen: ihre Termine bleiben stehen, zählen aber nicht mehr.
            .Where(s => db.Groups.Any(g => g.Id == s.GroupId))
            .Select(s => new { s.Id, s.StartsAt, s.Status })
            .AsNoTracking()
            .ToListAsync(ct);

        var zusagen = (await db.GroupTrainingSessionResponses
                .Where(r => r.IsAttending
                    && r.Session!.ClubId == clubId && r.Session.StartsAt >= von && r.Session.StartsAt < bis
                    && r.Session.Status == GroupTrainingSessionStatus.Planned
                    && db.GroupMembers.Any(m => m.GroupId == r.Session.GroupId && m.UserId == r.UserId && m.Status == GroupMemberStatus.Active)
                    && db.Groups.Any(g => g.Id == r.Session.GroupId))
                .GroupBy(r => r.GroupTrainingSessionId)
                .Select(g => new { SessionId = g.Key, Anzahl = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.SessionId, x => x.Anzahl);

        return Enumerable.Range(0, Monate)
            .Select(i => erster.AddMonths(i))
            .Select(monat =>
            {
                var imMonat = termine.Where(t =>
                {
                    var tag = Vereinszeit.Tag(t.StartsAt);
                    return tag.Year == monat.Year && tag.Month == monat.Month;
                }).ToList();
                var geplant = imMonat.Where(t => t.Status == GroupTrainingSessionStatus.Planned).ToList();
                return new ClubOverviewMonthDto(
                    monat.Year,
                    monat.Month,
                    geplant.Count,
                    imMonat.Count - geplant.Count,
                    geplant.Sum(t => zusagen.GetValueOrDefault(t.Id)));
            })
            .ToList();
    }

    public async Task<Result<IReadOnlyList<ClubOverviewMemberDto>>> GetMembersAsync(Guid callerId, bool isAdmin, Guid clubId, CancellationToken ct = default)
    {
        if (!await DarfLesenAsync(callerId, isAdmin, clubId, ct))
            return Result<IReadOnlyList<ClubOverviewMemberDto>>.NotFound("Verein nicht gefunden.");

        var mitglieder = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Approved)
            .Select(m => new { m.Id, m.UserId, Seit = m.DecidedAt ?? m.RequestedAt })
            .AsNoTracking()
            .ToListAsync(ct);

        var rollen = await db.ClubTrainers
            .Where(t => t.ClubId == clubId)
            .ToDictionaryAsync(t => t.UserId, t => t.Role, ct);
        var (_, gruppenJePerson) = await GruppenAsync(clubId, ct);
        // Nur Namen: Die Adresse aus dem Nachschlagen verlässt diese Methode nie.
        var lookup = await userLookup.FindByIdsAsync(mitglieder.Select(m => m.UserId).ToList(), ct);

        var liste = mitglieder
            .Select(m =>
            {
                var gruppen = gruppenJePerson.TryGetValue(m.UserId, out var g)
                    ? g.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList()
                    : new List<string>();
                lookup.TryGetValue(m.UserId, out var info);
                return new ClubOverviewMemberDto(m.Id, info?.FirstName ?? "", info?.LastName ?? "", m.Seit, gruppen, rollen.GetValueOrDefault(m.UserId));
            })
            .OrderBy(m => m.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return Result<IReadOnlyList<ClubOverviewMemberDto>>.Success(liste);
    }
}
