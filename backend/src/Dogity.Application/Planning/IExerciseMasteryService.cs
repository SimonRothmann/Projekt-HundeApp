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
    /// Rechnet den Zustand EINER Übung eines Hundes komplett aus der Historie
    /// neu. Nötig, sobald ein bereits geloggtes Training nachträglich geändert
    /// wird: <see cref="ApplyLogAsync"/> schreibt fort, statt zu ersetzen -
    /// ein korrigiertes Rating einfach noch einmal anzuwenden würde dasselbe
    /// Training doppelt zählen und Box und Fälligkeit verfälschen.
    /// Die manuelle Gewichtung (ManualPriority) bleibt erhalten.
    /// Speichert NICHT selbst.
    /// </summary>
    Task RecomputeAsync(Guid dogId, Guid exerciseId, CancellationToken ct = default);

    /// <summary>
    /// Einmaliger Backfill aus der bestehenden Trainingshistorie - läuft nur,
    /// solange noch keine Mastery-Zeilen existieren (idempotent). Wird beim
    /// Anwendungsstart nach den Migrationen aufgerufen.
    /// </summary>
    Task BackfillIfEmptyAsync(CancellationToken ct = default);

    /// <summary>
    /// Setzt die manuelle Gewichtung ("mehr/weniger üben") einer Katalog-Übung
    /// für einen Hund. Der Wert (−2..+2) fließt als additiver Term ins Ranking
    /// des adaptiven Generators ein (höher = eher gewählt). Legt die Mastery-
    /// Zeile bei Bedarf an (Übung noch nie trainiert) und speichert; der Wert
    /// wird auf [−2, +2] begrenzt.
    /// </summary>
    Task SetManualPriorityAsync(Guid dogId, Guid exerciseId, int value, CancellationToken ct = default);
}
