namespace CanisTrack.Application.Abstractions;

/// <summary>
/// Erzeugt signierte JWTs für authentifizierte Benutzer. Implementierung
/// lebt in Infrastructure (benötigt Signierschlüssel-Konfiguration).
/// </summary>
public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string email, IEnumerable<string> roles);
}
