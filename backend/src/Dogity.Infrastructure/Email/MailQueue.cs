using System.Threading.Channels;
using Dogity.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Dogity.Infrastructure.Email;

/// <summary>
/// In-Memory-Warteschlange für ausgehende Mails (System.Threading.Channels).
/// Der Hintergrunddienst (Dogity.Api/Hosting/MailVersandBackgroundService)
/// liest sie leer.
///
/// Bewusst nicht dauerhaft: Eine Mail, die bei einem Neustart noch in der
/// Schlange lag, geht verloren. Das ist vertretbar - die Glocke in der App ist
/// die verlässliche Quelle, und beim Passwort-Reset fordert die Person den Link
/// einfach neu an. Eine Tabelle mit Wiederholungen wäre dafür mehr Maschinerie
/// als der Nutzen hergibt.
/// </summary>
public class MailQueue(ILogger<MailQueue> logger) : IMailQueue
{
    // Begrenzt: Hängt der Mailserver, soll der Speicher nicht unbegrenzt wachsen.
    // Bei den erwarteten wenigen Mails am Tag ist die Grenze nie erreichbar.
    // Wait statt DropWrite: TryWrite meldet nur bei Wait "voll" (false); DropWrite
    // würde stillschweigend verwerfen und true liefern. TryWrite blockiert nie.
    private const int Kapazitaet = 500;

    private readonly Channel<MailNachricht> _kanal = Channel.CreateBounded<MailNachricht>(
        new BoundedChannelOptions(Kapazitaet)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    public ChannelReader<MailNachricht> Reader => _kanal.Reader;

    // Nur der Übergang "voll" wird gemeldet, sonst füllte ein längerer Ausfall das Log.
    private bool _voll;

    public bool Enqueue(MailNachricht mail)
    {
        var angenommen = _kanal.Writer.TryWrite(mail);
        if (!angenommen && !_voll)
            logger.LogWarning("Mail-Warteschlange voll ({Kapazitaet}): Mails werden verworfen.", Kapazitaet);
        _voll = !angenommen;
        return angenommen;
    }
}
