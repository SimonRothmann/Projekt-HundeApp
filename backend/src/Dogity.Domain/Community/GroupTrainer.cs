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

    public GroupTrainerStatus Status { get; set; } = GroupTrainerStatus.Active;

    /// <summary>Die Adresse, die beim Einladen eingegeben wurde - bis zur Zusage das Einzige, was die Gruppe sieht.</summary>
    public string? InvitedEmail { get; set; }
}

public enum GroupTrainerStatus
{
    Active,

    /// <summary>
    /// Eingeladen, noch nicht angenommen. Gilt nirgends - keine
    /// Verwaltungsrechte, keine Trainer-Rolle, kein Name in der Gruppe (der
    /// globale Filter in ApplicationDbContext blendet die Zeile aus).
    ///
    /// Bis 2026-09-28 wurde man per E-Mail-Adresse sofort Trainer:in einer
    /// fremden Gruppe. Das verriet der Gruppe den Namen hinter jeder Adresse -
    /// auch dann noch, als Mitbesitz und Gruppenaufnahme längst eine Zusage
    /// verlangten -, und über die eingetragene Trainer:in ließ sich deren
    /// Betreuung eines Hundes aus einer anderen Gruppe heraus beenden.
    /// </summary>
    Invited
}
