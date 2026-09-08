namespace Dogity.Application.Preferences;

/// <summary>
/// Die persönlichen Einstellungen, wie das Frontend sie braucht.
/// </summary>
/// <param name="Locale">Oberflächensprache, null = Vorgabe der App.</param>
/// <param name="Country">Geltungsbereich der Prüfungsordnungen, null = Vorgabe der App.</param>
/// <param name="DisabledModules">ABGEWÄHLTE Module. Leer = alles an.</param>
/// <param name="SportIds">Betriebene Sportarten. Leer = keine Einschränkung.</param>
/// <param name="FontScale">Schriftgröße der Oberfläche, null = Vorgabe ("normal").</param>
public record UserPreferenceDto(
    string? Locale,
    string? Country,
    IReadOnlyList<string> DisabledModules,
    IReadOnlyList<Guid> SportIds,
    string? FontScale);

public record UpdateModulesRequest(IReadOnlyList<string> DisabledModules);
public record UpdateSportsRequest(IReadOnlyList<Guid> SportIds);
public record UpdateLocaleRequest(string? Locale);
public record UpdateCountryRequest(string? Country);
public record UpdateFontScaleRequest(string? FontScale);

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
