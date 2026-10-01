using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <summary>Der Anmeldelink einer Gruppe - nur für deren Trainer:innen sichtbar.</summary>
public record GroupRegistrationLinkDto(string Code);

/// <summary>
/// Was die öffentliche Anmeldeseite ohne Anmeldung erfährt: Vereins- und
/// Gruppenname. Bewusst nichts weiter - wer den Link hat, soll nichts über
/// Trainer:innen, Mitglieder oder andere Angemeldete erfahren. Gehört die
/// Gruppe keinem Verein, ist <paramref name="ClubName"/> null.
/// </summary>
public record GroupRegistrationFormDto(string? ClubName, string GroupName);

/// <param name="Website">
/// Köder-Feld gegen Roboter: Menschen sehen es nicht und lassen es leer. Ist es
/// gefüllt, tut der Server so, als sei alles gut gegangen, und speichert nichts.
/// </param>
public record PublicRegistrationRequest(
    string? FirstName,
    string? LastName,
    string? DogName,
    string? DogBreed,
    DateOnly? DogBirthDate,
    string? Phone,
    bool Consent,
    string? Website);

/// <summary>Eingabe einer Trainer:in: manuell anlegen oder bearbeiten (alle Felder plus Notiz).</summary>
public record GroupRegistrationRequest(
    string? FirstName,
    string? LastName,
    string? DogName,
    string? DogBreed,
    DateOnly? DogBirthDate,
    string? Phone,
    string? Notes);

/// <param name="Source">Numerisch wie alle Aufzählungen der API: 0 = Formular, 1 = Import, 2 = von Hand.</param>
public record GroupRegistrationDto(
    Guid Id,
    string FirstName,
    string LastName,
    string DogName,
    string DogBreed,
    DateOnly DogBirthDate,
    string Phone,
    GroupRegistrationSource Source,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? PaidAt,
    string? Notes,
    int AttendanceCount);

public record SetRegistrationPaidRequest(bool Paid);

public record SetRegistrationAttendanceRequest(DateOnly Date, bool Present);

/// <summary>Ein Tag mit Termin der Gruppe, für die Tagesauswahl der Anwesenheitsliste.</summary>
public record AttendanceDayDto(DateOnly Date, Guid SessionId, DateTimeOffset StartsAt);

/// <param name="Zeile">Zeilennummer in der Datei (vom Browser gezählt) - für die Fehlermeldung an die Trainer:in.</param>
/// <param name="RegisteredAt">Zeitstempel aus dem Google-Formular; fehlt er, gilt der Moment des Imports.</param>
public record ImportRegistrationRow(
    int Zeile,
    string? FirstName,
    string? LastName,
    string? DogName,
    string? DogBreed,
    DateOnly? DogBirthDate,
    string? Phone,
    DateTimeOffset? RegisteredAt);

public record ImportRegistrationsRequest(IReadOnlyList<ImportRegistrationRow>? Rows);

public record ImportRegistrationErrorDto(int Zeile, string Meldung);

public record ImportRegistrationsResultDto(
    int Angelegt,
    int Uebersprungen,
    IReadOnlyList<ImportRegistrationErrorDto> Fehler);
