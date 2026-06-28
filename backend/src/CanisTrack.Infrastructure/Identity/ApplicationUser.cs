using Microsoft.AspNetCore.Identity;

namespace CanisTrack.Infrastructure.Identity;

/// <summary>
/// Erweitert ASP.NET Identity um die in DATABASE.md definierten Felder der
/// "users"-Tabelle (firstname, lastname, avatar_url). Authentifizierung
/// (Passwort-Hashing, Rollen) wird vollständig von ASP.NET Identity
/// übernommen - kein eigenes Domain.User-Pendant, um Duplikation zu vermeiden.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
