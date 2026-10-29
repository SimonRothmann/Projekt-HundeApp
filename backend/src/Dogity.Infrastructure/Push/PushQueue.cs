using System.Threading.Channels;
using Dogity.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Push;

/// <summary>
/// In-Memory-Warteschlange für Push-Aufträge - Bauart und Begründung wie
/// <see cref="Email.MailQueue"/>: Geht bei einem Neustart etwas verloren, ist die
/// Meldung trotzdem in der Glocke.
/// </summary>
public class PushQueue(ILogger<PushQueue> logger) : IPushQueue
{
    // Wait statt DropWrite: TryWrite meldet nur bei Wait "voll" (false); DropWrite
    // würde stillschweigend verwerfen und true liefern. TryWrite blockiert nie.
    private const int Kapazitaet = 1000;

    private readonly Channel<PushAuftrag> _kanal = Channel.CreateBounded<PushAuftrag>(
        new BoundedChannelOptions(Kapazitaet)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ChannelReader<PushAuftrag> Reader => _kanal.Reader;

    // Nur der Übergang "voll" wird gemeldet, sonst füllte ein längerer Ausfall das Log.
    private bool _voll;

    public bool Enqueue(PushAuftrag auftrag)
    {
        var angenommen = _kanal.Writer.TryWrite(auftrag);
        if (!angenommen && !_voll)
            logger.LogWarning("Push-Warteschlange voll ({Kapazitaet}): Aufträge werden verworfen.", Kapazitaet);
        _voll = !angenommen;
        return angenommen;
    }
}
