using Dogity.Application.Community;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Gemeinsame Quelle der Wahrheit für "Trainings, die eine Trainer:in noch
/// bewerten muss" - Liste und Zähler lesen beide von hier.
/// </summary>
public static class TrainerSessionQueries
{
    /// <summary>
    /// So viele Wochen zurück (nach Trainingsdatum, nicht nach Anlegedatum)
    /// reicht die Bewertungsliste. Ehrenamtliche Trainer:innen sollen keine
    /// Liste vor sich haben, die nie abzutragen ist: Was länger her ist, wird
    /// nicht mehr nachgefragt - der Zähler auf der Übersicht bliebe sonst für
    /// immer über null und würde nicht mehr beachtet.
    /// </summary>
    public const int BewertungsWochen = 8;

    /// <summary>
    /// Trainings betreuter Hunde, die noch etwas brauchen: kein Gesamt-Feedback
    /// ODER mindestens eine unbewertete Übung.
    ///
    /// Eine Definition für die Liste (TrainingService.GetSessionsToRateAsync)
    /// und den Zähler auf der Trainer-Übersicht - sonst zeigte der Zähler eine
    /// andere Zahl, als hinter dem Antippen steht. Ältere Trainings als
    /// <see cref="BewertungsWochen"/> Wochen zählen nicht mehr.
    /// </summary>
    public static IQueryable<TrainingSession> TrainingSessionsToRate(this IApplicationDbContext db, IReadOnlyCollection<Guid> assignedDogIds)
    {
        var ab = Vereinszeit.Heute().AddDays(-7 * BewertungsWochen);
        return db.TrainingSessions
            .Where(s => assignedDogIds.Contains(s.DogId))
            .Where(s => s.Date >= ab)
            .Where(s => s.TrainerFeedback == null || s.Exercises.Any(e => e.TrainerRating == null));
    }
}
