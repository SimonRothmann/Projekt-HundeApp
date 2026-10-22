using Dogity.Application.Abstractions;
using Dogity.Application.Planning;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Dogs;

/// <summary>
/// Die Pausen eines Hundes für alle Stellen, an denen der Hund ohnehin geladen
/// wird (Hundeliste, Hundeseite, Startseite, Trainerübersicht, Ziele). Eine
/// Stelle für die Sichtbarkeitsregel: Grund und Notiz nur mit
/// <c>zeigeDetails</c> - und das tun nur Besitzer:innen.
/// </summary>
public static class DogPauseQueries
{
    public static DogPauseDto ToDto(DogPause p, bool zeigeDetails) =>
        new(p.Id, p.StartDate, p.EndDate, zeigeDetails ? p.Reason : null, zeigeDetails ? p.Note : null);

    /// <summary>Die Pause, die an <paramref name="heute"/> gilt, je Hund (höchstens eine; bei Altbestand die jüngste).</summary>
    public static async Task<Dictionary<Guid, DogPause>> ActivePausesAsync(
        this IApplicationDbContext db, IReadOnlyCollection<Guid> dogIds, DateOnly heute, CancellationToken ct = default)
    {
        if (dogIds.Count == 0) return [];

        var pausen = await db.DogPauses
            .Where(p => dogIds.Contains(p.DogId) && p.StartDate <= heute && (p.EndDate == null || p.EndDate >= heute))
            .AsNoTracking()
            .ToListAsync(ct);

        return pausen
            .GroupBy(p => p.DogId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.StartDate).First());
    }

    /// <summary>Alle Pausenzeiträume der Hunde (ohne Grund und Notiz) - für die Planrechnung.</summary>
    public static async Task<Dictionary<Guid, List<PausenZeitraum>>> PauseZeitraeumeAsync(
        this IApplicationDbContext db, IReadOnlyCollection<Guid> dogIds, CancellationToken ct = default)
    {
        if (dogIds.Count == 0) return [];

        var zeilen = await db.DogPauses
            .Where(p => dogIds.Contains(p.DogId))
            .Select(p => new { p.DogId, p.StartDate, p.EndDate })
            .AsNoTracking()
            .ToListAsync(ct);

        return zeilen
            .GroupBy(z => z.DogId)
            .ToDictionary(g => g.Key, g => g.Select(z => new PausenZeitraum(z.StartDate, z.EndDate)).ToList());
    }
}
