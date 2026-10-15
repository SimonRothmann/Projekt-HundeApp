namespace Dogity.Application.Notifications;

/// <summary>Ein Gerät, an das gesendet wird - nur, was der Versand braucht.</summary>
public record PushZiel(string Endpoint, string P256dh, string Auth);

public enum PushErgebnis
{
    Gesendet,

    /// <summary>
    /// 404/410: Der Push-Dienst kennt das Gerät nicht mehr (App deinstalliert,
    /// Erlaubnis entzogen, Abo abgelaufen). Die Zeile wird gelöscht.
    /// </summary>
    AboUngueltig,

    /// <summary>Alles andere (Netz, 413/400, 5xx) - geloggt, das Abo bleibt.</summary>
    Fehler,
}

/// <summary>
/// Der eigentliche Versand an den Push-Dienst. Als Schnittstelle, damit Tests
/// nie echte Pushes absetzen und die Antwort 404/410 nachstellen können.
/// </summary>
public interface IPushSender
{
    Task<PushErgebnis> SendAsync(PushZiel ziel, string payload, CancellationToken ct = default);
}
