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

    /// <summary>
    /// Schriftgröße der Oberfläche: "normal", "gross" oder "sehr-gross".
    /// Null = noch nicht gewählt, es gilt "normal".
    ///
    /// Ein Schlüssel und keine Zahl: Ein frei wählbarer Prozentwert brächte
    /// Größen hervor, in denen niemand die Oberfläche je gesehen hat. Die drei
    /// Stufen sind geprüft - jede weitere müsste es auch werden.
    ///
    /// Gehört zum Nutzer und nicht zum Gerät, obwohl das Aussehen betroffen
    /// ist: Wer die Schrift größer braucht, braucht sie auf jedem Gerät größer.
    /// </summary>
    public string? FontScale { get; set; }

    /// <summary>
    /// Ob wichtige Benachrichtigungen (Einladungen, Entscheidungen über
    /// Beitrittsanfragen) zusätzlich per E-Mail kommen. Standard: ja.
    ///
    /// Betrifft NUR diese Mails. Die Willkommensmail und die Mail zum
    /// Zurücksetzen des Passworts gehören zum Konto und sind davon unabhängig -
    /// ohne die zweite käme niemand mehr ins Konto, der das Passwort vergessen hat.
    /// Bestehende Zeilen sind beim Einführen auf "ja" gesetzt worden: Wer nie
    /// etwas eingestellt hat, hat auch nichts abgelehnt.
    /// </summary>
    public bool EmailNotifications { get; set; } = true;

    // ---- Erinnerungen (Glocke + Push, nie per Mail; siehe Dogity.Application.Reminders) ----
    // Alle Vorgaben "an": Wer vorher nichts eingestellt hat, hat auch
    // nichts abgelehnt - bestehende Zeilen bekamen mit der Migration dieselben Werte.

    /// <summary>Erinnerung am Vortag an Gruppentermine. Standard: ja.</summary>
    public bool ReminderSessions { get; set; } = true;

    /// <summary>Erinnerung an Fährten, die noch nicht abgelaufen wurden. Standard: ja.</summary>
    public bool ReminderTracks { get; set; } = true;

    /// <summary>
    /// Erinnerung an ablaufende Impfungen, fällige Entwurmung und Tierarzttermine der eigenen
    /// Hunde. Standard: ja; wird das Modul "gesundheit" abgeschaltet, kommt sie ohnehin nicht.
    /// </summary>
    public bool ReminderHealth { get; set; } = true;

    /// <summary>Zusammenfassung offener Rückmeldungen (nur für Trainer:innen relevant). Standard: ja.</summary>
    public bool ReminderTrainerSummary { get; set; } = true;

    /// <summary>
    /// Wochentage der Zusammenfassung als Bitmaske: Montag = 1, Dienstag = 2, ...
    /// Sonntag = 64 (Bit = Abstand zum Montag). Gültig sind 1 bis 127, mindestens
    /// ein Tag. Standard: Sonntag.
    /// </summary>
    public int TrainerSummaryDays { get; set; } = 64;

    /// <summary>Uhrzeit der Zusammenfassung in Vereinszeit, volle Stunde von 8 bis 21. Standard: 18.</summary>
    public int TrainerSummaryHour { get; set; } = 18;

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
