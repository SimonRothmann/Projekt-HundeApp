using Dogity.Application.Common;

namespace Dogity.Application.Notifications;

/// <param name="Token">Das Token aus dem Abmelde-Link.</param>
/// <param name="Aktiv">false = abmelden, true = wieder anmelden.</param>
public record EmailAbmeldungRequest(string? Token, bool Aktiv);

/// <summary>
/// Abmelden von den Mails ohne Anmeldung - über den Link in der Mail. Der
/// einzige Weg, auf dem eine anonyme Anfrage Einstellungen eines Kontos ändert;
/// deshalb gilt hier nur das Token und sonst nichts.
/// </summary>
public interface IEmailAbmeldungService
{
    /// <summary>
    /// Setzt "Wichtiges auch per E-Mail" für das Konto hinter dem Token.
    /// Idempotent. Ein ungültiges Token und ein gelöschtes Konto bekommen
    /// dieselbe Meldung - sie verrät nicht, ob es das Konto gibt.
    /// </summary>
    Task<Result> SetzeAsync(string? token, bool aktiv, CancellationToken ct = default);
}
