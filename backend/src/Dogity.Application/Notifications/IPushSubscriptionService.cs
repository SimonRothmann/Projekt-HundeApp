using Dogity.Application.Common;

namespace Dogity.Application.Notifications;

/// <summary>Verwaltet die Geräte einer Person, an die Push-Nachrichten gehen.</summary>
public interface IPushSubscriptionService
{
    /// <summary>
    /// Meldet ein Gerät an. Upsert über die Adresse: Gibt es sie schon, gehört
    /// sie danach der aufrufenden Person (Gerät wurde weitergegeben, oder die
    /// Schlüssel haben sich erneuert).
    /// </summary>
    /// <param name="userAgent">Roher User-Agent der Anfrage; gespeichert wird nur eine Kurzbezeichnung.</param>
    Task<Result> SubscribeAsync(Guid userId, SubscribePushRequest request, string? userAgent, CancellationToken ct = default);

    /// <summary>Meldet ein Gerät der aufrufenden Person ab. Fremde Geräte lassen sich nicht abmelden.</summary>
    Task<Result> UnsubscribeAsync(Guid userId, UnsubscribePushRequest request, CancellationToken ct = default);
}
