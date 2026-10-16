namespace Dogity.Application.Notifications;

/// <summary>
/// Das Token im Abmelde-Link einer Mail. Es ersetzt die Anmeldung: Wer den Link
/// aus seinem Postfach öffnet, darf genau diese eine Einstellung ("Wichtiges
/// auch per E-Mail") für genau dieses Konto ändern - mehr nicht.
///
/// Signiert und verschlüsselt, mit eigenem Zweck, damit es nirgends sonst
/// (Passwort-Reset, Anmelde-Token) gilt. Ohne Ablauf: Ein Link in einer alten
/// Mail soll weiter funktionieren.
/// </summary>
public interface IEmailAbmeldeToken
{
    /// <summary>URL-taugliches Token (base64url) für dieses Konto.</summary>
    string Erzeuge(Guid userId);

    /// <summary>
    /// Die Konto-Id hinter dem Token, oder null bei leerem, manipuliertem oder
    /// fremdem Token. Wirft nie nach außen - der Endpunkt ist anonym.
    /// </summary>
    Guid? Pruefe(string? token);
}
