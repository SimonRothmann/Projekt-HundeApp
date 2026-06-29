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

    public Guid TrainerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}
