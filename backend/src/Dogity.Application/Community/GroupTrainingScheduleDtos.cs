using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record SessionItemDto(Guid Id, Guid? ExerciseId, string? FreeText, int SortOrder, GroupTrainingExerciseDto? Exercise);

public record SessionTrainerDto(Guid UserId, string FirstName, string LastName);

/// <summary>
/// Zu- oder Absage einer Person - nur in der Trainer-Sicht gefüllt (siehe
/// <see cref="GroupTrainingSessionDto.Responses"/>).
/// </summary>
public record SessionResponseDto(Guid UserId, string FirstName, string LastName, bool IsAttending);

public record GroupTrainingSessionDto(
    Guid Id,
    Guid ClubId,
    Guid GroupId,
    string GroupName,
    GroupTrainingCategory Category,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location,
    string? Notes,
    GroupTrainingSessionStatus Status,
    int PlannedMinutes,
    IReadOnlyList<SessionItemDto> Items,
    IReadOnlyList<SessionTrainerDto> Trainers,
    /// <summary>Eigene Antwort des Aufrufers: true = kommt, false = kann nicht, null = noch offen.</summary>
    bool? MyResponse,
    /// <summary>Zählung über aktive Gruppenmitglieder - für Mitglieder und Trainer:innen gleich.</summary>
    int AttendingCount,
    int DecliningCount,
    /// <summary>Noch keine Antwort - NUR für Trainer:innen, für Mitglieder 0 (sonst wäre die Gruppengröße ablesbar).</summary>
    int OpenCount,
    /// <summary>
    /// Wer zu- bzw. abgesagt hat, mit Namen - NUR für Trainer:innen des
    /// Vereins bzw. des Termins. Für Mitglieder leer: sie sehen Zahlen, keine Personen.
    /// </summary>
    IReadOnlyList<SessionResponseDto> Responses);

/// <summary>Antwort eines Mitglieds auf einen Termin ("Ich komme" / "Kann nicht").</summary>
public record RespondToSessionRequest(bool Attending);

/// <summary>Eine Inhaltsposition: entweder ExerciseId (Baustein) ODER FreeText.</summary>
public record SessionContentInput(Guid? ExerciseId = null, string? FreeText = null);

public record CreateSessionRequest(
    Guid GroupId,
    GroupTrainingCategory Category,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location,
    string? Notes,
    IReadOnlyList<Guid> TrainerUserIds,
    IReadOnlyList<SessionContentInput> Items);

public record UpdateSessionRequest(
    GroupTrainingCategory Category,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location,
    string? Notes,
    IReadOnlyList<Guid> TrainerUserIds,
    IReadOnlyList<SessionContentInput> Items);

/// <summary>
/// Serien-Generator: erzeugt für jeden übergebenen Zeitpunkt einen
/// eigenständigen Termin mit gleichem Inhalt/Trainer/Ort. Das Frontend rechnet
/// „Wochentag + Uhrzeit + Zeitraum" (zeitzonen-korrekt im Browser) in die
/// konkreten <see cref="Starts"/> um.
/// </summary>
public record GenerateSeriesRequest(
    Guid GroupId,
    GroupTrainingCategory Category,
    IReadOnlyList<DateTimeOffset> Starts,
    int DurationMinutes,
    string? Location,
    IReadOnlyList<Guid> TrainerUserIds,
    IReadOnlyList<SessionContentInput> Items,
    // true = pro Termin einen frischen Mix aus der Bibliothek generieren
    // (abwechslungsreiche Serie); dann werden Items ignoriert.
    bool AutoGenerateContent = false);
