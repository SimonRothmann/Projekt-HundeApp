namespace Dogity.Application.Preferences;

/// <summary>
/// Die persönlichen Einstellungen, wie das Frontend sie braucht.
/// </summary>
/// <param name="Locale">Oberflächensprache, null = Vorgabe der App.</param>
/// <param name="Country">Geltungsbereich der Prüfungsordnungen, null = Vorgabe der App.</param>
/// <param name="DisabledModules">ABGEWÄHLTE Module. Leer = alles an.</param>
/// <param name="SportIds">Betriebene Sportarten. Leer = keine Einschränkung.</param>
/// <param name="FontScale">Schriftgröße der Oberfläche, null = Vorgabe ("normal").</param>
/// <param name="EmailNotifications">Ob wichtige Benachrichtigungen auch per E-Mail kommen. Vorgabe: ja.</param>
/// <param name="ReminderSessions">Erinnerung am Vortag an Gruppentermine. Vorgabe: ja.</param>
/// <param name="ReminderTracks">Erinnerung an noch nicht abgelaufene Fährten. Vorgabe: ja.</param>
/// <param name="ReminderTrainerSummary">Zusammenfassung offener Rückmeldungen (Trainer:innen). Vorgabe: ja.</param>
/// <param name="TrainerSummaryDays">Wochentage der Zusammenfassung als Bitmaske (Mo = 1 ... So = 64). Vorgabe: Sonntag.</param>
/// <param name="TrainerSummaryHour">Uhrzeit der Zusammenfassung, volle Stunde 8 bis 21 (Vereinszeit). Vorgabe: 18.</param>
/// <param name="ReminderHealth">Erinnerung an ablaufende Impfungen, fällige Entwurmung und Tierarzttermine. Vorgabe: ja.</param>
public record UserPreferenceDto(
    string? Locale,
    string? Country,
    IReadOnlyList<string> DisabledModules,
    IReadOnlyList<Guid> SportIds,
    string? FontScale,
    bool EmailNotifications = true,
    bool ReminderSessions = true,
    bool ReminderTracks = true,
    bool ReminderTrainerSummary = true,
    int TrainerSummaryDays = 64,
    int TrainerSummaryHour = 18,
    bool ReminderHealth = true);

public record UpdateModulesRequest(IReadOnlyList<string> DisabledModules);
public record UpdateSportsRequest(IReadOnlyList<Guid> SportIds);
public record UpdateLocaleRequest(string? Locale);
public record UpdateCountryRequest(string? Country);
public record UpdateFontScaleRequest(string? FontScale);
public record UpdateEmailNotificationsRequest(bool Enabled);

/// <summary>
/// Alle Einstellungen der Erinnerungen auf einmal - sie gehören zusammen und werden
/// zusammen angezeigt. <paramref name="TrainerSummaryDays"/> 1 bis 127, <paramref name="TrainerSummaryHour"/> 8 bis 21.
/// <paramref name="Health"/> fehlt bei Geräten, die noch eine ältere Fassung der App zeigen: null lässt den Schalter, wie er ist.
/// </summary>
public record UpdateRemindersRequest(
    bool Sessions, bool Tracks, bool TrainerSummary, int TrainerSummaryDays, int TrainerSummaryHour, bool? Health = null);

/// <summary>
/// Die wählbaren Schriftgrößen. Steht hier und nicht im Service, weil auch der
/// Test dagegen prüft - und weil eine vierte Stufe eine Entscheidung ist und
/// kein Tippfehler.
/// </summary>
public static class FontScales
{
    public const string Normal = "normal";
    public const string Gross = "gross";
    public const string SehrGross = "sehr-gross";

    public static readonly IReadOnlySet<string> Alle = new HashSet<string> { Normal, Gross, SehrGross };
}

/// <param name="UsesOwnSports">
/// false = folgt der Auswahl des Menschen, true = eigene Auswahl.
/// </param>
public record UpdateDogSportsRequest(bool UsesOwnSports, IReadOnlyList<Guid> SportIds);
