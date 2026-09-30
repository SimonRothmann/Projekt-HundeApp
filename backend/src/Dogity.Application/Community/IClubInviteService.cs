using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Einladungslink (und QR-Code) eines Vereins: Der Verein holt neue Leute
/// direkt zur Beitrittsanfrage, statt dass sie ihn erst in der Liste suchen.
/// Die Freigabe bleibt wie immer bei den Trainer:innen - der Link schaltet
/// nichts frei, er spart nur den Weg.
/// </summary>
public interface IClubInviteService
{
    /// <summary>Der aktuelle Code oder null (kein Link / abgeschaltet). Nur für Trainer:innen des Vereins.</summary>
    Task<Result<ClubInviteLinkDto?>> GetAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>Erzeugt einen neuen Code. Ein vorhandener wird damit ersetzt und ungültig.</summary>
    Task<Result<ClubInviteLinkDto>> RegenerateAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>Schaltet den Link ab. Idempotent.</summary>
    Task<Result> DisableAsync(Guid callerId, Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// Öffentlich, ohne Anmeldung: der Vereinsname zu einem Code. Unbekannter,
    /// abgeschalteter Code, unmögliche Form und gelöschter Verein antworten
    /// gleich - man soll Codes nicht nach ihrem Zustand unterscheiden können.
    /// </summary>
    Task<Result<ClubInvitePreviewDto>> GetPreviewAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Beitritt über den Link anfragen. Wie <see cref="IClubService.RequestJoinAsync"/>,
    /// nur mit Herkunft "Einladungslink" - und idempotent: Wer schon Mitglied
    /// ist oder schon angefragt hat, bekommt den aktuellen Stand zurück statt
    /// eines Fehlers (Link zweimal geöffnet, Anmeldung im zweiten Tab).
    /// </summary>
    Task<Result<ClubMembershipDto>> JoinAsync(Guid userId, string code, CancellationToken ct = default);
}
