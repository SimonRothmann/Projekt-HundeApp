using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Welche Fassung einer Prüfungsordnung gerade gilt - die EINE Regel, nach der
/// Plan, Höchstpunkte, Prüfungsreife, Katalog und Pflege dieselbe Fassung meinen.
///
/// Gültig ist die jüngste Fassung, die schon begonnen hat (<see cref="RegulationVersion.ValidFrom"/>
/// bis heute in Vereinszeit); eine künftige gilt erst ab ihrem Tag. Beginnt am
/// selben Tag mehr als eine, gewinnt die später angelegte - zuletzt entscheidet
/// die Id, damit die Wahl nie vom Zufall der Datenbank abhängt.
///
/// Gibt es nur künftige Fassungen (eine Ordnung, die schon angeboten wird, bevor
/// sie in Kraft tritt, etwa die Hoopers- und Rally-Obedience-Ordnungen ab
/// 01.01.2027), gilt die früheste von ihnen: Sonst hätte die Ordnung bis zum
/// Stichtag keine Übungen.
/// </summary>
public static class RegulationVersionQueries
{
    public static RegulationVersion? AktuelleFassung(this IEnumerable<RegulationVersion> fassungen, DateOnly heute)
    {
        var liste = fassungen as IReadOnlyCollection<RegulationVersion> ?? fassungen.ToList();
        return liste
                .Where(v => v.ValidFrom <= heute)
                .OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.CreatedAt).ThenBy(v => v.Id)
                .FirstOrDefault()
            ?? liste
                .OrderBy(v => v.ValidFrom).ThenByDescending(v => v.CreatedAt).ThenBy(v => v.Id)
                .FirstOrDefault();
    }

    /// <summary>Die gültige Fassung je Ordnung; Ordnungen ohne Fassung fehlen.</summary>
    public static async Task<Dictionary<Guid, RegulationVersion>> AktuelleFassungenAsync(
        this IApplicationDbContext db, IReadOnlyCollection<Guid> ordnungIds, DateOnly heute, CancellationToken ct = default)
    {
        var fassungen = await db.RegulationVersions
            .Where(v => ordnungIds.Contains(v.RegulationId))
            .AsNoTracking()
            .ToListAsync(ct);
        return fassungen
            .GroupBy(v => v.RegulationId)
            .ToDictionary(g => g.Key, g => g.AktuelleFassung(heute)!);
    }

    /// <summary>Die gültige Fassung einer Ordnung; null, wenn sie keine hat.</summary>
    public static async Task<RegulationVersion?> AktuelleFassungAsync(
        this IApplicationDbContext db, Guid ordnungId, DateOnly heute, CancellationToken ct = default) =>
        (await db.RegulationVersions.Where(v => v.RegulationId == ordnungId).AsNoTracking().ToListAsync(ct)).AktuelleFassung(heute);
}
