using Dogity.Domain.Common;

namespace Dogity.Domain.Dogs;

public enum DogHealthKind
{
    Vaccination,
    Deworming,
    ParasiteProtection,

    /// <summary>Ein Tierarzttermin - der einzige Eintrag mit Uhrzeit und ohne Fälligkeit.</summary>
    VetAppointment,
    Other
}

/// <summary>
/// Ein Eintrag zur Gesundheit eines Hundes: eine Impfung, eine Entwurmung, ein
/// Parasitenschutz, ein Tierarzttermin oder etwas Sonstiges. Eine Entität für alle
/// Arten, weil sie dieselben Felder haben und dieselbe Oberfläche teilen.
///
/// Es sind Daten über das Tier, keine Gesundheitsdaten der Person (Art. 9 DSGVO).
/// Trotzdem gehören sie der Familie: Sehen und ändern dürfen sie nur Besitzer:innen
/// und Mitbesitzer:innen, betreuende Trainer:innen nicht (siehe docs/GESUNDHEIT.md).
///
/// Dogity rechnet nichts aus: <see cref="DueDate"/> trägt die Person selbst ein, so wie
/// der Tierarzt es im Impfpass vermerkt hat. Eine erneuerte Impfung ist einfach ein
/// neuer Eintrag - der ältere bleibt im Verlauf, "aktuell" ist je Art und Titel der
/// jüngste (Dogity.Application.Dogs.Gesundheitsstand.Aktuelle).
/// </summary>
public class DogHealthRecord : Entity
{
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    public DogHealthKind Kind { get; set; }

    /// <summary>Was es ist, z. B. "Tollwut", "Entwurmung", "Kontrolle". Pflicht.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gegeben am bzw. bei einem Tierarzttermin: der Tag des Termins.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Uhrzeit des Termins; nur beim Tierarzttermin, dort optional.</summary>
    public TimeOnly? Time { get; set; }

    /// <summary>
    /// Gültig bis (Impfung) bzw. nächste fällig (Entwurmung, Parasitenschutz). Einschließlich:
    /// Am Tag selbst gilt der Schutz noch. Beim Tierarzttermin immer leer.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Wer den Eintrag angelegt hat. Kein Fremdschlüssel, wie bei den anderen Verweisen auf
    /// Konten; bei der Kontolöschung der Person bleibt der Eintrag bei den übrigen
    /// Besitzer:innen, der Verweis wird dann geleert.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }
}
