namespace Dogity.Application.Planning;

/// <summary>
/// Pflegt den persistenten Wiedervorlage-/Beherrschungs-Zustand
/// (<see cref="Domain.Planning.ExerciseMastery"/>) je Hund und Katalog-Übung
/// (Leitner-/Spaced-Repetition-Modell, siehe docs/SMART_TRAINING_PLAN.md).
/// Grundlage für die spätere adaptive Wochen-Auswahl (P3).
/// </summary>
public interface IExerciseMasteryService
{
    /// <summary>
    /// Aktualisiert (oder legt an) den Zustand einer Katalog-Übung für einen
    /// Hund anhand eines geloggten Trainings. Speichert NICHT selbst - der
    /// Aufruf erfolgt im selben <c>SaveChanges</c> wie das Training.
    /// </summary>
    Task ApplyLogAsync(Guid dogId, Guid exerciseId, int rating, bool success, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Einmaliger Backfill aus der bestehenden Trainingshistorie - läuft nur,
    /// solange noch keine Mastery-Zeilen existieren (idempotent). Wird beim
    /// Anwendungsstart nach den Migrationen aufgerufen.
    /// </summary>
    Task BackfillIfEmptyAsync(CancellationToken ct = default);
}
