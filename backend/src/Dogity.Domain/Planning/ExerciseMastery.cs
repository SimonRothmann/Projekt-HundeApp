using Dogity.Domain.Common;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;

namespace Dogity.Domain.Planning;

/// <summary>
/// Persistenter Wiedervorlage-/Beherrschungs-Zustand einer Katalog-Übung für
/// einen Hund (Leitner-/Spaced-Repetition-Modell, siehe
/// docs/SMART_TRAINING_PLAN.md). Wird nach jedem geloggten Training
/// aktualisiert und vom adaptiven Generator zur Wochen-Auswahl genutzt.
/// Nur für Katalog-Übungen (<see cref="ExerciseId"/>) - Freitext-Übungen
/// bekommen keinen Wiedervorlage-Zustand.
/// </summary>
public class ExerciseMastery : Entity
{
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    public Guid ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    /// <summary>Leitner-Box 1..5 (höher = besser beherrscht, längeres Intervall).</summary>
    public int Box { get; set; } = 1;

    public DateTimeOffset? LastTrainedAt { get; set; }

    /// <summary>Nächste Fälligkeit = <see cref="LastTrainedAt"/> + Intervall[Box].</summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>Gewichteter Schnitt der jüngsten Bewertungen (1-5); 0, wenn nie trainiert.</summary>
    public double RecentAvgRating { get; set; }

    public int SessionCount { get; set; }

    /// <summary>
    /// Manueller Score-Einfluss durch Nutzer/Trainer (z.B. -2..+2): gezielt
    /// "diese Übung mehr/weniger üben", unabhängig von der Historie. Default 0.
    /// </summary>
    public int ManualPriority { get; set; }
}
