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
    /// Englische Bezeichnung der FCI-Ordnung (null = keine, siehe
    /// <see cref="Sport.NameEn"/>). Nationale Ordnungen (VDH) bekommen keine:
    /// Es gibt sie nur auf Deutsch.
    /// </summary>
    public string? NameEn { get; set; }

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
    /// Englische Fassung von <see cref="Description"/> (null = keine, die App
    /// zeigt dann den deutschen Text). Nur für die FCI-Ordnungen gepflegt
    /// (Paket E4), eigene Formulierung wie das Deutsche - keine Kopie der
    /// offiziellen Ordnung.
    /// </summary>
    public string? DescriptionEn { get; set; }

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

    /// <summary>
    /// Code des Sachkunde-Fragenkatalogs (<c>QuizCatalog.Code</c>, die
    /// Erwachsenenfassung), dessen Theorie zum Ablauf dieser Prüfung gehört -
    /// null = die Prüfung hat keinen Theorieteil. Er speist die Theoriezeile in
    /// der Prüfungsreife (siehe docs/SMART_TRAINING_PLAN.md). Die
    /// Jugendfassung desselben Katalogs (gleicher Code bis auf die Endung) zählt
    /// mit.
    ///
    /// Gesetzt nur, wo der Ablauf der Prüfung den Sachkundenachweis selbst
    /// enthält (die BH/VT nach FCI-Ordnung, deren Katalog der des swhv ist).
    /// NICHT gesetzt bei Ordnungen, die eine bestandene BH/VT bloß als Zulassung
    /// verlangen, und nicht bei der österreichischen und der Schweizer BH: Dort
    /// richtet sich der Nachweis nach Landesrecht bzw. eigener Ordnung, und
    /// unser Katalog ist der des swhv.
    /// </summary>
    public string? TheoryCatalogCode { get; set; }

    /// <summary>
    /// Stabiler Schlüssel einer vom Seeder gepflegten (globalen) Ordnung: der
    /// Name, unter dem der SportCatalogSeeder sie anlegt. null bei Ordnungen,
    /// die der Seeder nicht kennt (Vereinsordnungen, von Hand angelegte).
    ///
    /// Der Seeder findet die Ordnung darüber und nicht über <see cref="Name"/>:
    /// Wer im Katalog-Editor umbenennt, erzeugt sonst beim nächsten Start ein
    /// Duplikat, und die Ziele hängen an der alten Zeile. Auch der
    /// Ausbildungsweg führt seine Folgestufen über diesen Schlüssel.
    /// </summary>
    public string? SeedKey { get; set; }

    /// <summary>
    /// Gesetzt, sobald ein Admin Name, Beschreibung oder die englischen Texte
    /// im Katalog-Editor tatsächlich geändert hat. Der Seeder lässt diese
    /// Felder dann in Ruhe - wie bei <c>QuizQuestion.EditedAt</c>: Wer von
    /// Hand speichert, behält das Sagen. Zurücknehmen setzt es auf null, dann
    /// kommt beim nächsten Start die Katalogfassung zurück.
    /// </summary>
    public DateTimeOffset? EditedAt { get; set; }

    public ICollection<RegulationVersion> Versions { get; set; } = new List<RegulationVersion>();
}
