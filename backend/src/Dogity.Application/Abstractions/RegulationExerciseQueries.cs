using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Abstractions;

/// <summary>
/// Zugriff auf die Verknüpfung Prüfungsordnungs-Version ↔ Übung, der auch
/// entfernte Zeilen sieht.
///
/// Hintergrund: Das Entfernen einer Übung aus einer Prüfungsordnung ist ein
/// Soft-Delete (<c>DeletedAt</c>), der eindeutige Index auf
/// (RegulationVersionId, ExerciseId) kennt aber keinen Filter darauf. Eine
/// gewöhnliche Abfrage findet die entfernte Zeile nicht mehr und legt eine
/// zweite an - die Datenbank weist sie zurück. Für den Seeder war das
/// besonders unangenehm: er läuft VOR <c>app.Run()</c>, eine von Hand
/// entfernte geseedete Übung hätte die Instanz beim nächsten Start also gar
/// nicht mehr hochkommen lassen.
///
/// Deshalb: vorhandene Zeile suchen, weichgelöschte wiederbeleben, und nur
/// wenn es wirklich keine gibt, eine neue anlegen.
/// </summary>
public static class RegulationExerciseQueries
{
    /// <summary>
    /// Die Verknüpfung zu dieser Übung - auch, wenn sie zwischenzeitlich
    /// entfernt wurde. <c>null</c> nur, wenn es sie noch nie gab.
    /// </summary>
    public static Task<RegulationExercise?> FindLinkIncludingRemovedAsync(
        this IApplicationDbContext db,
        Guid regulationVersionId,
        Guid exerciseId,
        CancellationToken ct = default) =>
        db.RegulationExercises
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(re => re.RegulationVersionId == regulationVersionId && re.ExerciseId == exerciseId, ct);
}
