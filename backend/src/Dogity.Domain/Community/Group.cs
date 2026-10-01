using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Eine Trainingsgruppe (siehe DATABASE.md "groups", Beispiel "Dienstag
/// Gruppe, Trainer: Anna, Mitglieder: 10"). ClubId ist optional, da
/// Trainer laut ROADMAP.md bereits in Phase 2 Gruppen verwalten, bevor in
/// Phase 3 die Vereinsplattform folgt.
/// </summary>
public class Group : Entity
{
    public Guid? ClubId { get; set; }
    public Club? Club { get; set; }

    /// <summary>
    /// Hauptverantwortliche:r Trainer:in. Weitere Trainer:innen mit denselben
    /// Rechten stehen in <see cref="Trainers"/>.
    /// </summary>
    public Guid TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Geheimer Teil des Anmeldelinks (<c>/anmeldung/{code}</c>); null, solange
    /// die Gruppe kein Anmeldeformular hat oder es geschlossen ist. Wer den Code
    /// kennt, kann sich - ohne Konto - zur Gruppe anmelden (siehe
    /// <see cref="GroupRegistration"/>), mehr nicht. Gleiche Bauart wie
    /// <see cref="Club.InviteCode"/>: ein neuer Code ersetzt den alten.
    /// </summary>
    public string? RegistrationCode { get; set; }

    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();

    /// <summary>Weitere Trainer:innen neben <see cref="TrainerId"/>.</summary>
    public ICollection<GroupTrainer> Trainers { get; set; } = new List<GroupTrainer>();
}
