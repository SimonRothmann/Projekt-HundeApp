namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Bindet an den "Jwt"-Abschnitt in appsettings.json. Secret darf niemals
/// in appsettings.json selbst stehen, sondern nur in appsettings.Development.json
/// (gitignored) oder als Umgebungsvariable/Secret auf dem Server.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Dogity";
    public string Audience { get; set; } = "Dogity";

    /// <summary>
    /// Lebensdauer des Access-Tokens (JWT). Bewusst kurz (60 min): ein JWT ist
    /// nicht widerrufbar, ein gestohlener Token ist also nur so lange nutzbar.
    /// Die Sitzung bleibt trotzdem lange bestehen, weil der Client mit dem
    /// Refresh-Token lautlos einen neuen Access-Token holt (siehe
    /// RefreshTokenService / Roadmap 6).
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Lebensdauer des Refresh-Tokens. So lange bleibt ein Nutzer eingeloggt,
    /// ohne sich neu anmelden zu müssen (jeder Refresh rotiert den Token und
    /// verlängert NICHT die Gesamtdauer - nach 60 Tagen ist erneuter Login
    /// nötig).
    /// </summary>
    public int RefreshExpiryDays { get; set; } = 60;
}
