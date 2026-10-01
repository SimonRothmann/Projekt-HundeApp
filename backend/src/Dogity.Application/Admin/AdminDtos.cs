namespace Dogity.Application.Admin;

public record AdminStatsDto(int UserCount, int DogCount, int GroupCount, int TrainingSessionCount, int GpsTrackCount, AdminRecentStatsDto Last30Days);

/// <summary>
/// Die letzten 30 Tage - nur Zählungen, keine Personen.
///
/// Die Zahlen nach <paramref name="NewAccounts"/> sind Teilmengen der neuen
/// Konten (wer in den 30 Tagen dazukam UND etwas getan hat). Die aktiven
/// Konten sind dagegen ALLE Konten, die in dem Zeitraum etwas angelegt haben,
/// also nicht nur die neuen.
/// </summary>
/// <param name="NewAccounts">Konten, die in den letzten 30 Tagen angelegt wurden.</param>
/// <param name="WithDog">Davon mit mindestens einem Hund (aktive Besitzerschaft, Hund nicht gelöscht).</param>
/// <param name="InClub">Davon mit genehmigter Vereinsmitgliedschaft.</param>
/// <param name="WithGoal">Davon mit mindestens einem Ziel (aktiv oder erreicht) für einen ihrer Hunde.</param>
/// <param name="ViaClubLink">Davon über einen Vereins-Einladungslink gekommen.</param>
/// <param name="ActiveAccounts">Verschiedene Personen mit mindestens einem Trainingseintrag oder einer Fährte, die in den letzten 30 Tagen angelegt wurden.</param>
public record AdminRecentStatsDto(int NewAccounts, int WithDog, int InClub, int WithGoal, int ViaClubLink, int ActiveAccounts);

public record AdminUserDto(Guid Id, string Email, string FirstName, string LastName, string[] Roles, bool IsLockedOut);

public record AdminUserPageDto(IReadOnlyList<AdminUserDto> Users, int TotalCount, int TotalPages, int Page, int PageSize);

public record UpdateRegulationSourceRequest(string? SourceUrl, string? LatestKnownVersionLabel);

public record SetUserPasswordRequest(string NewPassword);

/// <summary>
/// Was an Konten hängt, die es nicht mehr gibt - nur Zählungen, keine Personen-Ids
/// und keine Namen. "Sonstige" fasst die kleinen Verweise zusammen (Besitzzeilen,
/// Mitgliedschaften, Einstellungen, Lernstand ...), die einzeln keine Zahl wert sind.
/// </summary>
public record OrphanedDataDto(int Konten, int Trainings, int Hunde, int Ziele, int Benachrichtigungen, int Sonstige);

/// <param name="Konten">Wie viele Konten bereinigt wurden.</param>
public record OrphanedDataPurgeDto(int Konten);
