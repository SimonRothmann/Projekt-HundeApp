namespace Dogity.Application.Notifications;

/// <summary>Führt einen <see cref="PushAuftrag"/> aus: sendet an alle Geräte der Person.</summary>
public interface IPushDispatcher
{
    Task SendeAsync(PushAuftrag auftrag, CancellationToken ct = default);
}
