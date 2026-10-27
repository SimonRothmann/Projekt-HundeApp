using Dogity.Domain.Dogs;

namespace Dogity.Application.Dogs;

/// <summary>
/// Ein Eintrag zur Gesundheit des Hundes, wie ihn Besitzer:innen und Mitbesitzer:innen
/// sehen - und niemand sonst (siehe <see cref="IDogHealthService"/>).
/// </summary>
/// <param name="Date">Gegeben am bzw. Tag des Tierarzttermins.</param>
/// <param name="Time">Nur beim Tierarzttermin, dort optional.</param>
/// <param name="DueDate">Gültig bis bzw. nächste fällig (einschließlich); beim Tierarzttermin leer.</param>
/// <param name="IsCurrent">
/// Ob der Eintrag zu "Aktuell" gehört: der jüngste je Art und Titel, bei Tierarztterminen
/// ein Termin heute oder später. Alles andere ist Verlauf.
/// </param>
public record DogHealthRecordDto(
    Guid Id,
    DogHealthKind Kind,
    string Title,
    DateOnly Date,
    TimeOnly? Time,
    DateOnly? DueDate,
    string? Note,
    bool IsCurrent);

/// <summary>Anlegen und Ändern eines Eintrags haben dieselben Felder.</summary>
public record SaveDogHealthRecordRequest(
    DogHealthKind Kind,
    string Title,
    DateOnly Date,
    TimeOnly? Time,
    DateOnly? DueDate,
    string? Note);

/// <summary>
/// Der Fälligkeitsstand eines Hundes in einem Blick - dort mitgeliefert, wo Besitzer:innen ihre
/// Hunde ohnehin laden (Hundeliste, Hundeseite, Startseite). In keinem Trainer-DTO.
/// null am Hund heißt: nichts Datiertes eingetragen.
/// </summary>
/// <param name="Overdue">Anzahl aktueller Einträge, deren Frist verstrichen ist.</param>
/// <param name="DueSoon">Anzahl aktueller Einträge, die in höchstens 30 Tagen (auch heute) fällig werden.</param>
/// <param name="NextDue">Der aktuelle Eintrag mit dem frühesten Fälligkeitsdatum - auch ein überfälliger; null ohne.</param>
/// <param name="NextAppointment">Der nächste Tierarzttermin (heute oder später); null ohne.</param>
public record DogHealthSummaryDto(
    int Overdue,
    int DueSoon,
    DogHealthDueDto? NextDue,
    DogHealthAppointmentDto? NextAppointment);

public record DogHealthDueDto(Guid Id, DogHealthKind Kind, string Title, DateOnly DueDate, HealthState State);

public record DogHealthAppointmentDto(Guid Id, string Title, DateOnly Date, TimeOnly? Time);
