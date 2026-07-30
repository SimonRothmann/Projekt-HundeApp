using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record SessionItemDto(Guid Id, Guid? ExerciseId, string? FreeText, int SortOrder, GroupTrainingExerciseDto? Exercise);

public record SessionTrainerDto(Guid UserId, string FirstName, string LastName);

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
    IReadOnlyList<SessionTrainerDto> Trainers);

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
