using Dogity.Domain.Common;

namespace Dogity.Domain.Dogs;

/// <summary>
/// Wann ein Hund meistens trainiert - die Vorliebe, die der Merkzettel vor dem Muster aus dem
/// Tagebuch nutzt (Dogity.Application.Training.Trainingsrhythmus). Je Hund eine Zeile, für alle
/// Besitzer:innen gleich: Der Hund trainiert dann, nicht die Person.
/// </summary>
public class DogTrainingRhythm : Entity
{
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    /// <summary>Wochentage als Bitmaske (Mo = 1 ... So = 64), wie die Tage der Zusammenfassung für Trainer:innen.</summary>
    public int Days { get; set; }

    /// <summary>Volle Stunde (Vereinszeit); leer, wenn die Uhrzeit wechselt.</summary>
    public int? Hour { get; set; }
}
