using Dogity.Domain.Common;

namespace Dogity.Domain.Preferences;

/// <summary>
/// Persönliche Einstellungen eines Nutzers: Sprache, ausgeblendete Module,
/// betriebene Sportarten.
///
/// Bewusst EINE Zeile je Nutzer statt verstreuter Flags an anderen Tabellen -
/// Sprache und Module gehören zusammen: Die Sachkunde ist ein deutsches
/// SWHV-Angebot, und wer die App auf Englisch nutzt, soll sie gar nicht erst
/// angeboten bekommen (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
/// </summary>
public class UserPreference : Entity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Oberflächensprache als BCP-47-Kürzel ("de", "en"). Null = noch nicht
    /// gewählt, es gilt die Vorgabe der App.
    ///
    /// Betrifft NUR die Oberfläche, nicht die Inhalte: Prüfungsordnungen und
    /// Sachkundefragen bleiben in ihrer Ursprungssprache - eine übersetzte
    /// Prüfungsfrage wäre für die Prüfung wertlos.
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Geltungsbereich der Prüfungsordnungen als ISO-3166-1-alpha-2-Kürzel.
    /// Null = noch nicht gewählt, es gilt die Vorgabe der App ("DE").
    ///
    /// Getrennt von <see cref="Locale"/> und nicht daraus abgeleitet, obwohl
    /// das naheläge: Wer in Deutschland trainiert und die App auf Englisch
    /// nutzt, braucht weiterhin die deutschen Ordnungen - die BH bleibt die
    /// BH. Und wer in Österreich lebt, spricht dieselbe Sprache, hat aber
    /// einen anderen Verband.
    /// </summary>
    public string? Country { get; set; }

    public ICollection<UserDisabledModule> DisabledModules { get; set; } = new List<UserDisabledModule>();
    public ICollection<UserSportSelection> Sports { get; set; } = new List<UserSportSelection>();
}

/// <summary>
/// Ein vom Nutzer ABGEWÄHLTES Modul.
///
/// Gespeichert wird die Abwahl, nicht die Auswahl - und das ist der
/// entscheidende Unterschied zur Sportartenauswahl: Nur so ist der Standard
/// automatisch an, auch für Module, die es beim Setzen der Einstellung noch
/// gar nicht gab. Eine Positivliste würde jedes künftige Modul vor allen
/// bestehenden Nutzern verstecken.
/// </summary>
public class UserDisabledModule : Entity
{
    public Guid UserPreferenceId { get; set; }
    public UserPreference? UserPreference { get; set; }

    /// <summary>
    /// Schlüssel des Moduls, z.B. "faehrte". Bewusst eine Zeichenkette und
    /// kein Enum: Ein Verband soll später eigene Module mitbringen können,
    /// ohne dass dafür Code geändert wird.
    /// </summary>
    public string ModuleKey { get; set; } = string.Empty;
}

/// <summary>
/// Eine Sportart, die der Nutzer betreibt.
///
/// Hier eine Positivliste, anders als bei den Modulen: Die Aussage ist "ich
/// mache genau das". Wer IGP und Fährte gewählt hat, will nicht, dass später
/// Obedience dazukommt, nur weil der Katalog gewachsen ist.
///
/// KEINE Zeile = keine Einschränkung, alle Sportarten werden angeboten.
/// </summary>
public class UserSportSelection : Entity
{
    public Guid UserPreferenceId { get; set; }
    public UserPreference? UserPreference { get; set; }

    public Guid SportId { get; set; }
}
