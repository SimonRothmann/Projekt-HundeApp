using Dogity.Application.Planning;
using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <param name="MaxPoints">Höchstpunktzahl der Ordnung; null, wenn sie nicht nach Punkten gewertet wird (oder in Listen, die sie nicht brauchen).</param>
public record ClubExamRegulationDto(Guid Id, string Name, string? NameEn, int? MaxPoints = null);

/// <param name="Regulations">Angebotene Ordnungen und die der Einträge zusammen.</param>
public record ClubExamListItemDto(
    Guid Id,
    DateOnly Date,
    TimeOnly? StartTime,
    string Location,
    ClubExamStatus Status,
    int TeamCount,
    int PassedCount,
    IReadOnlyList<ClubExamRegulationDto> Regulations);

/// <summary>Was Training und Vorstand vom nächsten Prüfungstag sehen: Datum, Ordnungen, Ort - keine Meldeliste.</summary>
public record ClubExamHinweisDto(DateOnly Date, string Location, IReadOnlyList<ClubExamRegulationDto> Regulations);

/// <summary>Wie ein Team auf der Liste steht: Mitglied mit Hund, Gast - oder ein Mitglied, das es nicht mehr gibt (Austritt, Hund oder Konto gelöscht).</summary>
public enum ClubExamTeamKind
{
    Member,
    Guest,
    FormerMember
}

/// <summary>
/// Die Prüfungsreife eines Hundes für die Prüfungsleitung: nur Ampel, Zählung und was noch fehlt
/// (offene und wackelige Übungen) - kein Tagebuch, keine Bewertungen, keine Pausen, keine Theorie.
/// </summary>
public record ClubExamReifeDto(Reifeampel Ampel, int Sicher, int Wackelig, int Offen, IReadOnlyList<ClubExamReifeUebungDto> Fehlend);

public record ClubExamReifeUebungDto(Guid ExerciseId, string Name, string? NameEn, bool IsMandatory, ReifeZustand Zustand);

/// <param name="StartNumber">Platz in der Starterliste der Ordnung, von 1 an.</param>
/// <param name="Reife">Nur bei Mitglied-Teams ohne Ergebnis, deren Hund ein laufendes Ziel zur Ordnung hat; sonst null ("kein Ziel in Dogity").</param>
/// <param name="Info">Verein und Rasse eines Gast-Teams, frei geschrieben.</param>
public record ClubExamEntryDto(
    Guid Id,
    Guid RegulationId,
    int StartNumber,
    ClubExamTeamKind Kind,
    string? HandlerName,
    string? DogName,
    string? Breed,
    DateOnly? Birthday,
    string? Info,
    ClubExamReifeDto? Reife,
    ClubExamResult Result,
    int? Score,
    string? ResultNote);

public record ClubExamDto(
    Guid Id,
    Guid ClubId,
    string ClubName,
    DateOnly Date,
    TimeOnly? StartTime,
    string Location,
    string? JudgeName,
    DateOnly? RegistrationDeadline,
    string? Note,
    ClubExamStatus Status,
    IReadOnlyList<ClubExamRegulationDto> OfferedRegulations,
    IReadOnlyList<ClubExamRegulationDto> Regulations,
    IReadOnlyList<ClubExamEntryDto> Entries);

/// <param name="RegulationIds">Die angebotenen Ordnungen; leer = keine Vorgabe.</param>
public record SaveClubExamRequest(
    DateOnly Date,
    TimeOnly? StartTime,
    string Location,
    string? JudgeName,
    DateOnly? RegistrationDeadline,
    string? Note,
    IReadOnlyList<Guid>? RegulationIds);

/// <summary>Entweder <paramref name="MemberUserId"/> mit <paramref name="DogId"/> oder die Gast-Felder.</summary>
public record AddClubExamEntryRequest(
    Guid RegulationId,
    Guid? MemberUserId,
    Guid? DogId,
    string? GuestHandlerName,
    string? GuestDogName,
    string? GuestInfo);

/// <param name="Direction">-1 = in der Starterliste nach oben, 1 = nach unten.</param>
public record MoveClubExamEntryRequest(int Direction);

public record SetClubExamResultRequest(ClubExamResult Result, int? Score, string? Note);

/// <summary>Ein Vereinsmitglied mit seinen Hunden - die Auswahl im Fenster "Team eintragen".</summary>
public record ClubExamCandidateDto(Guid UserId, string FirstName, string LastName, IReadOnlyList<ClubExamCandidateDogDto> Dogs);

public record ClubExamCandidateDogDto(Guid Id, string Name, string? Breed, DateOnly? Birthday);

/// <summary>Die Meldung eines Hundes zu einem anstehenden Prüfungstag - "Gemeldet: ..." auf der Hundeseite.</summary>
public record DogExamRegistrationDto(Guid EntryId, DateOnly Date, string ClubName, ClubExamRegulationDto Regulation);
