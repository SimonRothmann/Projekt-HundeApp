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

    public async Task RecomputeAsync(Guid dogId, Guid exerciseId, CancellationToken ct = default)
    {
        var mastery = await db.ExerciseMasteries.FirstOrDefaultAsync(m => m.DogId == dogId && m.ExerciseId == exerciseId, ct);
        if (mastery is null)
        {
            mastery = new ExerciseMastery { DogId = dogId, ExerciseId = exerciseId };
            db.ExerciseMasteries.Add(mastery);
        }
        else
        {
            // Auf den Startzustand zurücksetzen und die Historie neu abspielen.
            // ManualPriority bleibt stehen: die Gewichtung "mehr/weniger üben"
            // ist eine Entscheidung des Nutzers, keine Folge der Historie.
            mastery.Box = 1;
            mastery.LastTrainedAt = null;
            mastery.DueAt = null;
            mastery.RecentAvgRating = 0;
            mastery.SessionCount = 0;
        }

        // Wie im Backfill: der SelectMany über die Navigation respektiert die
        // Soft-Delete-Filter auf Einheit UND Übung.
        var logs = await db.TrainingSessions
            .Where(s => s.DogId == dogId)
            .SelectMany(s => s.Exercises
                .Where(e => e.ExerciseId == exerciseId)
                .Select(e => new { s.Date, e.Rating, e.Success }))
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        foreach (var log in logs)
            ApplyOutcome(mastery, log.Rating, log.Success, log.Date);

        mastery.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public async Task SetManualPriorityAsync(Guid dogId, Guid exerciseId, int value, CancellationToken ct = default)
    {
        var clamped = Math.Clamp(value, -2, 2);

        var mastery = await db.ExerciseMasteries.FirstOrDefaultAsync(m => m.DogId == dogId && m.ExerciseId == exerciseId, ct);
        if (mastery is null)
        {
            // Übung wurde für diesen Hund noch nie trainiert - Zeile anlegen, damit
            // die Gewichtung ab dem nächsten Wochen-Neuaufbau greift (Box/History
            // bleiben auf Startwerten).
            mastery = new ExerciseMastery { DogId = dogId, ExerciseId = exerciseId };
            db.ExerciseMasteries.Add(mastery);
        }

        mastery.ManualPriority = clamped;
        mastery.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
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
