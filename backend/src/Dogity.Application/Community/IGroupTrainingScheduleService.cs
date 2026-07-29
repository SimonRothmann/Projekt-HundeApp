using Dogity.Application.Common;
using Dogity.Domain.Community;

namespace Dogity.Application.Community;

/// <summary>
/// Terminplanung fürs Gruppentraining (siehe docs/GROUP_TRAINING_SCHEDULE.md).
/// Jede:r ClubTrainer plant/bearbeitet Termine des Vereins; Mitglieder sehen
/// die Termine ihrer Gruppen read-only.
/// </summary>
public interface IGroupTrainingScheduleService
{
    /// <summary>Termine eines Vereins ab <paramref name="from"/> (Trainer-Sicht), optional gefiltert.</summary>
    Task<Result<IReadOnlyList<GroupTrainingSessionDto>>> GetClubScheduleAsync(
        Guid userId, Guid clubId, DateOnly from, DateOnly? to, Guid? groupId, GroupTrainingCategory? category, bool mineOnly, CancellationToken ct = default);

    /// <summary>Kommende Termine der Gruppen, in denen der Nutzer aktives Mitglied ist (read-only).</summary>
    Task<Result<IReadOnlyList<GroupTrainingSessionDto>>> GetMemberScheduleAsync(Guid userId, DateOnly from, CancellationToken ct = default);

    Task<Result<GroupTrainingSessionDto>> CreateSessionAsync(Guid userId, Guid clubId, CreateSessionRequest request, CancellationToken ct = default);
    Task<Result<GroupTrainingSessionDto>> UpdateSessionAsync(Guid userId, Guid sessionId, UpdateSessionRequest request, CancellationToken ct = default);
    Task<Result> CancelSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task<Result> DeleteSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

    /// <summary>Erzeugt eine Serie eigenständiger Termine und gibt sie zurück.</summary>
    Task<Result<IReadOnlyList<GroupTrainingSessionDto>>> GenerateSeriesAsync(Guid userId, Guid clubId, GenerateSeriesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Komponiert einen Inhalts-Entwurf für einen Termin der angegebenen
    /// Kategorie aus den Bausteinen des Vereins (Mix-Generator). Liefert die
    /// gewählten Bausteine in Reihenfolge; das Frontend übernimmt sie in den
    /// Termin und lässt sie frei anpassen.
    /// </summary>
    Task<Result<IReadOnlyList<GroupTrainingExerciseDto>>> GenerateContentAsync(Guid userId, Guid clubId, GroupTrainingCategory category, CancellationToken ct = default);
}
