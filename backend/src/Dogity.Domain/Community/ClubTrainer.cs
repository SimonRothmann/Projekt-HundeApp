using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Verleiht einem Nutzer die Trainer-Berechtigung für genau einen Verein
/// (siehe USER FLOWS.md "Verein: Admin legt Verein an -> Trainingsgruppen").
/// Nur Nutzer mit einem Eintrag hier dürfen Gruppen unter diesem Verein
/// anlegen und vereinsspezifische Übungen pflegen (siehe Exercise.ClubId).
/// Die Zuweisung erfolgt ausschließlich durch einen Admin.
/// </summary>
public class ClubTrainer : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public Guid UserId { get; set; }
}
