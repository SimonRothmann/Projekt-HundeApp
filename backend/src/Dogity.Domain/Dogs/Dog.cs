using Dogity.Domain.Common;

namespace Dogity.Domain.Dogs;

public enum DogGender
{
    Male,
    Female
}

/// <summary>
/// Ein Hund. Besitzer werden über <see cref="DogOwner"/> verknüpft,
/// damit mehrere Benutzer (Besitzer, Trainer) denselben Hund verwalten können.
/// </summary>
public class Dog : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Breed { get; set; }
    public DateOnly? Birthday { get; set; }
    public DogGender Gender { get; set; }
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Zeitpunkt der Archivierung (z.B. wenn der Hund verstorben ist). Anders als
    /// <see cref="Entity.DeletedAt"/> ist dies KEIN Soft-Delete: der Hund bleibt
    /// mit seiner gesamten Historie erhalten und abrufbar, wird aber aus der
    /// aktiven Hundeliste ausgeblendet. Reversibel (Archivierung aufhebbar).
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    /// <summary>
    /// Ob für diesen Hund eine EIGENE Sportartenauswahl gilt (siehe
    /// <see cref="Preferences.DogSportSelection"/>) statt der des Menschen.
    ///
    /// Warum ein eigenes Kennzeichen und nicht einfach "hat Zeilen": Ohne das
    /// ließe sich am Hund nie ausdrücklich auf "alle Sportarten" stellen -
    /// keine Zeilen hieße dann immer "erbt", und man käme aus der geerbten
    /// Auswahl nicht mehr heraus. Mit dem Kennzeichen sind beide Aussagen
    /// unterscheidbar: false = folgt dem Menschen, true = eigene Auswahl
    /// (und die darf leer sein, das heißt dann "alle").
    /// </summary>
    public bool UsesOwnSports { get; set; }

    public ICollection<DogOwner> Owners { get; set; } = new List<DogOwner>();
    public ICollection<Preferences.DogSportSelection> Sports { get; set; } = new List<Preferences.DogSportSelection>();
}
