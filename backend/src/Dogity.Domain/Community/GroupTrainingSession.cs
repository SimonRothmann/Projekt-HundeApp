using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

public enum GroupTrainingSessionStatus
{
    Planned,
    Cancelled
}

/// <summary>
/// Ein geplanter Gruppentraining-Termin (siehe docs/GROUP_TRAINING_SCHEDULE.md):
/// eine Gruppe trainiert zu einer Zeit an einem Ort mit einem konkreten Inhalt
/// (geordnete Mischung aus Bausteinen + Freitext). Verein-weit; jede:r
/// ClubTrainer plant/bearbeitet, Mitglieder der Gruppe sehen ihn read-only.
/// Termine sind eigenständig (auch aus einer Serie erzeugte) und einzeln
/// editier-/absagbar.
/// </summary>
public class GroupTrainingSession : Entity
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public Guid GroupId { get; set; }
    public Group? Group { get; set; }

    /// <summary>Trainingsstufe des Termins – steuert u.a. den Mix-Generator.</summary>
    public GroupTrainingCategory Category { get; set; } = GroupTrainingCategory.Puppy;

    public DateTimeOffset StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Besonderer Treffpunkt (z.B. Wald, Parkplatz, Biergarten); null = üblicher Platz.</summary>
    public string? Location { get; set; }
    public string? Notes { get; set; }

    public GroupTrainingSessionStatus Status { get; set; } = GroupTrainingSessionStatus.Planned;
    public Guid? CreatedByUserId { get; set; }

    /// <summary>Inhalt = geordnete Bausteine und/oder Freitext-Positionen.</summary>
    public ICollection<GroupTrainingSessionItem> Items { get; set; } = new List<GroupTrainingSessionItem>();

    /// <summary>Zuständige Trainer:innen (mehrere möglich – gemeinsames Planen/Vertretung).</summary>
    public ICollection<GroupTrainingSessionTrainer> Trainers { get; set; } = new List<GroupTrainingSessionTrainer>();
}
