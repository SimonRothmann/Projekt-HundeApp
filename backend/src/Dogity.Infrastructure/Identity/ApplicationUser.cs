using Microsoft.AspNetCore.Identity;

namespace Dogity.Infrastructure.Identity;

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

    /// <summary>
    /// Wann der geführte Erststart weggeklickt wurde; null, solange er noch
    /// angezeigt werden soll. Am Nutzer und nicht im Browser, damit ein
    /// Wegklicken auf dem Telefon auch am Rechner gilt.
    /// </summary>
    public DateTimeOffset? OnboardingDismissedAt { get; set; }
}
