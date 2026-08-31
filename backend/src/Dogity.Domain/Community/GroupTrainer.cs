using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Eine weitere Trainer:in einer Trainingsgruppe neben der/dem in
/// <see cref="Group.TrainerId"/> hinterlegten Hauptverantwortlichen.
///
/// Bis hierher hatte eine Gruppe genau eine:n Trainer:in - in der Praxis
/// teilen sich aber mehrere eine Gruppe, und wer eine andere Gruppe
/// mitbetreut, brauchte dort dieselben Rechte. Genau dafür ist diese
/// Zuordnung da: Sie ist n:m, ein und dieselbe Trainer:in kann also in
/// beliebig vielen Gruppen stehen.
///
/// Wer hier steht, darf die Gruppe verwalten wie die/der Hauptverantwortliche
/// (siehe GroupService.GetManageableGroupAsync). Die Hauptverantwortliche
/// bleibt in <see cref="Group.TrainerId"/> stehen und wird nicht zusätzlich
/// hier geführt - sonst gäbe es zwei Wahrheiten für dieselbe Aussage.
/// </summary>
public class GroupTrainer : Entity
{
    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    public Guid UserId { get; set; }
}
