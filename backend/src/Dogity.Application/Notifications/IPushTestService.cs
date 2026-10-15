namespace Dogity.Application.Notifications;

/// <param name="PushEingerichtet">false: Server ohne VAPID-Schlüssel oder ohne Frontend:BaseUrl - es geht kein Push hinaus.</param>
/// <param name="Geraete">Auf wie vielen Geräten der Person Push eingeschaltet ist - an die geht die Test-Meldung.</param>
public record TestpushErgebnisDto(bool PushEingerichtet, int Geraete);

/// <summary>
/// "Test-Push an mich" im Adminbereich: eine Meldung nur für die aufrufende
/// Person, über denselben Weg wie jede echte Benachrichtigung (Glocke,
/// Warteschlange, Versand an alle ihre Geräte). Geprüft wird also genau das,
/// was im Alltag passiert - kein Sonderweg, der funktioniert, während der
/// echte es nicht tut.
/// </summary>
public interface IPushTestService
{
    Task<TestpushErgebnisDto> SendeAnMichAsync(Guid userId, CancellationToken ct = default);
}
