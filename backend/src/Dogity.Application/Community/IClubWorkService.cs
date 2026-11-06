using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Arbeitseinsätze des Vereins (Paket A1, siehe docs/BERECHTIGUNGEN.md). Schreiben darf die Verwaltung
/// (<see cref="Domain.Community.ClubRollen.Arbeitseinsaetze"/>), lesen mit Namen Verwaltung und Vorstand
/// (<see cref="Domain.Community.ClubRollen.ArbeitseinsaetzeLesen"/>). Fehlt die Rolle oder gehört der Verein
/// jemand anderem, ist das Ergebnis "nicht gefunden". Jedes Mitglied sieht nur die eigenen Einsätze
/// (<see cref="GetMineAsync"/>).
/// </summary>
public interface IClubWorkService
{
    /// <summary>Alle Einsätze des Vereins, die neuesten zuerst.</summary>
    Task<Result<IReadOnlyList<ClubWorkEventListItemDto>>> GetListAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>Ein Einsatz mit den abgehakten Helfer:innen.</summary>
    Task<Result<ClubWorkEventDto>> GetAsync(Guid userId, Guid clubId, Guid eventId, CancellationToken ct = default);

    Task<Result<ClubWorkEventDto>> CreateAsync(Guid userId, Guid clubId, SaveClubWorkEventRequest request, CancellationToken ct = default);
    Task<Result<ClubWorkEventDto>> UpdateAsync(Guid userId, Guid clubId, Guid eventId, SaveClubWorkEventRequest request, CancellationToken ct = default);

    /// <summary>Löscht den Einsatz; die Teilnahmen gehen endgültig mit.</summary>
    Task<Result> DeleteAsync(Guid userId, Guid clubId, Guid eventId, CancellationToken ct = default);

    /// <summary>Die Personen, die sich abhaken lassen: freigegebene Mitglieder und Personen mit einer Rolle im Verein.</summary>
    Task<Result<IReadOnlyList<ClubWorkCandidateDto>>> GetCandidatesAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>Hakt Personen ab - erst ab dem Tag des Einsatzes. Wer schon abgehakt ist, bleibt unverändert.</summary>
    Task<Result> AddParticipantsAsync(Guid userId, Guid clubId, Guid eventId, AddClubWorkParticipantsRequest request, CancellationToken ct = default);

    /// <summary>Setzt abweichende Minuten einer Person (null = die Dauer des Einsatzes).</summary>
    Task<Result> SetMinutesAsync(Guid userId, Guid clubId, Guid eventId, Guid participationId, SetClubWorkMinutesRequest request, CancellationToken ct = default);

    /// <summary>Trägt eine Person wieder aus (endgültig).</summary>
    Task<Result> RemoveParticipantAsync(Guid userId, Guid clubId, Guid eventId, Guid participationId, CancellationToken ct = default);

    /// <summary>Die Jahresauswertung; ohne Jahr das neueste mit Einsätzen, sonst das laufende.</summary>
    Task<Result<ClubWorkSummaryDto>> GetSummaryAsync(Guid userId, Guid clubId, int? year, CancellationToken ct = default);

    /// <summary>Die eigenen Einsätze der anfragenden Person - für jedes Mitglied des Vereins.</summary>
    Task<Result<ClubMyWorkDto>> GetMineAsync(Guid userId, Guid clubId, CancellationToken ct = default);
}
