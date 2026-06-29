using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Ein Trainer betreut einen Hund eines Gruppenmitglieds
/// (siehe DATABASE.md "trainer_assignments", Beispiel "Trainer Anna
/// betreut Max + Hund Bello"). Diese Zuordnung gewährt dem Trainer
/// Zugriff auf Training/Ziele des betreuten Hundes.
/// </summary>
public class TrainerAssignment : Entity
{
    public Guid TrainerId { get; set; }
    public Guid MemberId { get; set; }
    public Guid DogId { get; set; }
    public DateOnly StartDate { get; set; }
}
