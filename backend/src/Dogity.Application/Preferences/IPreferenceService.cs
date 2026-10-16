using Dogity.Application.Common;

namespace Dogity.Application.Preferences;

public interface IPreferenceService
{
    Task<Result<UserPreferenceDto>> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result> UpdateModulesAsync(Guid userId, UpdateModulesRequest request, CancellationToken ct = default);
    Task<Result> UpdateSportsAsync(Guid userId, UpdateSportsRequest request, CancellationToken ct = default);
    Task<Result> UpdateLocaleAsync(Guid userId, UpdateLocaleRequest request, CancellationToken ct = default);

    /// <summary>
    /// Merkt sich die Sprache, in der jemand das Konto anlegt - sie gilt für Mails und
    /// Push, bis die Person sie im Profil ändert. Nimmt nur Sprachen an, die der Server
    /// schreiben kann (<see cref="Sprachen"/>); alles andere ist kein Fehler, es wird
    /// nur nichts gespeichert (keine Zeile angelegt). Gibt die gespeicherte Sprache
    /// zurück, sonst null.
    /// </summary>
    Task<string?> SetRegistrationLocaleAsync(Guid userId, string? locale, CancellationToken ct = default);

    /// <summary>
    /// Setzt den Geltungsbereich der Prüfungsordnungen. Getrennt von der
    /// Sprache - siehe <see cref="Dogity.Domain.Preferences.UserPreference.Country"/>.
    /// </summary>
    Task<Result> UpdateCountryAsync(Guid userId, UpdateCountryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Setzt die Schriftgröße der Oberfläche. Unbekannte Stufen werden
    /// abgelehnt - siehe <see cref="Dogity.Domain.Preferences.UserPreference.FontScale"/>.
    /// </summary>
    Task<Result> UpdateFontScaleAsync(Guid userId, UpdateFontScaleRequest request, CancellationToken ct = default);

    /// <summary>
    /// Schaltet "Wichtiges auch per E-Mail" ein oder aus - siehe
    /// <see cref="Dogity.Domain.Preferences.UserPreference.EmailNotifications"/>.
    /// </summary>
    Task<Result> UpdateEmailNotificationsAsync(Guid userId, UpdateEmailNotificationsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Die Sportarten, die für diesen Hund tatsächlich gelten - Auswahl des
    /// Hundes, sonst die des Menschen, sonst alle.
    /// </summary>
    Task<Result<IReadOnlyList<Guid>>> GetEffectiveDogSportsAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    Task<Result> UpdateDogSportsAsync(Guid userId, Guid dogId, UpdateDogSportsRequest request, CancellationToken ct = default);
}
