namespace Dogity.Application.Notifications;

/// <summary>Eine zu versendende Mail - fertig gerendert, ohne Bezug zur Vorlage.</summary>
public record MailNachricht(string An, string Betreff, string Text, string? Html);

/// <summary>
/// Übergabe an den Versand im Hintergrund. Reiht nur ein und kehrt sofort
/// zurück: Der Aufrufer (ein Request) wartet nie auf den Mailserver und sieht
/// nie einen Versandfehler. Die Glocke in der App bleibt die verlässliche
/// Quelle; eine Mail ist die zusätzliche Erinnerung.
/// </summary>
public interface IMailQueue
{
    /// <returns>false, wenn die Warteschlange voll ist und die Mail verworfen wurde.</returns>
    bool Enqueue(MailNachricht mail);
}
