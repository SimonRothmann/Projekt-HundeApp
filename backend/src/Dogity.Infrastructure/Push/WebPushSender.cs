using System.Net;
using Dogity.Application.Notifications;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dogity.Infrastructure.Push;

/// <summary>
/// Sendet eine Push-Nachricht an den Push-Dienst des Geräteherstellers
/// (Lib.Net.Http.WebPush: VAPID-Anmeldung, Verschlüsselung nach RFC 8291).
/// Ohne vollständige Konfiguration tut es nichts.
/// </summary>
public class WebPushSender : IPushSender
{
    /// <summary>
    /// Wie lange der Push-Dienst eine Nachricht aufhebt, wenn das Gerät gerade
    /// nicht erreichbar ist. Einen Tag: Danach ist die Meldung ohnehin
    /// überholt, und die Glocke in der App hat sie weiter.
    /// </summary>
    internal const int TtlSekunden = 24 * 60 * 60;

    private readonly PushServiceClient? _client;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(HttpClient httpClient, IOptions<PushSettings> options, ILogger<WebPushSender> logger)
    {
        _logger = logger;
        var settings = options.Value;
        if (!settings.Aktiv) return;

        _client = new PushServiceClient(httpClient)
        {
            DefaultAuthentication = new VapidAuthentication(settings.VapidPublicKey, settings.VapidPrivateKey)
            {
                Subject = settings.Subject,
            },
            // Eine Wartezeit des Push-Dienstes (429 mit Retry-After) soll nicht den
            // ganzen Versand anhalten - die Glocke hat die Meldung ohnehin.
            AutoRetryAfter = false,
        };
    }

    public async Task<PushErgebnis> SendAsync(PushZiel ziel, string payload, CancellationToken ct = default)
    {
        if (_client is null) return PushErgebnis.Fehler;

        var abo = new Lib.Net.Http.WebPush.PushSubscription { Endpoint = ziel.Endpoint };
        abo.SetKey(PushEncryptionKeyName.P256DH, ziel.P256dh);
        abo.SetKey(PushEncryptionKeyName.Auth, ziel.Auth);

        var nachricht = new PushMessage(payload)
        {
            TimeToLive = TtlSekunden,
            Urgency = PushMessageUrgency.Normal,
        };

        try
        {
            await _client.RequestPushMessageDeliveryAsync(abo, nachricht, ct);
            return PushErgebnis.Gesendet;
        }
        catch (PushServiceClientException ex)
        {
            // 404/410: Das Gerät ist beim Push-Dienst nicht mehr bekannt.
            if (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                return PushErgebnis.AboUngueltig;

            // 413 (zu groß) und 400 (abgelehnt) deuten auf einen Fehler bei uns
            // hin, nicht auf ein totes Gerät - sichtbar machen, Abo behalten.
            // Ohne Adresse und ohne Inhalt.
            _logger.LogWarning("Push vom Push-Dienst abgelehnt (HTTP {Status}).", (int)ex.StatusCode);
            return PushErgebnis.Fehler;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Push-Dienst nicht erreichbar.");
            return PushErgebnis.Fehler;
        }
    }
}
