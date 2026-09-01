using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Sports;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Sports;

public class RegulationManagementService(IApplicationDbContext db) : IRegulationManagementService
{
    public async Task<Result<SportDto>> UpdateSportAsync(Guid actingUserId, bool isAdmin, Guid sportId, UpdateSportRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<SportDto>.Failure("Name ist erforderlich.");

        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Id == sportId, ct);
        if (sport is null)
            return Result<SportDto>.NotFound("Sportart nicht gefunden.");

        var authError = await AuthorizeAsync(actingUserId, isAdmin, sport.ClubId, ct);
        if (authError is not null)
            return Result<SportDto>.Failure(authError);

        sport.Name = request.Name.Trim();
        sport.Description = NullIfBlank(request.Description);
        await db.SaveChangesAsync(ct);

        return Result<SportDto>.Success(new SportDto(sport.Id, sport.Code, sport.Name, sport.Description, sport.ClubId));
    }

    public async Task<Result<RegulationDto>> UpdateRegulationAsync(Guid actingUserId, bool isAdmin, Guid regulationId, UpdateRegulationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<RegulationDto>.Failure("Name ist erforderlich.");

        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == regulationId, ct);
        if (regulation is null)
            return Result<RegulationDto>.NotFound("Prüfungsordnung nicht gefunden.");

        var authError = await AuthorizeForSportAsync(actingUserId, isAdmin, regulation.SportId, ct);
        if (authError is not null)
            return Result<RegulationDto>.Failure(authError);

        regulation.Name = request.Name.Trim();
        regulation.Description = NullIfBlank(request.Description);
        regulation.SourceUrl = NullIfBlank(request.SourceUrl);
        await db.SaveChangesAsync(ct);

        return Result<RegulationDto>.Success(new RegulationDto(
            regulation.Id, regulation.Name, regulation.SourceUrl, regulation.LastSyncedAt, regulation.LatestKnownVersionLabel, regulation.Description));
    }

    public async Task<Result<RegulationExerciseDto>> AddRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, AddRegulationExerciseRequest request, CancellationToken ct = default)
    {
        if (request.MaxPoints < 0)
            return Result<RegulationExerciseDto>.Failure("Punkte dürfen nicht negativ sein.");

        var (version, authError) = await GetEditableVersionAsync(actingUserId, isAdmin, regulationId, ct);
        if (authError is not null)
            return Result<RegulationExerciseDto>.Failure(authError);
        if (version is null)
            return Result<RegulationExerciseDto>.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        var exercise = await db.Exercises.FirstOrDefaultAsync(e => e.Id == request.ExerciseId, ct);
        if (exercise is null)
            return Result<RegulationExerciseDto>.NotFound("Übung nicht gefunden.");

        // Auch entfernte Zeilen ansehen - sonst scheitert das erneute
        // Hinzufügen einer zuvor entfernten Übung am eindeutigen Index
        // (Soft-Delete, siehe RegulationExerciseQueries).
        var existing = await db.FindLinkIncludingRemovedAsync(version.Id, request.ExerciseId, ct);
        if (existing is { DeletedAt: null })
            return Result<RegulationExerciseDto>.Failure("Diese Übung ist bereits Teil der Prüfungsordnung.");

        // Von Hand ergänzte Übungen hängen sich ans Ende der Prüfungsordnung -
        // wo genau sie fachlich hingehören, weiß nur der Seed, und eine
        // eingeschobene Übung würde die dort gepflegte Reihenfolge zerreißen.
        var lastSortOrder = await db.RegulationExercises
            .Where(re => re.RegulationVersionId == version.Id)
            .Select(re => (int?)re.SortOrder)
            .MaxAsync(ct) ?? -1;

        RegulationExercise link;
        if (existing is not null)
        {
            link = existing;
            link.DeletedAt = null;
            link.SortOrder = lastSortOrder + 1;
        }
        else
        {
            link = new RegulationExercise
            {
                RegulationVersionId = version.Id,
                ExerciseId = request.ExerciseId,
                SortOrder = lastSortOrder + 1
            };
            db.RegulationExercises.Add(link);
        }

        link.IsMandatory = request.IsMandatory;
        link.MaxPoints = request.MaxPoints;
        link.ScoringNotes = NullIfBlank(request.ScoringNotes);
        await db.SaveChangesAsync(ct);

        return Result<RegulationExerciseDto>.Success(new RegulationExerciseDto(
            exercise.Id, exercise.Name, link.IsMandatory, link.MaxPoints, link.ScoringNotes));
    }

    public async Task<Result> UpdateRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, Guid exerciseId, UpdateRegulationExerciseRequest request, CancellationToken ct = default)
    {
        if (request.MaxPoints < 0)
            return Result.Failure("Punkte dürfen nicht negativ sein.");

        var (version, authError) = await GetEditableVersionAsync(actingUserId, isAdmin, regulationId, ct);
        if (authError is not null)
            return Result.Failure(authError);
        if (version is null)
            return Result.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        var link = await db.RegulationExercises.FirstOrDefaultAsync(
            re => re.RegulationVersionId == version.Id && re.ExerciseId == exerciseId, ct);
        if (link is null)
            return Result.Failure("Übung ist nicht Teil dieser Prüfungsordnung.");

        // SortOrder bleibt bewusst unberührt: Punkte oder Pflicht-Kennzeichen zu
        // ändern darf die Reihenfolge der Prüfungsordnung nicht verwerfen.
        link.IsMandatory = request.IsMandatory;
        link.MaxPoints = request.MaxPoints;
        link.ScoringNotes = NullIfBlank(request.ScoringNotes);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveRegulationExerciseAsync(Guid actingUserId, bool isAdmin, Guid regulationId, Guid exerciseId, CancellationToken ct = default)
    {
        var (version, authError) = await GetEditableVersionAsync(actingUserId, isAdmin, regulationId, ct);
        if (authError is not null)
            return Result.Failure(authError);
        if (version is null)
            return Result.Failure("Keine gültige Version für diese Prüfungsordnung gefunden.");

        var link = await db.RegulationExercises.FirstOrDefaultAsync(
            re => re.RegulationVersionId == version.Id && re.ExerciseId == exerciseId, ct);
        if (link is null)
            return Result.Failure("Übung ist nicht Teil dieser Prüfungsordnung.");

        // Soft-Delete (RegulationExercise hat einen DeletedAt-QueryFilter) - die
        // Übung selbst bleibt im Katalog, nur die Zuordnung zur PO wird entfernt.
        link.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ----- Helpers -----

    private async Task<(RegulationVersion? Version, string? Error)> GetEditableVersionAsync(Guid actingUserId, bool isAdmin, Guid regulationId, CancellationToken ct)
    {
        var regulation = await db.Regulations.FirstOrDefaultAsync(r => r.Id == regulationId, ct);
        if (regulation is null)
            return (null, "Prüfungsordnung nicht gefunden.");

        var authError = await AuthorizeForSportAsync(actingUserId, isAdmin, regulation.SportId, ct);
        if (authError is not null)
            return (null, authError);

        // Aktuelle Version = neueste nach ValidFrom (wie GetRegulationDetailAsync).
        var version = await db.RegulationVersions
            .Where(v => v.RegulationId == regulationId)
            .OrderByDescending(v => v.ValidFrom)
            .FirstOrDefaultAsync(ct);
        return (version, null);
    }

    private async Task<string?> AuthorizeForSportAsync(Guid actingUserId, bool isAdmin, Guid sportId, CancellationToken ct)
    {
        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Id == sportId, ct);
        if (sport is null)
            return "Sportart nicht gefunden.";
        return await AuthorizeAsync(actingUserId, isAdmin, sport.ClubId, ct);
    }

    private async Task<string?> AuthorizeAsync(Guid actingUserId, bool isAdmin, Guid? clubId, CancellationToken ct)
    {
        if (clubId is null)
            return isAdmin ? null : "Nur Admins dürfen globale Sportarten und Prüfungsordnungen bearbeiten.";

        var isClubTrainer = await db.IsClubTrainerAsync(actingUserId, clubId.Value, ct);
        return isClubTrainer ? null : "Du bist für diesen Verein nicht als Trainer eingetragen.";
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
