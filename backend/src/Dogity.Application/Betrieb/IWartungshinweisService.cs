using Dogity.Application.Common;

namespace Dogity.Application.Betrieb;

public interface IWartungshinweisService
{
    /// <summary>Der Hinweis, wenn jetzt einer anzuzeigen ist - sonst null. Ohne Anmeldung, kurz im Speicher gehalten.</summary>
    Task<Result<WartungshinweisDto?>> GetAktuellenAsync(CancellationToken ct = default);

    /// <summary>Der gespeicherte Hinweis, auch wenn er aus ist oder abgelaufen (Verwaltung, nur Admin).</summary>
    Task<Result<WartungshinweisAdminDto>> GetGespeicherteAsync(CancellationToken ct = default);

    /// <summary>Setzt den Hinweis (legt ihn beim ersten Mal an). Nur Admin.</summary>
    Task<Result<WartungshinweisAdminDto>> SetzenAsync(SetWartungshinweisRequest request, CancellationToken ct = default);

    /// <summary>Schaltet den Hinweis aus; Text und Zeiten bleiben für das nächste Mal stehen. Nur Admin.</summary>
    Task<Result> AusschaltenAsync(CancellationToken ct = default);
}
