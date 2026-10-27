using Dogity.Application.Abstractions;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Dogs;

/// <summary>
/// Der Gesundheitsstand der Hunde für alle Stellen, an denen Besitzer:innen ihre Hunde ohnehin
/// laden (Hundeliste, Hundeseite, Startseite): eine Abfrage für alle Hunde, kein Nachladen je Hund.
/// Der Aufrufer entscheidet, WEM er das zeigt - Trainer:innen nie.
/// </summary>
public static class DogHealthQueries
{
    public static DogHealthRecordDto ToDto(DogHealthRecord r, bool aktuell) =>
        new(r.Id, r.Kind, r.Title, r.Date, r.Time, r.DueDate, r.Note, aktuell);

    public static async Task<Dictionary<Guid, DogHealthSummaryDto>> HealthSummariesAsync(
        this IApplicationDbContext db, IReadOnlyCollection<Guid> dogIds, DateOnly heute, CancellationToken ct = default)
    {
        if (dogIds.Count == 0) return [];

        // Ohne die Notiz: Für den Stand wird sie nicht gebraucht.
        var eintraege = await db.DogHealthRecords
            .Where(r => dogIds.Contains(r.DogId))
            .Select(r => new DogHealthRecord
            {
                Id = r.Id, DogId = r.DogId, Kind = r.Kind, Title = r.Title, Date = r.Date,
                Time = r.Time, DueDate = r.DueDate, CreatedAt = r.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        return Gesundheitsstand.Zusammenfassungen(eintraege, heute);
    }
}
