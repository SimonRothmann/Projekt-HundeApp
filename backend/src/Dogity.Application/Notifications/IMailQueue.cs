namespace Dogity.Application.Notifications;

/// <summary>
/// Eine zu versendende Mail - fertig gerendert, ohne Bezug zur Vorlage.
/// </summary>
/// <param name="AbmeldeSeite">
/// Die Abmelde-Seite der App, die schon im Text steht - hier nur mitgeführt,
/// damit sich prüfen lässt, welche Mail einen Abmelde-Link trägt. Trägt ein Token.
/// </param>
public record MailNachricht(string An, string Betreff, string Text, string? Html, string? AbmeldeSeite = null);

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
