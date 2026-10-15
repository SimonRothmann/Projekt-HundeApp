using Dogity.Application.Common;

namespace Dogity.Application.Notifications;

public record TestmailRequest(string An);

/// <param name="VersandEingerichtet">false: kein SMTP konfiguriert - es wurde nichts verschickt.</param>
/// <param name="Versendet">Ob der Mailserver die Mail angenommen hat (zugestellt ist sie damit noch nicht).</param>
/// <param name="Fehler">Antwort bzw. Fehler des Mailservers, wenn er die Mail nicht angenommen hat.</param>
public record TestmailErgebnisDto(bool VersandEingerichtet, bool Versendet, string? Fehler);

/// <summary>
/// "Testmail senden" im Adminbereich: prüft den Versand, ohne ein Konto
/// anzulegen oder ein Passwort zurückzusetzen.
///
/// Anders als jede andere Mail NICHT über die Warteschlange, sondern direkt:
/// Der Admin soll sofort sehen, ob der Mailserver die Mail angenommen hat und
/// wenn nicht, mit welcher Antwort.
/// </summary>
public interface IMailTestService
{
    Task<Result<TestmailErgebnisDto>> SendeAsync(string an, CancellationToken ct = default);
}
