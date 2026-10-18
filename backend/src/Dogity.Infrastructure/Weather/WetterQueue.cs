using System.Threading.Channels;
using Dogity.Application.Weather;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Weather;

/// <summary>
/// In-Memory-Warteschlange für Wetteraufträge - Bauart und Begründung wie
/// <see cref="Email.MailQueue"/>. Ein Auftrag, der bei einem Neustart noch in
/// der Schlange lag, geht verloren; dem Eintrag fehlt dann nur das Wetter
/// (Anreicherung), bei einer Fährte holt es der Knopf "Wetter laden" nach. Ein
/// Nachholer beim Start lohnt nicht: Aufträge sind nach wenigen Sekunden
/// erledigt, und er fragte bei jedem Neustart auch alles erneut an, was der
/// Wetterdienst nie geliefert hat.
/// </summary>
public class WetterQueue(ILogger<WetterQueue> logger) : IWetterQueue
{
    // Begrenzt: Hängt der Wetterdienst, soll der Speicher nicht unbegrenzt wachsen.
    // Wait statt DropWrite: TryWrite meldet nur bei Wait "voll" (false); DropWrite
    // würde stillschweigend verwerfen und true liefern. TryWrite blockiert nie.
    private const int Kapazitaet = 500;

    private readonly Channel<WetterAuftrag> _kanal = Channel.CreateBounded<WetterAuftrag>(
        new BoundedChannelOptions(Kapazitaet)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ChannelReader<WetterAuftrag> Reader => _kanal.Reader;

    // Nur der Übergang "voll" wird gemeldet, sonst füllte ein längerer Ausfall das Log.
    private bool _voll;

    public bool Enqueue(WetterAuftrag auftrag)
    {
        var angenommen = _kanal.Writer.TryWrite(auftrag);
        if (!angenommen && !_voll)
            logger.LogWarning("Wetter-Warteschlange voll ({Kapazitaet}): Aufträge werden verworfen.", Kapazitaet);
        _voll = !angenommen;
        return angenommen;
    }
}
