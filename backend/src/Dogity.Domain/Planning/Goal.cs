using Dogity.Domain.Common;
using Dogity.Domain.Sports;

namespace Dogity.Domain.Planning;

public enum GoalStatus
{
    Active,
    Achieved,
    Cancelled
}

/// <summary>
/// Ein Trainingsziel für einen Hund, z.B. "BH Prüfung am 01.05.2027"
/// (siehe DATABASE.md "Zielsystem"). SportId verweist auf die Zielsportart;
/// eine konkrete Prüfungsanmeldung (Competition-Modul) ist noch nicht
/// Teil des MVP-Funktionsumfangs.
///
/// RegulationId ist optional und verweist auf die konkrete Prüfungsordnung
/// innerhalb der Sportart (z.B. "Fährte C" statt nur "Fährte") - eine
/// Sportart kann mehrere, klar unterschiedliche Prüfungsordnungen mit
/// jeweils eigener Pflichtübungsliste haben (siehe RegulationExercise).
/// Ohne Auswahl (null) generiert <see cref="TrainingPlanGenerator"/> den
/// Plan aus allen Übungen der Sportart wie zuvor - z.B. für Sportarten ohne
/// hinterlegte Prüfungsordnung.
/// </summary>
public class Goal : Entity
{
    public Guid DogId { get; set; }
    public Guid SportId { get; set; }
    public Guid? RegulationId { get; set; }
    public Regulation? Regulation { get; set; }

    public DateOnly TargetDate { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;
    public string? Notes { get; set; }

    /// <summary>
    /// Kein festes Prüfungsziel: der TrainingPlan bleibt beim Anlegen leer
    /// (kein Auto-Fill durch <see cref="TrainingPlanGenerator"/>) und der
    /// Nutzer legt Wochenübungen manuell an. RegulationId ist dann immer null.
    /// TargetDate wird trotzdem gebraucht, um den Wochenraster (Wie viele
    /// Wochen umfasst der Plan?) für die manuellen Einträge zu spannen.
    /// </summary>
    public bool IsCustom { get; set; }

    public TrainingPlan? TrainingPlan { get; set; }
}
