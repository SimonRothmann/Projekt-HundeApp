using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Sports;

public class RegulationImportService(IApplicationDbContext db, IRegulationPdfParser parser, TimeProvider timeProvider) : IRegulationImportService
{
    public Task<Result<IReadOnlyList<ParsedExerciseCandidate>>> ScanAsync(CancellationToken ct = default) =>
        parser.ScanAsync(ct);

    public async Task<Result> ApplyAsync(ApplyRegulationImportRequest request, CancellationToken ct = default)
    {
        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == request.RegulationId, ct);
        if (regulation is null)
            return Result.NotFound("Prüfungsordnung nicht gefunden.");

        // Globale Sportarten pflegt der Seeder mit: Was der Import dort von Hand
        // ergänzt oder ändert, muss er wie die Katalog-Pflege als Handänderung
        // kennzeichnen (EditedAt), sonst räumt der nächste Start es wieder ab.
        var global = await db.Sports.AnyAsync(s => s.Id == regulation.SportId && s.ClubId == null, ct);

        var currentVersion = await db.AktuelleFassungAsync(regulation.Id, Vereinszeit.Tag(timeProvider.GetUtcNow()), ct);
        if (currentVersion is null)
            return Result.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        // Neue Übungen hängen sich in Reihenfolge der Kandidatenliste hinten an
        // (das ist die Reihenfolge, in der sie im PDF stehen). Bereits
        // vorhandene Zeilen behalten ihren SortOrder - ein Import darf eine
        // gepflegte oder geseedete Reihenfolge nicht umsortieren, nur weil er
        // eine einzelne Übung nachträgt.
        var nextSortOrder = (await db.RegulationExercises
            .Where(re => re.RegulationVersionId == currentVersion.Id)
            .Select(re => (int?)re.SortOrder)
            .MaxAsync(ct) ?? -1) + 1;

        foreach (var candidate in request.Candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Name))
                continue;

            var exercise = await db.Exercises.FirstOrDefaultAsync(
                e => e.SportId == regulation.SportId && e.ClubId == null && e.Name.ToLower() == candidate.Name.Trim().ToLower(), ct);

            if (exercise is null)
            {
                exercise = new Exercise
                {
                    SportId = regulation.SportId,
                    Name = candidate.Name.Trim(),
                    Difficulty = ExerciseDifficulty.Beginner
                };
                db.Exercises.Add(exercise);
            }

            // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index,
            // siehe RegulationExerciseQueries).
            var regulationExercise = await db.FindLinkIncludingRemovedAsync(currentVersion.Id, exercise.Id, ct);

            if (regulationExercise is null)
            {
                db.RegulationExercises.Add(new RegulationExercise
                {
                    RegulationVersionId = currentVersion.Id,
                    ExerciseId = exercise.Id,
                    MaxPoints = candidate.MaxPoints,
                    IsMandatory = true,
                    SortOrder = nextSortOrder++,
                    EditedAt = global ? DateTimeOffset.UtcNow : null
                });
            }
            else
            {
                // Wiederbeleben: die Übung steht wieder in der importierten
                // Liste, also gehört sie wieder zur Prüfungsordnung.
                if (global && (regulationExercise.DeletedAt is not null || regulationExercise.MaxPoints != candidate.MaxPoints))
                    regulationExercise.EditedAt = DateTimeOffset.UtcNow;
                regulationExercise.DeletedAt = null;
                regulationExercise.MaxPoints = candidate.MaxPoints;
            }
        }

        regulation.LastSyncedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
