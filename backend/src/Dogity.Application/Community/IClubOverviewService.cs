using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Die Vereinsübersicht für Vorstand und Verwaltung (und den Admin): nur
/// Lesen, nur Vereinsdaten. Ein fremder Verein und fehlendes Recht antworten
/// gleich mit "nicht gefunden".
/// </summary>
public interface IClubOverviewService
{
    /// <summary>Zählwerte des Vereins - Mitglieder, Rollen, Gruppen, Termine der letzten 6 Monate (siehe <see cref="ClubOverviewDto"/>).</summary>
    Task<Result<ClubOverviewDto>> GetOverviewAsync(Guid callerId, bool isAdmin, Guid clubId, CancellationToken ct = default);

    /// <summary>Die freigegebenen Mitglieder mit Namen, Gruppen und Rollen - ohne E-Mail-Adresse, nach Nachname und Vorname sortiert.</summary>
    Task<Result<IReadOnlyList<ClubOverviewMemberDto>>> GetMembersAsync(Guid callerId, bool isAdmin, Guid clubId, CancellationToken ct = default);
}
