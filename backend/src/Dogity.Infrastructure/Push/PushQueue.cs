using System.Threading.Channels;
using Dogity.Application.Notifications;

namespace Dogity.Infrastructure.Push;

/// <summary>
/// In-Memory-Warteschlange für Push-Aufträge - Bauart und Begründung wie
/// <see cref="Email.MailQueue"/>: Geht bei einem Neustart etwas verloren, ist die
/// Meldung trotzdem in der Glocke.
/// </summary>
public class PushQueue : IPushQueue
{
    private const int Kapazitaet = 1000;

    private readonly Channel<PushAuftrag> _kanal = Channel.CreateBounded<PushAuftrag>(
        new BoundedChannelOptions(Kapazitaet)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public ChannelReader<PushAuftrag> Reader => _kanal.Reader;

    public bool Enqueue(PushAuftrag auftrag) => _kanal.Writer.TryWrite(auftrag);
}
