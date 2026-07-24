using Dogity.Domain.Common;
using Dogity.Domain.Sports;

namespace Dogity.Domain.Planning;

/// <summary>
/// Woher ein Plan-Ziel stammt: automatisch generiert oder manuell bzw. vom
/// Trainer gesetzt. Manuelle/Trainer-Einträge werden im adaptiven Generator
/// höher gewichtet und nie überschrieben (siehe docs/SMART_TRAINING_PLAN.md).
/// </summary>
public enum PlanItemSource
{
    Auto,
    Manual,
    Trainer
}

/// <summary>
/// Grund, warum eine Übung in dieser Woche geplant ist (nur informativ, für
/// Transparenz in der UI): Schwachstelle (lief schlecht), Wiederholung (sitzt,
/// aber im Wiedervorlage-Intervall fällig) oder neu eingeführt.
/// </summary>
public enum PlanItemReason
{
    Weakness,
    Repetition,
    Introduction
}

/// <summary>
/// Ein Wochenziel innerhalb eines Trainingsplans. Bei einer Pausenwoche
/// (<see cref="IsRestWeek"/>) ist <see cref="ExerciseId"/> null.
/// </summary>
public class TrainingPlanItem : Entity
{
    public Guid TrainingPlanId { get; set; }
    public TrainingPlan? TrainingPlan { get; set; }

    public int WeekNumber { get; set; }

    public Guid? ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    /// <summary>
    /// Alternative zu <see cref="ExerciseId"/>: freier Text für spontane
    /// Wochenziele, die nicht als eigene Katalog-Übung angelegt werden
    /// sollen (z.B. "Kopfarbeit ausprobieren"). Genau eines von
    /// ExerciseId/FreeTextLabel muss gesetzt sein, außer bei Pausenwochen.
    /// Analog zu <see cref="Training.TrainingExercise.FreeTextLabel"/>.
    /// </summary>
    public string? FreeTextLabel { get; set; }

    public int RepetitionsTarget { get; set; }
    public bool IsRestWeek { get; set; }

    /// <summary>
    /// Welcher Trainingstag der Woche (1..<see cref="Goal.TrainingDaysPerWeek"/>).
    /// Erlaubt es, die Wochenübungen auf mehrere Trainingstage zu verteilen.
    /// </summary>
    public int DayIndex { get; set; } = 1;

    /// <summary>
    /// Herkunft des Eintrags. Vom Generator erzeugte Einträge sind
    /// <see cref="PlanItemSource.Auto"/>; manuell/vom Trainer angelegte werden
    /// höher gewichtet und vom Generator nicht überschrieben.
    /// </summary>
    public PlanItemSource Source { get; set; } = PlanItemSource.Auto;

    /// <summary>
    /// Grund, warum die Übung diese Woche geplant ist (informativ). Null bei
    /// manuellen Einträgen bzw. Pausenwochen.
    /// </summary>
    public PlanItemReason? Reason { get; set; }

    /// <summary>
    /// Denormalisierte Schwierigkeit der Übung zum Generierungszeitpunkt -
    /// erlaubt Sortierung nach Schwierigkeit ohne Join. Null bei Freitext/Pause.
    /// </summary>
    public ExerciseDifficulty? Difficulty { get; set; }
}
