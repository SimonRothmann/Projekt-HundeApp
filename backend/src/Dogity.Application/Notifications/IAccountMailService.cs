namespace Dogity.Application.Notifications;

/// <param name="Link">Der vollständige Link zum Zurücksetzen (enthält das Token - nie loggen).</param>
public record PasswortResetAnfrage(Guid UserId, string Email, string Vorname, string Nachname, string Link);

/// <summary>
/// Mails, die zum Konto selbst gehören (nicht zu einer Benachrichtigung):
/// Willkommen und Passwort zurücksetzen. Unabhängig vom Schalter
/// "Wichtiges auch per E-Mail" - ohne die zweite käme niemand mehr ins Konto,
/// der das Passwort vergessen hat.
/// </summary>
public interface IAccountMailService
{
    /// <summary>
    /// Willkommensmail nach der Registrierung. Geht nur raus, wenn der
    /// Mailversand eingerichtet ist - sonst gäbe es nichts zu protokollieren,
    /// was jemand lesen wollte. Ohne Namen, weil die Adresse unbestätigt ist.
    /// </summary>
    void SendeWillkommen(string email);

    /// <summary>
    /// Mail mit dem Link zum Zurücksetzen. Ist KEIN Mailversand eingerichtet,
    /// bekommen stattdessen alle Admins eine Benachrichtigung in der App und
    /// setzen das Passwort in der Nutzerverwaltung selbst zurück - sonst gäbe es
    /// für die Person gar keinen Weg zurück ins Konto. Mit Mailversand wäre diese
    /// Meldung nur Lärm (und verriete den Admins jede Anfrage).
    /// </summary>
    /// <param name="adminRolle">Name der Admin-Rolle - die Application kennt die Identity-Rollen nicht.</param>
    Task SendePasswortZuruecksetzenAsync(PasswortResetAnfrage anfrage, string adminRolle, CancellationToken ct = default);
}
