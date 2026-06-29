using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Sports;

public class RegulationImportService(IApplicationDbContext db, IRegulationPdfParser parser) : IRegulationImportService
{
    public Task<Result<IReadOnlyList<ParsedExerciseCandidate>>> ScanAsync(CancellationToken ct = default) =>
        parser.ScanAsync(ct);

    public async Task<Result> ApplyAsync(ApplyRegulationImportRequest request, CancellationToken ct = default)
    {
        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == request.RegulationId, ct);
        if (regulation is null)
            return Result.Failure("Prüfungsordnung nicht gefunden.");

        var currentVersion = await db.RegulationVersions
            .Where(v => v.RegulationId == regulation.Id)
            .OrderByDescending(v => v.ValidFrom)
            .FirstOrDefaultAsync(ct);
        if (currentVersion is null)
            return Result.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

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

            var regulationExercise = await db.RegulationExercises
                .FirstOrDefaultAsync(re => re.RegulationVersionId == currentVersion.Id && re.ExerciseId == exercise.Id, ct);

            if (regulationExercise is null)
            {
                db.RegulationExercises.Add(new RegulationExercise
                {
                    RegulationVersionId = currentVersion.Id,
                    ExerciseId = exercise.Id,
                    MaxPoints = candidate.MaxPoints,
                    IsMandatory = true
                });
            }
            else
            {
                regulationExercise.MaxPoints = candidate.MaxPoints;
            }
        }

        regulation.LastSyncedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
