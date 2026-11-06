namespace Dogity.Application.Community;

/// <param name="HelperCount">Abgehakte Helfer:innen (auch ehemalige Mitglieder).</param>
/// <param name="TotalMinutes">Summe der Minuten aller Helfer:innen.</param>
public record ClubWorkEventListItemDto(Guid Id, DateOnly Date, string Title, int DurationMinutes, int HelperCount, int TotalMinutes);

/// <param name="Id">Id der Teilnahme (nicht der Person).</param>
/// <param name="Former">Die Person ist nicht mehr im Verein oder hat kein Konto mehr: Die Namen sind leer, die Anzeige schreibt "Ehemaliges Mitglied".</param>
/// <param name="Minutes">Die Minuten, die zählen: die abweichenden, sonst die Dauer des Einsatzes.</param>
/// <param name="Custom">Die Minuten weichen von der Dauer des Einsatzes ab (sind für diese Person eigens gesetzt).</param>
/// <param name="GuestId">Gesetzt bei einer Person ohne Dogity-Konto (<paramref name="Guest"/>); dann ist <paramref name="UserId"/> leer.</param>
public record ClubWorkParticipantDto(Guid Id, Guid? UserId, string FirstName, string LastName, bool Former, int Minutes, bool Custom, Guid? GuestId = null, bool Guest = false);

public record ClubWorkEventDto(
    Guid Id,
    Guid ClubId,
    string ClubName,
    DateOnly Date,
    string Title,
    string? Note,
    int DurationMinutes,
    IReadOnlyList<ClubWorkParticipantDto> Participants);

public record SaveClubWorkEventRequest(DateOnly Date, string Title, string? Note, int DurationMinutes);

/// <param name="GuestIds">Personen ohne Konto (<see cref="ClubWorkGuestDto"/>) des Vereins.</param>
public record AddClubWorkParticipantsRequest(IReadOnlyList<Guid>? UserIds, IReadOnlyList<Guid>? GuestIds = null);

/// <param name="Minutes">Abweichende Minuten dieser Person; null = die Dauer des Einsatzes.</param>
public record SetClubWorkMinutesRequest(int? Minutes);

/// <summary>Eine Person, die sich abhaken lässt - die Auswahl im Fenster "Helfer:innen hinzufügen".</summary>
public record ClubWorkCandidateDto(Guid UserId, string FirstName, string LastName);

/// <summary>Eine Helfer:in ohne Dogity-Konto, wie die Verwaltung sie angelegt hat (Paket A2).</summary>
public record ClubWorkGuestDto(Guid Id, string FirstName, string LastName);

/// <param name="LastName">Leer, wenn nur ein Vorname bekannt ist.</param>
public record SaveClubWorkGuestRequest(string FirstName, string? LastName);

/// <param name="UserId">Das Mitglied (oder die Person mit Rolle), das die Person ohne Konto ab jetzt ist.</param>
public record AssignClubWorkGuestRequest(Guid UserId);

/// <param name="EventCount">Einsätze, bei denen die Person abgehakt ist (in der Sammelzeile: Teilnahmen aller Ehemaligen).</param>
/// <param name="Guest">Die Person hat kein Dogity-Konto.</param>
public record ClubWorkSummaryRowDto(string FirstName, string LastName, int EventCount, int Minutes, bool Guest = false);

/// <summary>
/// Die Jahresauswertung: je Person Einsätze und Minuten, nach Minuten absteigend.
/// </summary>
/// <param name="Years">Die Jahre, für die es Einsätze gibt, das neueste zuerst.</param>
/// <param name="EventCount">Einsätze des Jahres (auch ohne Helfer:innen).</param>
/// <param name="ParticipationCount">Abgehakte Teilnahmen im Jahr, alle Personen zusammen.</param>
/// <param name="Former">Eine Sammelzeile für ehemalige Mitglieder, ohne Namen; null, wenn es sie im Jahr nicht gibt.</param>
public record ClubWorkSummaryDto(
    int Year,
    IReadOnlyList<int> Years,
    int EventCount,
    int ParticipationCount,
    int TotalMinutes,
    IReadOnlyList<ClubWorkSummaryRowDto> Rows,
    ClubWorkSummaryRowDto? Former);

public record ClubMyWorkItemDto(Guid Id, DateOnly Date, string Title, int Minutes);

/// <summary>Die eigenen Einsätze eines Mitglieds mit der Summe des laufenden Jahres.</summary>
public record ClubMyWorkDto(int Year, int YearEventCount, int YearMinutes, IReadOnlyList<ClubMyWorkItemDto> Items);
