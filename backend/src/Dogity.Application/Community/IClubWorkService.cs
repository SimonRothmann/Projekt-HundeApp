using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Arbeitseinsätze des Vereins (Paket A1, siehe docs/BERECHTIGUNGEN.md). Schreiben darf die Verwaltung
/// (<see cref="Domain.Community.ClubRollen.Arbeitseinsaetze"/>), lesen mit Namen Verwaltung und Vorstand
/// (<see cref="Domain.Community.ClubRollen.ArbeitseinsaetzeLesen"/>). Fehlt die Rolle oder gehört der Verein
/// jemand anderem, ist das Ergebnis "nicht gefunden". Jedes Mitglied sieht nur die eigenen Einsätze
/// (<see cref="GetMineAsync"/>). Seit Paket A2 lassen sich auch Helfer:innen ohne Dogity-Konto abhaken
/// (<see cref="Domain.Community.ClubWorkGuest"/>); sie haben keine eigene Ansicht.
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

    /// <summary>Die Helfer:innen ohne Dogity-Konto des Vereins - die zweite Auswahl im Fenster "Helfer:innen hinzufügen".</summary>
    Task<Result<IReadOnlyList<ClubWorkGuestDto>>> GetGuestsAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>Legt eine Person ohne Konto an. Namensgleiche sind erlaubt (Namensgleichheit gibt es); die Oberfläche weist vorher darauf hin.</summary>
    Task<Result<ClubWorkGuestDto>> CreateGuestAsync(Guid userId, Guid clubId, SaveClubWorkGuestRequest request, CancellationToken ct = default);

    Task<Result<ClubWorkGuestDto>> UpdateGuestAsync(Guid userId, Guid clubId, Guid guestId, SaveClubWorkGuestRequest request, CancellationToken ct = default);

    /// <summary>Löscht die Person ohne Konto samt ihren Teilnahmen endgültig.</summary>
    Task<Result> DeleteGuestAsync(Guid userId, Guid clubId, Guid guestId, CancellationToken ct = default);

    /// <summary>
    /// Die Person nutzt jetzt Dogity: Ihre Teilnahmen wandern auf das Mitglied (ist es beim selben Einsatz schon
    /// abgehakt, bleibt die Teilnahme mit den höheren Minuten), danach wird die Person ohne Konto gelöscht.
    /// </summary>
    Task<Result> AssignGuestAsync(Guid userId, Guid clubId, Guid guestId, AssignClubWorkGuestRequest request, CancellationToken ct = default);

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
