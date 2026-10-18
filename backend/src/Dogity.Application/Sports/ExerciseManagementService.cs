using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;
using static Dogity.Application.Common.Texte;

namespace Dogity.Application.Sports;

public class ExerciseManagementService(IApplicationDbContext db) : IExerciseManagementService
{
    public async Task<Result<ExerciseDto>> CreateAsync(Guid actingUserId, bool isAdmin, CreateExerciseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ExerciseDto>.Failure("Name ist erforderlich.");

        var enFehler = PruefeEnglisch(request.NameEn, request.DescriptionEn, request.ScoringCriteriaEn);
        if (enFehler is not null)
            return Result<ExerciseDto>.Failure(enFehler);

        // SportId optional: sportartlose Übungen sind erlaubt (z.B. Grundlagen-
        // Training, das in mehreren Kontexten genutzt wird).
        if (request.SportId is { } sportId)
        {
            var sportExists = await db.Sports.AnyAsync(s => s.Id == sportId, ct);
            if (!sportExists)
                return Result<ExerciseDto>.NotFound("Sportart nicht gefunden.");
        }

        var authError = await CheckScopeAuthorizationAsync(actingUserId, isAdmin, request.ClubId, ct);
        if (authError is not null)
            return Result<ExerciseDto>.Failure(authError);

        var exercise = new Exercise
        {
            SportId = request.SportId,
            Name = request.Name.Trim(),
            NameEn = NullIfBlank(request.NameEn),
            Description = request.Description,
            DescriptionEn = NullIfBlank(request.DescriptionEn),
            Difficulty = request.Difficulty,
            Category = request.Category,
            ScoringCriteria = request.ScoringCriteria,
            ScoringCriteriaEn = NullIfBlank(request.ScoringCriteriaEn),
            ClubId = request.ClubId
        };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync(ct);

        return Result<ExerciseDto>.Success(ToDto(exercise));
    }

    public async Task<Result<ExerciseDto>> UpdateAsync(Guid actingUserId, bool isAdmin, Guid exerciseId, UpdateExerciseRequest request, CancellationToken ct = default)
    {
        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise is null)
            return Result<ExerciseDto>.NotFound("Übung nicht gefunden.");

        var authError = await CheckScopeAuthorizationAsync(actingUserId, isAdmin, exercise.ClubId, ct);
        if (authError is not null)
            return Result<ExerciseDto>.Failure(authError);

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ExerciseDto>.Failure("Name ist erforderlich.");

        var enFehler = PruefeEnglisch(request.NameEn, request.DescriptionEn, request.ScoringCriteriaEn);
        if (enFehler is not null)
            return Result<ExerciseDto>.Failure(enFehler);

        exercise.Name = request.Name.Trim();
        exercise.NameEn = NullIfBlank(request.NameEn);
        exercise.Description = request.Description;
        exercise.DescriptionEn = NullIfBlank(request.DescriptionEn);
        exercise.Difficulty = request.Difficulty;
        exercise.Category = request.Category;
        exercise.ScoringCriteria = request.ScoringCriteria;
        exercise.ScoringCriteriaEn = NullIfBlank(request.ScoringCriteriaEn);
        await db.SaveChangesAsync(ct);

        return Result<ExerciseDto>.Success(ToDto(exercise));
    }

    public async Task<Result> DeleteAsync(Guid actingUserId, bool isAdmin, Guid exerciseId, CancellationToken ct = default)
    {
        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise is null)
            return Result.NotFound("Übung nicht gefunden.");

        var authError = await CheckScopeAuthorizationAsync(actingUserId, isAdmin, exercise.ClubId, ct);
        if (authError is not null)
            return Result.Failure(authError);

        exercise.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string?> CheckScopeAuthorizationAsync(Guid actingUserId, bool isAdmin, Guid? clubId, CancellationToken ct)
    {
        if (clubId is null)
            return isAdmin ? null : "Nur Admins dürfen globale Übungen anlegen oder bearbeiten.";

        var isClubTrainer = await db.IsClubTrainerAsync(actingUserId, clubId.Value, ct);
        return isClubTrainer ? null : "Du bist für diesen Verein nicht als Trainer eingetragen.";
    }

    /// <summary>Längenprüfung der englischen Felder (Paket E4).</summary>
    private static string? PruefeEnglisch(string? nameEn, string? beschreibungEn, string? bewertungEn) =>
        Textlaengen.ZuLang(nameEn, Textlaengen.UebungNameEn, "Der englische Name")
        ?? Textlaengen.ZuLang(beschreibungEn, Textlaengen.UebungBeschreibungEn, "Die englische Beschreibung")
        ?? Textlaengen.ZuLang(bewertungEn, Textlaengen.UebungBewertungEn, "Die englische Bewertung");

    private static ExerciseDto ToDto(Exercise e) =>
        new(e.Id, e.SportId, e.Name, e.Description, e.Difficulty, e.Category, e.ScoringCriteria, e.ClubId, e.NameEn, e.DescriptionEn, e.ScoringCriteriaEn);
}
