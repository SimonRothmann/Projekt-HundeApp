namespace Dogity.Application.Preferences;

/// <summary>
/// Die persönlichen Einstellungen, wie das Frontend sie braucht.
/// </summary>
/// <param name="Locale">Oberflächensprache, null = Vorgabe der App.</param>
/// <param name="Country">Geltungsbereich der Prüfungsordnungen, null = Vorgabe der App.</param>
/// <param name="DisabledModules">ABGEWÄHLTE Module. Leer = alles an.</param>
/// <param name="SportIds">Betriebene Sportarten. Leer = keine Einschränkung.</param>
public record UserPreferenceDto(string? Locale, string? Country, IReadOnlyList<string> DisabledModules, IReadOnlyList<Guid> SportIds);

public record UpdateModulesRequest(IReadOnlyList<string> DisabledModules);
public record UpdateSportsRequest(IReadOnlyList<Guid> SportIds);
public record UpdateLocaleRequest(string? Locale);
public record UpdateCountryRequest(string? Country);

/// <param name="UsesOwnSports">
/// false = folgt der Auswahl des Menschen, true = eigene Auswahl.
/// </param>
public record UpdateDogSportsRequest(bool UsesOwnSports, IReadOnlyList<Guid> SportIds);
