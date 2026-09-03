using Dogity.Domain.Common;

namespace Dogity.Domain.Preferences;

/// <summary>
/// Eine Sportart, die für DIESEN Hund gilt - unabhängig davon, was sein
/// Mensch sonst betreibt.
///
/// Gebraucht, weil ein Fährtenhund und ein Agility-Hund verschiedene Dinge
/// tun. Ohne diese Ebene bekäme man am Agility-Hund die Fährtenaufzeichnung
/// angeboten, nur weil man mit dem anderen Hund Fährte läuft.
///
/// Ob diese Zeilen überhaupt gelten, entscheidet
/// <see cref="Dogs.Dog.UsesOwnSports"/> - siehe dort, warum das ein eigenes
/// Kennzeichen braucht und nicht am Fehlen von Zeilen hängen darf.
/// </summary>
public class DogSportSelection : Entity
{
    public Guid DogId { get; set; }
    public Dogs.Dog? Dog { get; set; }

    public Guid SportId { get; set; }
}
