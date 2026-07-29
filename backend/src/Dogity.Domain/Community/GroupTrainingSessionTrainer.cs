using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Eine zuständige Trainer:in an einem <see cref="GroupTrainingSession"/>.
/// Mehrere je Termin möglich (gemeinsames Planen/Vertretung); der persönliche
/// Kalender einer Trainer:in aggregiert die Termine, an denen sie zugewiesen ist.
/// </summary>
public class GroupTrainingSessionTrainer : Entity
{
    public Guid GroupTrainingSessionId { get; set; }
    public GroupTrainingSession? Session { get; set; }

    public Guid UserId { get; set; }
}
