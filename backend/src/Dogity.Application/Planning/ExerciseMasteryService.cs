using Dogity.Application.Abstractions;
using Dogity.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Planning;

/// <inheritdoc />
public class ExerciseMasteryService(IApplicationDbContext db) : IExerciseMasteryService
{
    // Leitner-Intervalle je Box in Tagen (Box 1..5), siehe docs/SMART_TRAINING_PLAN.md:
    // schwach (Box 1) alle 2 Tage fällig, gemeistert (Box 5) erst nach 28 Tagen.
    private static readonly int[] IntervalDaysByBox = [2, 4, 7, 14, 28];

    public async Task ApplyLogAsync(Guid dogId, Guid exerciseId, int rating, bool success, DateOnly date, CancellationToken ct = default)
    {
        // Erst im lokalen Change-Tracker suchen (falls dieselbe Übung mehrfach in
        // einer Einheit vorkommt und die Zeile in diesem Request neu angelegt
        // wurde), sonst aus der DB laden.
        var mastery = db.ExerciseMasteries.Local.FirstOrDefault(m => m.DogId == dogId && m.ExerciseId == exerciseId)
            ?? await db.ExerciseMasteries.FirstOrDefaultAsync(m => m.DogId == dogId && m.ExerciseId == exerciseId, ct);
        if (mastery is null)
        {
            mastery = new ExerciseMastery { DogId = dogId, ExerciseId = exerciseId };
            db.ExerciseMasteries.Add(mastery);
        }

        ApplyOutcome(mastery, rating, success, date);
    }

    public async Task BackfillIfEmptyAsync(CancellationToken ct = default)
    {
        if (await db.ExerciseMasteries.AnyAsync(ct))
            return;

        // Alle Katalog-Übungs-Logs chronologisch abspielen. Der SelectMany über
        // die Session->Übungen-Navigation respektiert die Soft-Delete-Filter auf
        // Session UND Übung - gelöschte Einheiten/Übungen fließen nicht ein.
        var logs = await db.TrainingSessions
            .SelectMany(s => s.Exercises
                .Where(e => e.ExerciseId != null)
                .Select(e => new { s.DogId, s.Date, ExerciseId = e.ExerciseId!.Value, e.Rating, e.Success }))
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        var byKey = new Dictionary<(Guid Dog, Guid Exercise), ExerciseMastery>();
        foreach (var log in logs)
        {
            var key = (log.DogId, log.ExerciseId);
            if (!byKey.TryGetValue(key, out var mastery))
            {
                mastery = new ExerciseMastery { DogId = log.DogId, ExerciseId = log.ExerciseId };
                byKey[key] = mastery;
                db.ExerciseMasteries.Add(mastery);
            }

            ApplyOutcome(mastery, log.Rating, log.Success, log.Date);
        }

        if (byKey.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reine Leitner-/EMA-Aktualisierung eines Mastery-Zustands anhand eines
    /// Trainingsausgangs (statisch/deterministisch - identisch für Live-Pfad,
    /// Backfill und Tests).
    /// </summary>
    public static void ApplyOutcome(ExerciseMastery m, int rating, bool success, DateOnly date)
    {
        // Leitner-Box: gut gemeistert -> hoch (längeres Intervall), schwach ->
        // runter (kommt schneller wieder), mittel (rating 3, erfolgreich) bleibt.
        if (success && rating >= 4)
            m.Box = Math.Min(5, m.Box + 1);
        else if (!success || rating <= 2)
            m.Box = Math.Max(1, m.Box - 1);

        // Gewichteter Schnitt (jüngere Bewertungen stärker); die erste Bewertung
        // setzt den Startwert.
        m.RecentAvgRating = m.SessionCount == 0 ? rating : m.RecentAvgRating * 0.6 + rating * 0.4;
        m.SessionCount += 1;

        var trainedAt = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        m.LastTrainedAt = trainedAt;
        m.DueAt = trainedAt.AddDays(IntervalDaysByBox[m.Box - 1]);
        m.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
