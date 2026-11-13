using Dogity.Application.Common;

namespace Dogity.Application.Community;

/// <summary>
/// Prüfungstage des Vereins (Paket V4, siehe docs/BERECHTIGUNGEN.md). Jede Methode prüft die Rolle
/// (<see cref="Domain.Community.ClubRollen.Pruefungen"/>: Ausbildungswart:in, Verwaltung) im eigenen
/// Verein; fehlt sie oder gehört der Verein jemand anderem, ist das Ergebnis "nicht gefunden".
/// </summary>
public interface IClubExamService
{
    /// <summary>Alle Prüfungstage des Vereins, nach Datum aufsteigend.</summary>
    Task<Result<IReadOnlyList<ClubExamListItemDto>>> GetListAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>Ein Prüfungstag mit Meldeliste und - bei Teams ohne Ergebnis - der Prüfungsreife (ohne Theorie).</summary>
    Task<Result<ClubExamDto>> GetAsync(Guid userId, Guid clubId, Guid examId, CancellationToken ct = default);

    Task<Result<ClubExamDto>> CreateAsync(Guid userId, Guid clubId, SaveClubExamRequest request, CancellationToken ct = default);
    Task<Result<ClubExamDto>> UpdateAsync(Guid userId, Guid clubId, Guid examId, SaveClubExamRequest request, CancellationToken ct = default);

    /// <summary>Sagt den Tag ab; die Mitglied-Teams bekommen eine Nachricht. Nicht mehr möglich, sobald Ergebnisse eingetragen sind.</summary>
    Task<Result> CancelAsync(Guid userId, Guid clubId, Guid examId, CancellationToken ct = default);

    /// <summary>Löscht den Tag samt Teams (auch die Gastdaten); nicht mehr möglich, sobald Ergebnisse eingetragen sind. Mitglied-Teams ohne Ergebnis bekommen eine Nachricht.</summary>
    Task<Result> DeleteAsync(Guid userId, Guid clubId, Guid examId, CancellationToken ct = default);

    /// <summary>Die Mitglieder mit ihren Hunden - die Auswahl beim Eintragen eines Teams.</summary>
    Task<Result<IReadOnlyList<ClubExamCandidateDto>>> GetCandidatesAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    Task<Result> AddEntryAsync(Guid userId, Guid clubId, Guid examId, AddClubExamEntryRequest request, CancellationToken ct = default);
    Task<Result> RemoveEntryAsync(Guid userId, Guid clubId, Guid examId, Guid entryId, CancellationToken ct = default);
    Task<Result> MoveEntryAsync(Guid userId, Guid clubId, Guid examId, Guid entryId, int direction, CancellationToken ct = default);

    /// <summary>Trägt das Ergebnis ein (oder setzt es zurück) und zieht bei Mitglied-Teams das Ziel am Hund nach.</summary>
    Task<Result> SetResultAsync(Guid userId, Guid clubId, Guid examId, Guid entryId, SetClubExamResultRequest request, CancellationToken ct = default);

    /// <summary>Die anstehenden Meldungen eines Hundes ("Gemeldet: ...") - nur für seine Besitzer:innen.</summary>
    Task<Result<IReadOnlyList<DogExamRegistrationDto>>> GetForDogAsync(Guid userId, Guid dogId, CancellationToken ct = default);
}
