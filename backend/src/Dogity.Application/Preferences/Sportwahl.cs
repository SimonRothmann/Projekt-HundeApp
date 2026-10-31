using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Preferences;

/// <summary>
/// Welche Sportarten für einen Hund gelten - die EINE Vererbungsregel: Ein Hund
/// mit eigener Auswahl (<c>Dog.UsesOwnSports</c>) zählt seine eigenen
/// Sportarten, sonst erbt er die des Menschen. Eine leere Auswahl heißt "keine
/// Einschränkung"; das entscheiden die Aufrufer.
///
/// Als Stapel für viele Nutzer und Hunde formuliert (der Erinnerungsdienst
/// braucht das), die Einstellungen fragen mit einem Hund. Das Frontend baut
/// die Regel nicht nach, es bekommt das Ergebnis geliefert.
/// </summary>
public static class Sportwahl
{
    /// <param name="nutzerIds">Die Menschen, deren Auswahl vererbt werden kann.</param>
    /// <param name="hundeMitEigenenSportarten">Die Hunde, die ihre eigene Auswahl führen.</param>
    /// <returns>(Nutzer, Hund, Hund führt eigene Auswahl) -> die gültigen Sportart-Ids.</returns>
    public static async Task<Func<Guid, Guid, bool, IReadOnlyList<Guid>>> LadenAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> nutzerIds,
        IReadOnlyCollection<Guid> hundeMitEigenenSportarten,
        CancellationToken ct)
    {
        var vomHund = hundeMitEigenenSportarten.Count == 0
            ? Enumerable.Empty<Guid>().ToLookup(_ => Guid.Empty)
            : (await db.DogSportSelections
                    .Where(s => hundeMitEigenenSportarten.Contains(s.DogId))
                    .Select(s => new { s.DogId, s.SportId })
                    .ToListAsync(ct))
                .ToLookup(s => s.DogId, s => s.SportId);

        var vomMenschen = (await db.UserSportSelections
                .Where(s => nutzerIds.Contains(s.UserPreference!.UserId))
                .Select(s => new { s.UserPreference!.UserId, s.SportId })
                .ToListAsync(ct))
            .ToLookup(s => s.UserId, s => s.SportId);

        return (nutzerId, hundId, eigene) => eigene ? vomHund[hundId].ToList() : vomMenschen[nutzerId].ToList();
    }
}
