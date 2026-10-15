namespace Dogity.Application.Notifications;

/// <summary>
/// Die Schlüssel eines Geräts, so wie der Browser sie in
/// <c>PushSubscription.toJSON()</c> liefert (base64url).
/// </summary>
public record PushKeysDto(string P256dh, string Auth);

/// <summary>Spiegel von <c>PushSubscription.toJSON()</c> - das Frontend schickt es unverändert.</summary>
public record SubscribePushRequest(string Endpoint, PushKeysDto Keys);

public record UnsubscribePushRequest(string Endpoint);

/// <summary>Antwort von <c>GET /api/system/features</c>: nur, was die Oberfläche zum Anzeigen braucht - keine Geheimnisse.</summary>
/// <param name="Email">Ob der Server E-Mails versendet (Text der Seite "Passwort vergessen").</param>
/// <param name="PushPublicKey">Öffentlicher VAPID-Schlüssel; null = Push nicht eingerichtet.</param>
public record SystemFeaturesDto(bool Email, string? PushPublicKey);
