using System.Threading.Channels;
using Dogity.Application.Notifications;

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
public class MailQueue : IMailQueue
{
    // Begrenzt: Hängt der Mailserver, soll der Speicher nicht unbegrenzt wachsen.
    // Bei den erwarteten wenigen Mails am Tag ist die Grenze nie erreichbar.
    private const int Kapazitaet = 500;

    private readonly Channel<MailNachricht> _kanal = Channel.CreateBounded<MailNachricht>(
        new BoundedChannelOptions(Kapazitaet)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        });

    public ChannelReader<MailNachricht> Reader => _kanal.Reader;

    public bool Enqueue(MailNachricht mail) => _kanal.Writer.TryWrite(mail);
}
