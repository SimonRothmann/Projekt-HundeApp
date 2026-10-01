using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Anmeldungen zu einer Gruppe verwalten: Anmeldelink, Liste, Anwesenheit je
/// Tag, "bezahlt", Import. Alles nur für die, die die Gruppe verwalten dürfen
/// (Leitung, weitere Trainer:innen, Trainer:innen des Vereins). Fremde
/// bekommen überall dieselbe Antwort wie bei einer Gruppe, die es nicht gibt.
/// </summary>
public interface IGroupRegistrationService
{
    /// <summary>Der aktuelle Code oder null (kein Link / geschlossen).</summary>
    Task<Result<GroupRegistrationLinkDto?>> GetLinkAsync(Guid callerId, Guid groupId, CancellationToken ct = default);

    /// <summary>Erzeugt einen neuen Code. Ein vorhandener wird damit ersetzt und ungültig.</summary>
    Task<Result<GroupRegistrationLinkDto>> RegenerateLinkAsync(Guid callerId, Guid groupId, CancellationToken ct = default);

    /// <summary>Schließt das Formular. Idempotent. Die bisherigen Anmeldungen bleiben.</summary>
    Task<Result> DisableLinkAsync(Guid callerId, Guid groupId, CancellationToken ct = default);

    /// <summary>Alle Anmeldungen der Gruppe, neueste zuerst.</summary>
    Task<Result<IReadOnlyList<GroupRegistrationDto>>> ListAsync(Guid callerId, Guid groupId, CancellationToken ct = default);

    /// <summary>Von Hand anlegen (Herkunft "von Hand").</summary>
    Task<Result<GroupRegistrationDto>> CreateAsync(Guid callerId, Guid groupId, GroupRegistrationRequest request, CancellationToken ct = default);

    Task<Result<GroupRegistrationDto>> UpdateAsync(Guid callerId, Guid groupId, Guid registrationId, GroupRegistrationRequest request, CancellationToken ct = default);

    /// <summary>Löscht die Anmeldung samt Anwesenheit endgültig.</summary>
    Task<Result> DeleteAsync(Guid callerId, Guid groupId, Guid registrationId, CancellationToken ct = default);

    /// <summary>Setzt "bezahlt" (PaidAt = jetzt, PaidByUserId = Aufrufer:in) oder nimmt es zurück. Idempotent.</summary>
    Task<Result<GroupRegistrationDto>> SetPaidAsync(Guid callerId, Guid groupId, Guid registrationId, bool paid, CancellationToken ct = default);

    /// <summary>Tage mit Termin der Gruppe: letzte vier bis nächste zwei Wochen (Vereinszeit), ohne abgesagte.</summary>
    Task<Result<IReadOnlyList<AttendanceDayDto>>> GetAttendanceDaysAsync(Guid callerId, Guid groupId, CancellationToken ct = default);

    /// <summary>Die Anmeldungen, die an diesem Tag abgehakt sind.</summary>
    Task<Result<IReadOnlyList<Guid>>> GetAttendanceAsync(Guid callerId, Guid groupId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Hakt eine Anmeldung für einen Tag ab oder nimmt den Haken zurück. Je Tag
    /// höchstens eine Zeile; wiederholtes Abhaken ändert sie. Zukünftige Tage
    /// sind nicht abhakbar. Gibt es an dem Tag einen Termin der Gruppe, wird er
    /// verknüpft.
    /// </summary>
    Task<Result> SetAttendanceAsync(Guid callerId, Guid groupId, Guid registrationId, DateOnly date, bool present, CancellationToken ct = default);

    /// <summary>
    /// Übernimmt Zeilen aus dem CSV einer früheren Google-Formular-Liste. Jede
    /// Zeile wird wie das Formular geprüft; schon vorhandene (gleiche Nummer,
    /// gleicher Rufname) werden übersprungen, ohne Zeitgrenze.
    /// </summary>
    Task<Result<ImportRegistrationsResultDto>> ImportAsync(Guid callerId, Guid groupId, ImportRegistrationsRequest request, CancellationToken ct = default);
}
