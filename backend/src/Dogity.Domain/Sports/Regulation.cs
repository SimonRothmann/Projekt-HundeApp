using Dogity.Domain.Common;

namespace Dogity.Domain.Sports;

/// <summary>
/// Eine Prüfungsordnung einer Sportart (z.B. "BH", "IBGH3"). Die konkreten,
/// zeitlich gültigen Inhalte stecken in <see cref="RegulationVersion"/>,
/// damit Änderungen der Prüfungsordnung nachvollziehbar bleiben.
/// </summary>
public class Regulation : Entity
{
    public Guid SportId { get; set; }
    public Sport? Sport { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Link zur offiziellen Quelle (z.B. VDH/Verband) zur Versionsprüfung
    /// (siehe DATABASE.md "Prüfungsordnung: automatische Prüfung ob neue
    /// Version verfügbar ist"). Der Volltext wird bewusst NICHT gespiegelt,
    /// da Prüfungsordnungen urheberrechtlich geschützt sind - Dogity
    /// verweist nur darauf und pflegt eigene, daraus abgeleitete
    /// Übungs-/Bewertungsdaten.
    /// </summary>
    public string? SourceUrl { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LatestKnownVersionLabel { get; set; }

    /// <summary>
    /// Eigene Kurzbeschreibung der Prüfungs-Rahmenbedingungen (mehrzeilig):
    /// z.B. bei Fährtenprüfungen Schrittzahl, Schenkel/Winkel, Fährtenalter,
    /// Ausarbeitungszeit, Zulassungsvoraussetzungen und Bestehensgrenze.
    /// Eigene Formulierung, kein PO-Volltext (Urheberrecht, siehe SourceUrl).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Geltungsbereich als ISO-3166-1-alpha-2-Kürzel ("DE", "AT"), null =
    /// gilt überall.
    ///
    /// Die Zuordnung sitzt bewusst an der Prüfungsordnung und nicht an der
    /// Sportart: Agility ist international, die Prüfungsordnung dazu ist es
    /// nicht. Und null als "gilt überall" ist kein Platzhalter, sondern
    /// notwendig - sonst müsste jede international gültige Ordnung für jedes
    /// Land dupliziert werden (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
    /// </summary>
    public string? CountryCode { get; set; }

    public ICollection<RegulationVersion> Versions { get; set; } = new List<RegulationVersion>();
}
