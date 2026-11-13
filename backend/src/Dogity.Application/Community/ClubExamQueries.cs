using Dogity.Application.Abstractions;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>Abfragen, die Prüfungsliste, Vereinsliste und Detail gemeinsam brauchen.</summary>
public static class ClubExamQueries
{
    /// <summary>
    /// Die Ordnungen je Prüfungstag: die angebotenen und die der Einträge zusammen, nach Name sortiert.
    /// Ordnungen, die es im Katalog nicht mehr gibt, fehlen.
    /// </summary>
    public static async Task<Dictionary<Guid, List<ClubExamRegulationDto>>> OrdnungenJePruefungAsync(
        this IApplicationDbContext db, IReadOnlyCollection<Guid> examIds, CancellationToken ct = default)
    {
        var angeboten = await db.ClubExamRegulations
            .Where(r => examIds.Contains(r.ExamId))
            .Select(r => new { r.ExamId, r.RegulationId })
            .ToListAsync(ct);
        var eingetragen = await db.ClubExamEntries
            .Where(e => examIds.Contains(e.ExamId))
            .Select(e => new { e.ExamId, e.RegulationId })
            .ToListAsync(ct);
        var paare = angeboten.Concat(eingetragen).Distinct().ToList();

        var ordnungIds = paare.Select(p => p.RegulationId).Distinct().ToList();
        var namen = await db.Regulations
            .Where(r => ordnungIds.Contains(r.Id))
            .Select(r => new ClubExamRegulationDto(r.Id, r.Name, r.NameEn, null))
            .ToDictionaryAsync(r => r.Id, ct);

        return paare
            .Where(p => namen.ContainsKey(p.RegulationId))
            .GroupBy(p => p.ExamId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => namen[p.RegulationId]).OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    /// <summary>
    /// Der nächste anstehende Prüfungstag je Verein (nicht abgesagt, heute oder später) - nur Datum, Ort und
    /// Ordnungen, keine Meldeliste. Vereine ohne anstehenden Tag fehlen.
    /// </summary>
    public static async Task<Dictionary<Guid, ClubExamHinweisDto>> NaechstePruefungenAsync(
        this IApplicationDbContext db, IReadOnlyCollection<Guid> clubIds, DateOnly heute, CancellationToken ct = default)
    {
        if (clubIds.Count == 0) return [];

        var tage = await db.ClubExams
            .Where(e => clubIds.Contains(e.ClubId) && e.Status == ClubExamStatus.Planned && e.Date >= heute)
            .Select(e => new { e.Id, e.ClubId, e.Date, e.StartTime, e.Location })
            .AsNoTracking()
            .ToListAsync(ct);
        var naechste = tage
            .GroupBy(e => e.ClubId)
            .Select(g => g.OrderBy(e => e.Date).ThenBy(e => e.StartTime).First())
            .ToList();
        var ordnungen = await db.OrdnungenJePruefungAsync(naechste.Select(e => e.Id).ToList(), ct);

        return naechste.ToDictionary(
            e => e.ClubId,
            e => new ClubExamHinweisDto(e.Date, e.Location, ordnungen.GetValueOrDefault(e.Id) ?? []));
    }
}
