using Dogity.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Löst Mitglied-Teams der Prüfungen von ihrer Person: Verlässt jemand den Verein, oder wird der
/// Hund oder das Konto gelöscht, werden MemberUserId, DogId und GoalId des Eintrags leer. Das Ergebnis
/// bleibt für die Vereinszahlen; die Anzeige schreibt "Ehemaliges Mitglied". Die Methoden speichern nicht -
/// der Aufrufer tut es mit seinen eigenen Änderungen.
///
/// Ohne Query-Filter: Auch Einträge einer gelöschten Prüfung sollen die Person nicht mehr tragen.
/// </summary>
public static class ClubExamAnonymisierung
{
    /// <summary>Die Einträge einer Person in den Prüfungen EINES Vereins (Austritt).</summary>
    public static async Task MitgliedAusVereinAsync(IApplicationDbContext db, Guid clubId, Guid userId, CancellationToken ct = default) =>
        Loesen(await db.ClubExamEntries.IgnoreQueryFilters()
            .Where(e => e.MemberUserId == userId && db.ClubExams.IgnoreQueryFilters().Any(x => x.Id == e.ExamId && x.ClubId == clubId))
            .ToListAsync(ct));

    /// <summary>Die Einträge von Personen in ALLEN Vereinen (Konto gelöscht).</summary>
    public static async Task MitgliederEntfernenAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        Loesen(await db.ClubExamEntries.IgnoreQueryFilters()
            .Where(e => e.MemberUserId != null && userIds.Contains(e.MemberUserId.Value))
            .ToListAsync(ct));

    /// <summary>Die Einträge von Hunden (Hund gelöscht).</summary>
    public static async Task HundeEntfernenAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> dogIds, CancellationToken ct = default) =>
        Loesen(await db.ClubExamEntries.IgnoreQueryFilters()
            .Where(e => e.DogId != null && dogIds.Contains(e.DogId.Value))
            .ToListAsync(ct));

    private static void Loesen(List<Domain.Community.ClubExamEntry> eintraege)
    {
        foreach (var e in eintraege)
        {
            e.MemberUserId = null;
            e.DogId = null;
            e.GoalId = null;
            e.GoalCreated = false;
        }
    }
}
