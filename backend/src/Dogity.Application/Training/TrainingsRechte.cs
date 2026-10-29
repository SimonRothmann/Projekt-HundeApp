using Dogity.Application.Abstractions;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Training;

/// <summary>
/// Wer ein Training ändern oder löschen darf - samt Übungen, Bewertungen,
/// Notizen, Ort/Zeit und Fährten: Besitzer:innen jedes Training ihres Hundes,
/// betreuende Trainer:innen nur die Trainings, die sie selbst eingetragen
/// haben. Einträge der Familie sehen sie und geben dort Feedback
/// (Entscheidung des Betreibers 2026-10-29, docs/BERECHTIGUNGEN.md).
///
/// Damit das hält, darf ein Eintrag der Trainer:in nie in der Tageseinheit der
/// Familie landen und umgekehrt (<see cref="EinheitDesTagesAsync"/>) - sonst
/// könnte eine Trainer:in über ihre eigene Einheit Übungen der Familie löschen.
/// </summary>
public static class TrainingsRechte
{
    public static IQueryable<TrainingSession> DarfBearbeiten(this IQueryable<TrainingSession> sessions, IApplicationDbContext db, Guid userId) =>
        sessions.Where(s =>
            db.EigeneHundeIds(userId).Contains(s.DogId) ||
            (s.UserId == userId && db.ZugaenglicheHundeIds(userId).Contains(s.DogId)));

    /// <summary>
    /// Dieselbe Regel für ein bereits geladenes Training, das die Person sehen
    /// darf (Lesezugriff ist geprüft) - für die Anzeige der Bearbeiten-Knöpfe.
    /// <paramref name="eingetragenVon"/> ist TrainingSession.UserId.
    /// </summary>
    public static bool DarfBearbeiten(Guid eingetragenVon, Guid userId, bool istBesitzer) =>
        istBesitzer || eingetragenVon == userId;

    /// <summary>
    /// Ein Tag, eine Einheit: die Einheit, an die ein neuer Eintrag von
    /// <paramref name="userId"/> angehängt wird, oder null für eine neue. Für
    /// Besitzer:innen die älteste Einheit der Familie an diesem Tag, für
    /// Trainer:innen ihre eigene. Gemeinsam für Trainings und Fährten, damit
    /// beide dieselbe Einheit wählen.
    /// </summary>
    public static async Task<TrainingSession?> EinheitDesTagesAsync(this IApplicationDbContext db, Guid userId, Guid dogId, DateOnly date, CancellationToken ct)
    {
        var amTag = db.TrainingSessions.Where(s => s.DogId == dogId && s.Date == date);
        amTag = await db.IstBesitzerAsync(userId, dogId, ct)
            ? amTag.Where(s => db.BesitzerIds(dogId).Contains(s.UserId))
            : amTag.Where(s => s.UserId == userId);
        return await amTag.OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(ct);
    }
}
