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
    /// Trainings betreuter Hunde, die noch etwas brauchen: kein Gesamt-Feedback
    /// ODER mindestens eine unbewertete Übung.
    ///
    /// Eine Definition für die Liste (TrainingService.GetSessionsToRateAsync)
    /// und den Zähler auf der Trainer-Übersicht - sonst zeigte der Zähler eine
    /// andere Zahl, als hinter dem Antippen steht.
    /// </summary>
    public static IQueryable<TrainingSession> TrainingSessionsToRate(this IApplicationDbContext db, IReadOnlyCollection<Guid> assignedDogIds) =>
        db.TrainingSessions
            .Where(s => assignedDogIds.Contains(s.DogId))
            .Where(s => s.TrainerFeedback == null || s.Exercises.Any(e => e.TrainerRating == null));
}
