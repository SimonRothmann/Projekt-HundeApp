namespace Dogity.Application.Notifications;

/// <summary>
/// Ein Push an alle Geräte einer Person. <paramref name="Payload"/> ist das
/// fertige JSON (siehe <see cref="PushPayload"/>) - die Geräte der Person werden
/// erst beim Versand geladen, damit ein inzwischen abgemeldetes Gerät nicht mehr
/// bedient wird.
/// </summary>
public record PushAuftrag(Guid UserId, string Payload);

/// <summary>Wie <see cref="IMailQueue"/>: einreihen und sofort weiter.</summary>
public interface IPushQueue
{
    /// <returns>false, wenn die Warteschlange voll ist und der Auftrag verworfen wurde.</returns>
    bool Enqueue(PushAuftrag auftrag);
}
