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
    public int ExpiryMinutes { get; set; } = 60 * 24;
}
