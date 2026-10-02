using Microsoft.AspNetCore.Identity;

namespace Dogity.Infrastructure.Identity;

/// <summary>
/// Deutsche Fehlertexte für ASP.NET Identity.
///
/// Identity liefert seine Meldungen auf Englisch, und AuthController reicht
/// sie unverändert als <c>errors</c> an die App weiter - mitten in einer
/// sonst deutschen Oberfläche ("Passwords must be at least 8 characters.").
/// Nur die Sprache ändert sich: welche Fälle als Fehler gelten und was der
/// Controller daraus macht, bleibt wie es war. Insbesondere sagt
/// <see cref="DuplicateEmail"/> nichts, was die Registrierung nicht schon
/// vorher selbst ("E-Mail wird bereits verwendet.") preisgibt.
///
/// Die Passwortsätze müssen zu den Regeln in <c>AddIdentity</c> passen
/// (mindestens 8 Zeichen, Groß-, Kleinbuchstabe und Ziffer) und zur
/// Hinweiszeile unter den Passwortfeldern im Frontend.
/// </summary>
public class DeutscherIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Fehler(nameof(DefaultError), "Es ist ein unbekannter Fehler aufgetreten.");

    public override IdentityError ConcurrencyFailure() =>
        Fehler(nameof(ConcurrencyFailure), "Der Datensatz wurde zwischenzeitlich geändert. Bitte versuche es erneut.");

    public override IdentityError PasswordMismatch() =>
        Fehler(nameof(PasswordMismatch), "Aktuelles Passwort ist falsch.");

    public override IdentityError InvalidToken() =>
        Fehler(nameof(InvalidToken), "Link ist ungültig oder abgelaufen.");

    public override IdentityError InvalidUserName(string? userName) =>
        Fehler(nameof(InvalidUserName), $"Der Benutzername \"{userName}\" ist ungültig.");

    public override IdentityError InvalidEmail(string? email) =>
        Fehler(nameof(InvalidEmail), $"Die E-Mail-Adresse \"{email}\" ist ungültig.");

    public override IdentityError DuplicateUserName(string userName) =>
        Fehler(nameof(DuplicateUserName), "E-Mail wird bereits verwendet.");

    public override IdentityError DuplicateEmail(string email) =>
        Fehler(nameof(DuplicateEmail), "E-Mail wird bereits verwendet.");

    public override IdentityError PasswordTooShort(int length) =>
        Fehler(nameof(PasswordTooShort), $"Das Passwort muss mindestens {length} Zeichen lang sein.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Fehler(nameof(PasswordRequiresNonAlphanumeric), "Das Passwort muss mindestens ein Sonderzeichen enthalten.");

    public override IdentityError PasswordRequiresDigit() =>
        Fehler(nameof(PasswordRequiresDigit), "Das Passwort muss mindestens eine Ziffer (0-9) enthalten.");

    public override IdentityError PasswordRequiresLower() =>
        Fehler(nameof(PasswordRequiresLower), "Das Passwort muss mindestens einen Kleinbuchstaben (a-z) enthalten.");

    public override IdentityError PasswordRequiresUpper() =>
        Fehler(nameof(PasswordRequiresUpper), "Das Passwort muss mindestens einen Großbuchstaben (A-Z) enthalten.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Fehler(nameof(PasswordRequiresUniqueChars), $"Das Passwort muss mindestens {uniqueChars} verschiedene Zeichen enthalten.");

    private static IdentityError Fehler(string code, string beschreibung) =>
        new() { Code = code, Description = beschreibung };
}
