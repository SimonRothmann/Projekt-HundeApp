using System.Text.RegularExpressions;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Notifications;

/// <inheritdoc />
public partial class PushSubscriptionService(IApplicationDbContext db) : IPushSubscriptionService
{
    /// <summary>
    /// Mehr Geräte je Person gibt es nicht: Wer ständig neu anmeldet (Browserdaten
    /// löschen, neu installieren), hinterlässt sonst Leichen, an die jede
    /// Meldung einzeln gesendet würde. Über der Grenze fallen die ältesten weg.
    /// </summary>
    public const int MaxGeraete = 20;

    public async Task<Result> SubscribeAsync(Guid userId, SubscribePushRequest request, string? userAgent, CancellationToken ct = default)
    {
        var endpoint = request.Endpoint?.Trim();
        var p256dh = request.Keys?.P256dh?.Trim();
        var auth = request.Keys?.Auth?.Trim();

        if (!PushEndpointPruefung.IstErlaubt(endpoint))
            return Result.Failure("Diese Push-Adresse wird nicht unterstützt.");
        if (!SchluesselGueltig(p256dh) || !SchluesselGueltig(auth))
            return Result.Failure("Die Schlüssel des Geräts sind ungültig.");

        var bezeichnung = GeraeteBezeichnung.Aus(userAgent);

        // Zwei gleichzeitige Anmeldungen mit demselben neuen Endpoint (Doppeltipp,
        // Wiederholung nach Timeout) laufen beide in den Zweig "neu anlegen"; die
        // zweite scheitert am eindeutigen Index. Dann ist die Zeile da, und der
        // zweite Anlauf findet sie und aktualisiert sie, statt mit 500 zu enden.
        for (var versuch = 1; ; versuch++)
        {
            var angelegt = await UpsertAsync(userId, endpoint!, p256dh!, auth!, bezeichnung, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (versuch == 1 && angelegt is not null)
            {
                // Den gescheiterten Eintrag aus dem Änderungsprotokoll nehmen
                // (ein neuer, noch nicht gespeicherter wird dadurch verworfen).
                db.PushSubscriptions.Remove(angelegt);
            }
        }

        await UeberzaehligeEntfernenAsync(userId, endpoint!, ct);
        return Result.Success();
    }

    /// <returns>Die neu angelegte Zeile - null, wenn eine vorhandene übernommen wurde.</returns>
    private async Task<PushSubscription?> UpsertAsync(Guid userId, string endpoint, string p256dh, string auth, string? bezeichnung, CancellationToken ct)
    {
        // Auch weich Gelöschtes mitnehmen: Die Adresse ist eindeutig, eine
        // gelöschte Zeile mit derselben Adresse würde das Anlegen sonst scheitern lassen.
        var vorhanden = await db.PushSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

        if (vorhanden is null)
        {
            var neu = new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = bezeichnung,
            };
            db.PushSubscriptions.Add(neu);
            return neu;
        }

        // Übernahme durch eine andere Person oder Wiederbelebung: Das Anmeldedatum
        // (Datenexport, Reihenfolge beim Aufräumen) gehört der jetzigen Person, nicht
        // der vorherigen auf demselben Gerät. Dasselbe Gerät derselben Person, das
        // nur neue Schlüssel meldet, behält sein Datum.
        if (vorhanden.UserId != userId || vorhanden.DeletedAt is not null)
        {
            vorhanden.CreatedAt = DateTimeOffset.UtcNow;
            vorhanden.LastUsedAt = null;
        }

        vorhanden.UserId = userId;
        vorhanden.P256dh = p256dh;
        vorhanden.Auth = auth;
        vorhanden.UserAgent = bezeichnung;
        vorhanden.DeletedAt = null;
        vorhanden.UpdatedAt = DateTimeOffset.UtcNow;
        return null;
    }

    public async Task<Result> UnsubscribeAsync(Guid userId, UnsubscribePushRequest request, CancellationToken ct = default)
    {
        var endpoint = request.Endpoint?.Trim();
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > Textlaengen.PushEndpoint)
            return Result.Failure("Push-Adresse fehlt.");

        // Nur die eigenen: ein fremdes Gerät ist "nicht gefunden", nicht "verboten" -
        // dass es die Adresse gibt, geht niemanden etwas an.
        var abo = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId, ct);
        if (abo is null) return Result.NotFound("Gerät nicht gefunden.");

        // Endgültig statt DeletedAt: Die Schlüssel des Geräts gehören nicht
        // weiter aufbewahrt, wenn es sich abmeldet.
        db.PushSubscriptions.Remove(abo);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task UeberzaehligeEntfernenAsync(Guid userId, string behalten, CancellationToken ct)
    {
        var alle = await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync(ct);
        if (alle.Count <= MaxGeraete) return;

        var weg = alle
            .Where(s => s.Endpoint != behalten)
            .OrderBy(s => s.LastUsedAt ?? s.CreatedAt)
            .Take(alle.Count - MaxGeraete);
        db.PushSubscriptions.RemoveRange(weg);
        await db.SaveChangesAsync(ct);
    }

    private static bool SchluesselGueltig(string? schluessel) =>
        !string.IsNullOrEmpty(schluessel)
        && schluessel.Length <= Textlaengen.PushSchluessel
        && Base64Url().IsMatch(schluessel);

    [GeneratedRegex("^[A-Za-z0-9_-]+={0,2}$")]
    private static partial Regex Base64Url();
}
