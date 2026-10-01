using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Das öffentliche Anmeldeformular einer Gruppe - ohne Anmeldung erreichbar,
/// für Menschen ohne Dogity-Konto. Der Zugang ist allein der geheime Code im
/// Link bzw. QR-Code; was der Server ohne Anmeldung herausgibt, ist deshalb
/// auf Vereins- und Gruppennamen beschränkt.
/// </summary>
public interface IGroupRegistrationFormService
{
    /// <summary>
    /// Vereins- und Gruppenname zu einem Code. Unbekannter, abgeschalteter Code,
    /// unmögliche Form und gelöschte Gruppe antworten gleich.
    /// </summary>
    Task<Result<GroupRegistrationFormDto>> GetFormAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Legt die Anmeldung an (Herkunft Formular). Antwortet in allen
    /// "nichts zu tun"-Fällen genauso erfolgreich wie bei einer neuen Anmeldung:
    /// gefülltes Köder-Feld (Roboter) und doppelte Anmeldung. Wer es versucht,
    /// soll daran nicht ablesen können, ob er etwas bewirkt hat.
    /// </summary>
    Task<Result> SubmitAsync(string code, PublicRegistrationRequest request, CancellationToken ct = default);
}
