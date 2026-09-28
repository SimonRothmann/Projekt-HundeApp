namespace Dogity.Application.Abstractions;

/// <summary>Welche Sperre an einem Konto hängt.</summary>
public enum Sperrart
{
    Keine,

    /// <summary>Nach zu vielen falschen Passwörtern, für ein paar Minuten (siehe DependencyInjection.cs).</summary>
    Voruebergehend,

    /// <summary>Vom Admin gesperrt - bis zur Entsperrung.</summary>
    Dauerhaft,
}

/// <summary>
/// Unterscheidet die beiden Sperren, die ASP.NET Identity in dasselbe Feld
/// (LockoutEnd) schreibt.
///
/// Bis 2026-09-28 behandelte die Token-Erneuerung beide gleich: Wer die
/// E-Mail-Adresse einer Person kannte, schickte fünf falsche Passwörter -
/// das Konto war fünf Minuten gesperrt, und erneuerte die App der Person in
/// dieser Zeit ihren Token, beendete der Server ALLE ihre Sitzungen. Alle
/// fünf Minuten wiederholt, war sie dauerhaft ausgesperrt. Die Sperre nach
/// Fehlversuchen soll aber nur das Raten von Passwörtern bremsen; wer schon
/// angemeldet ist, hat sein Passwort längst bewiesen.
/// </summary>
public static class Sperren
{
    /// <summary>
    /// Die Admin-Sperre setzt das Ende auf <see cref="DateTimeOffset.MaxValue"/>
    /// (UserLookupService.LockUserAsync), die nach Fehlversuchen auf ein paar
    /// Minuten. Alles über ein Jahr hinaus ist eine Admin-Sperre.
    /// </summary>
    private static readonly TimeSpan AbHierDauerhaft = TimeSpan.FromDays(365);

    public static Sperrart Bestimme(DateTimeOffset? sperreBis, DateTimeOffset jetzt)
    {
        if (sperreBis is not { } bis || bis <= jetzt) return Sperrart.Keine;
        return bis - jetzt > AbHierDauerhaft ? Sperrart.Dauerhaft : Sperrart.Voruebergehend;
    }
}
