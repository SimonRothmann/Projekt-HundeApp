using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Preferences;

namespace Dogity.Application.Notifications;

/// <inheritdoc />
public class EmailAbmeldungService(
    IEmailAbmeldeToken tokens,
    IUserLookupService userLookup,
    IPreferenceService preferences) : IEmailAbmeldungService
{
    public const string Ungueltig = "Der Link ist ungültig oder abgelaufen.";

    public async Task<Result> SetzeAsync(string? token, bool aktiv, CancellationToken ct = default)
    {
        var userId = tokens.Pruefe(token);
        if (userId is null) return Result.Failure(Ungueltig);

        // Das Konto muss es noch geben: Sonst entstünde über LadenOderAnlegen eine
        // Einstellungszeile für ein Konto, das gelöscht ist.
        var konten = await userLookup.FindByIdsAsync([userId.Value], ct);
        if (!konten.ContainsKey(userId.Value)) return Result.Failure(Ungueltig);

        // Der vorhandene Weg - derselbe Schalter wie unter Profil → Funktionen.
        return await preferences.UpdateEmailNotificationsAsync(userId.Value, new UpdateEmailNotificationsRequest(aktiv), ct);
    }
}
