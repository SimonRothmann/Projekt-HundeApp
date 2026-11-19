namespace Dogity.Application.Abstractions;

public record UserLookupResult(Guid UserId, string Email, string FirstName, string LastName);

public record UserDirectoryEntry(Guid UserId, string Email, string FirstName, string LastName, string[] Roles, bool IsLockedOut);

/// <summary>
/// Sucht registrierte Benutzer anhand der E-Mail-Adresse (z.B. um einen
/// Trainer eine Gruppeneinladung an einen bestehenden Benutzer aussprechen
/// zu lassen) und deckt Admin-Nutzerverwaltung ab (sperren/entsperren/
/// löschen). Implementierung lebt in Infrastructure (nutzt UserManager) -
/// Application bleibt dadurch frei von einer direkten Identity-Abhängigkeit.
/// </summary>
public interface IUserLookupService
{
    /// <summary>
    /// Findet das Konto zu einer E-Mail-Adresse - für die Person <paramref name="actorId"/>, die es sucht.
    /// Gastkonten und echte Konten sehen einander nie: Gehört das Konto zur anderen Seite, ist die Antwort
    /// dieselbe wie bei einer unbekannten Adresse (null) - so verrät sie nicht, ob es die Adresse gibt.
    /// </summary>
    Task<UserLookupResult?> FindByEmailAsync(string email, Guid actorId, CancellationToken ct = default);

    /// <summary>Ob das Konto ein Gastzugang ist (siehe <c>Gastkonto</c>). Ein unbekanntes Konto ist keiner.</summary>
    Task<bool> IsGuestAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Die Ids aller Gastkonten (auch der erfundenen Mitglieder) - die Admin-Auswertungen schließen sie aus.</summary>
    Task<IReadOnlyList<Guid>> ListGuestUserIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, UserLookupResult>> FindByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    /// <summary>Die Zahl der Konten ohne Gastzugänge.</summary>
    Task<int> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// Die Ids der Konten (ohne Gastzugänge), die seit <paramref name="since"/> angelegt wurden.
    /// Nur Ids, keine Personen: Die Admin-Kennzahlen zählen, sie zeigen niemanden.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListUserIdsCreatedSinceAsync(DateTimeOffset since, CancellationToken ct = default);

    /// <summary>
    /// Die Ids ALLER Konten, auch der Gastzugänge. Nur Ids, keine Personen - die Bereinigung der Daten
    /// gelöschter Konten braucht die Menge der Konten, die es noch gibt, und
    /// sonst nichts. Wirft bei einem Fehler, statt eine leere Liste zu liefern:
    /// Aus "keine Konten" würde dort "alles ist verwaist".
    /// </summary>
    Task<IReadOnlyList<Guid>> ListAllUserIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Eine Seite der Nutzerliste (ohne Gastzugänge), sortiert nach E-Mail. <paramref name="suche"/>
    /// filtert ohne Rücksicht auf Groß-/Kleinschreibung auf E-Mail, Vor- und
    /// Nachname (Teilstück genügt; auch "Vorname Nachname" zusammen); eine Guid
    /// findet genau dieses Konto. Leer oder null heißt: alle. <c>TotalCount</c> zählt nur die Treffer.
    /// </summary>
    Task<(IReadOnlyList<UserDirectoryEntry> Users, int TotalCount)> ListPagedAsync(int page, int pageSize, string? suche, CancellationToken ct = default);

    /// <summary>Sperrt einen Benutzer dauerhaft (kein Login mehr möglich). False, falls Benutzer nicht existiert.</summary>
    Task<bool> LockUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Hebt eine Sperrung auf. False, falls Benutzer nicht existiert.</summary>
    Task<bool> UnlockUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Löscht das Identity-Konto hart. False, falls Benutzer nicht existiert oder Löschung fehlschlägt.</summary>
    Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>UserIds aller Benutzer in der angegebenen Rolle (z.B. um alle Admins zu benachrichtigen).</summary>
    Task<IReadOnlyList<Guid>> ListUserIdsInRoleAsync(string role, CancellationToken ct = default);

    /// <summary>
    /// Setzt die Identity-Rolle TRAINER oder nimmt sie weg. Der Rollenname
    /// bleibt bewusst in Infrastructure - Application soll nicht wissen, wie
    /// die Rolle heißt, nur DASS es sie gibt.
    /// Idempotent: mehrfaches Setzen/Entfernen ist unproblematisch.
    /// </summary>
    Task SetTrainerRoleAsync(Guid userId, bool isTrainer, CancellationToken ct = default);

    /// <summary>
    /// Setzt das Passwort eines Benutzers administrativ neu (ohne den
    /// Token-basierten Self-Service-Reset). Gibt bei Fehlschlag die
    /// Identity-Fehlermeldungen zurück (z.B. Passwortrichtlinie verletzt).
    /// </summary>
    Task<(bool Success, string[] Errors)> SetPasswordAsync(Guid userId, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Ob der Nutzer den geführten Erststart weggeklickt hat.
    ///
    /// Am Nutzer und nicht im Browser gespeichert: Wer den Hinweis auf dem
    /// Telefon wegklickt, will ihn auch am Rechner nicht wiedersehen.
    /// </summary>
    Task<bool> IsOnboardingDismissedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Merkt sich, dass der Erststart weggeklickt wurde. Idempotent.</summary>
    Task DismissOnboardingAsync(Guid userId, CancellationToken ct = default);
}
