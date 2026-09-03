using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Verleiht einem Nutzer die Trainer-Berechtigung für genau einen Verein
/// (siehe USER FLOWS.md "Verein: Admin legt Verein an -> Trainingsgruppen").
/// Nur Nutzer mit einem Eintrag hier dürfen Gruppen unter diesem Verein
/// anlegen und vereinsspezifische Übungen pflegen (siehe Exercise.ClubId).
/// Die Zuweisung erfolgt ausschließlich durch einen Admin.
/// </summary>
/// <summary>
/// Was jemand innerhalb eines Vereins darf.
///
/// Zwei Stufen, weil eine flache Rolle für Selbstverwaltung nicht reicht:
/// Dürfte jede:r Trainer:in andere abberufen, könnte auch die Person, die
/// den Verein angelegt hat, aus ihm entfernt werden - und niemand könnte es
/// zurücknehmen.
///
/// Bewusst JETZT eingeführt, solange es zwei Vereine gibt: Nachträglich
/// hieße es, jede bestehende Zeile zu migrieren und jede Berechtigungs-
/// prüfung erneut anzufassen (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
/// </summary>
public enum ClubRole
{
    /// <summary>Trainiert, pflegt Gruppen und den vereinseigenen Katalog.</summary>
    Training,

    /// <summary>
    /// Zusätzlich: Stammdaten ändern, Trainer:innen berufen und abberufen.
    /// </summary>
    Verwaltung,
}

public class ClubTrainer : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Beim Backfill bekommen alle bestehenden Zuweisungen
    /// <see cref="ClubRole.Verwaltung"/> - niemand soll durch die Einführung
    /// Rechte verlieren, die er vorher hatte.
    /// </summary>
    public ClubRole Role { get; set; } = ClubRole.Training;
}
