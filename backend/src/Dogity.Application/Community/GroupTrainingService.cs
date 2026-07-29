using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Vereins-Trainingsbibliothek (siehe docs/GROUP_TRAINING_LIBRARY.md).
/// Alles ist verein-weit geteilt und von jeder/jedem Vereinstrainer:in
/// (ClubTrainer) voll pflegbar. Bausteine (<see cref="GroupTrainingExercise"/>)
/// sind wiederverwendbare Übungen; Einheiten (<see cref="GroupTrainingUnit"/>)
/// sind geordnete Zusammenstellungen daraus.
/// </summary>
public class GroupTrainingService(IApplicationDbContext db) : IGroupTrainingService
{
    public async Task<Result<GroupTrainingLibraryDto>> GetLibraryAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<GroupTrainingLibraryDto>.Failure("Keine Trainer-Berechtigung für diesen Verein.");

        var club = await db.Clubs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result<GroupTrainingLibraryDto>.Failure("Verein nicht gefunden.");

        var exercises = await db.GroupTrainingExercises
            .Where(e => e.ClubId == clubId)
            .AsNoTracking()
            .OrderBy(e => e.Category).ThenBy(e => e.Title)
            .ToListAsync(ct);

        var units = await db.GroupTrainingUnits
            .Include(u => u.Items).ThenInclude(i => i.Exercise)
            .Where(u => u.ClubId == clubId)
            .AsNoTracking()
            .OrderBy(u => u.Category).ThenBy(u => u.Title)
            .ToListAsync(ct);

        var dto = new GroupTrainingLibraryDto(
            clubId,
            club.Name,
            exercises.Select(ToDto).ToList(),
            units.Select(ToDto).ToList());
        return Result<GroupTrainingLibraryDto>.Success(dto);
    }

    // ---- Bausteine ----

    public async Task<Result<GroupTrainingExerciseDto>> CreateExerciseAsync(Guid userId, Guid clubId, UpsertExerciseRequest request, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<GroupTrainingExerciseDto>.Failure("Keine Trainer-Berechtigung für diesen Verein.");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GroupTrainingExerciseDto>.Failure("Titel ist erforderlich.");

        var exercise = new GroupTrainingExercise
        {
            ClubId = clubId,
            Category = request.Category,
            Title = request.Title.Trim(),
            Focus = Clean(request.Focus),
            DurationMinutes = request.DurationMinutes,
            Description = Clean(request.Description),
            ExamTargets = request.ExamTargets,
            CreatedByUserId = userId
        };
        db.GroupTrainingExercises.Add(exercise);
        await db.SaveChangesAsync(ct);
        return Result<GroupTrainingExerciseDto>.Success(ToDto(exercise));
    }

    public async Task<Result<GroupTrainingExerciseDto>> UpdateExerciseAsync(Guid userId, Guid exerciseId, UpsertExerciseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GroupTrainingExerciseDto>.Failure("Titel ist erforderlich.");

        var exercise = await db.GroupTrainingExercises.FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise is null || !await IsClubTrainerAsync(userId, exercise.ClubId, ct))
            return Result<GroupTrainingExerciseDto>.Failure("Baustein nicht gefunden.");

        exercise.Category = request.Category;
        exercise.Title = request.Title.Trim();
        exercise.Focus = Clean(request.Focus);
        exercise.DurationMinutes = request.DurationMinutes;
        exercise.Description = Clean(request.Description);
        exercise.ExamTargets = request.ExamTargets;
        exercise.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<GroupTrainingExerciseDto>.Success(ToDto(exercise));
    }

    public async Task<Result> DeleteExerciseAsync(Guid userId, Guid exerciseId, CancellationToken ct = default)
    {
        var exercise = await db.GroupTrainingExercises.FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise is null || !await IsClubTrainerAsync(userId, exercise.ClubId, ct))
            return Result.Failure("Baustein nicht gefunden.");

        var now = DateTimeOffset.UtcNow;
        // Referenzen in Einheiten mit-entfernen, damit keine Einheit auf einen
        // gelöschten Baustein zeigt.
        var referencing = await db.GroupTrainingUnitItems.Where(i => i.GroupTrainingExerciseId == exerciseId).ToListAsync(ct);
        foreach (var item in referencing)
            item.DeletedAt = now;
        exercise.DeletedAt = now;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---- Einheiten ----

    public async Task<Result<GroupTrainingUnitDto>> CreateUnitAsync(Guid userId, Guid clubId, UpsertUnitRequest request, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<GroupTrainingUnitDto>.Failure("Keine Trainer-Berechtigung für diesen Verein.");
        var error = await ValidateUnitAsync(clubId, request, ct);
        if (error is not null)
            return Result<GroupTrainingUnitDto>.Failure(error);

        var unit = new GroupTrainingUnit
        {
            ClubId = clubId,
            Category = request.Category,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            CreatedByUserId = userId
        };
        db.GroupTrainingUnits.Add(unit);
        db.GroupTrainingUnitItems.AddRange(BuildItems(unit.Id, request.ExerciseIds));
        await db.SaveChangesAsync(ct);
        return Result<GroupTrainingUnitDto>.Success(await LoadUnitDtoAsync(unit.Id, ct));
    }

    public async Task<Result<GroupTrainingUnitDto>> UpdateUnitAsync(Guid userId, Guid unitId, UpsertUnitRequest request, CancellationToken ct = default)
    {
        var unit = await db.GroupTrainingUnits.Include(u => u.Items).FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null || !await IsClubTrainerAsync(userId, unit.ClubId, ct))
            return Result<GroupTrainingUnitDto>.Failure("Einheit nicht gefunden.");
        var error = await ValidateUnitAsync(unit.ClubId, request, ct);
        if (error is not null)
            return Result<GroupTrainingUnitDto>.Failure(error);

        unit.Category = request.Category;
        unit.Title = request.Title.Trim();
        unit.Description = Clean(request.Description);
        unit.UpdatedAt = DateTimeOffset.UtcNow;

        // Items komplett ersetzen; neue bewusst über das DbSet (nicht die
        // getrackte Navigation) - sonst Collection-Fixup Modified statt Added.
        db.GroupTrainingUnitItems.RemoveRange(unit.Items);
        db.GroupTrainingUnitItems.AddRange(BuildItems(unit.Id, request.ExerciseIds));
        await db.SaveChangesAsync(ct);
        return Result<GroupTrainingUnitDto>.Success(await LoadUnitDtoAsync(unit.Id, ct));
    }

    public async Task<Result> DeleteUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default)
    {
        var unit = await db.GroupTrainingUnits.Include(u => u.Items).FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null || !await IsClubTrainerAsync(userId, unit.ClubId, ct))
            return Result.Failure("Einheit nicht gefunden.");

        var now = DateTimeOffset.UtcNow;
        unit.DeletedAt = now;
        foreach (var item in unit.Items)
            item.DeletedAt = now;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<GroupTrainingUnitDto>> DuplicateUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default)
    {
        var src = await db.GroupTrainingUnits.Include(u => u.Items).AsNoTracking().FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (src is null || !await IsClubTrainerAsync(userId, src.ClubId, ct))
            return Result<GroupTrainingUnitDto>.Failure("Einheit nicht gefunden.");

        var copy = new GroupTrainingUnit
        {
            ClubId = src.ClubId,
            Category = src.Category,
            Title = $"{src.Title} (Kopie)",
            Description = src.Description,
            CreatedByUserId = userId
        };
        db.GroupTrainingUnits.Add(copy);
        var orderedExerciseIds = src.Items.OrderBy(i => i.SortOrder).Select(i => i.GroupTrainingExerciseId).ToList();
        db.GroupTrainingUnitItems.AddRange(BuildItems(copy.Id, orderedExerciseIds));
        await db.SaveChangesAsync(ct);
        return Result<GroupTrainingUnitDto>.Success(await LoadUnitDtoAsync(copy.Id, ct));
    }

    // ---- Helfer ----

    private async Task<string?> ValidateUnitAsync(Guid clubId, UpsertUnitRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return "Titel ist erforderlich.";
        if (request.ExerciseIds is null || request.ExerciseIds.Count == 0)
            return "Mindestens einen Baustein wählen.";

        var distinct = request.ExerciseIds.Distinct().ToList();
        var found = await db.GroupTrainingExercises.CountAsync(e => e.ClubId == clubId && distinct.Contains(e.Id), ct);
        if (found != distinct.Count)
            return "Mindestens ein Baustein gehört nicht zu diesem Verein.";
        return null;
    }

    private static List<GroupTrainingUnitItem> BuildItems(Guid unitId, IReadOnlyList<Guid> exerciseIds)
    {
        var order = 0;
        return exerciseIds.Select(id => new GroupTrainingUnitItem
        {
            GroupTrainingUnitId = unitId,
            GroupTrainingExerciseId = id,
            SortOrder = order++
        }).ToList();
    }

    private async Task<GroupTrainingUnitDto> LoadUnitDtoAsync(Guid unitId, CancellationToken ct)
    {
        var unit = await db.GroupTrainingUnits
            .Include(u => u.Items).ThenInclude(i => i.Exercise)
            .AsNoTracking()
            .FirstAsync(u => u.Id == unitId, ct);
        return ToDto(unit);
    }

    private Task<bool> IsClubTrainerAsync(Guid userId, Guid clubId, CancellationToken ct) =>
        db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId, ct);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static GroupTrainingExerciseDto ToDto(GroupTrainingExercise e) =>
        new(e.Id, e.ClubId, e.Category, e.Title, e.Focus, e.DurationMinutes, e.Description, e.ExamTargets);

    private static GroupTrainingUnitDto ToDto(GroupTrainingUnit u)
    {
        var items = u.Items
            .Where(i => i.Exercise != null)
            .OrderBy(i => i.SortOrder)
            .Select(i => new GroupTrainingUnitItemDto(i.Id, i.GroupTrainingExerciseId, i.SortOrder, ToDto(i.Exercise!)))
            .ToList();
        return new GroupTrainingUnitDto(
            u.Id, u.ClubId, u.Category, u.Title, u.Description,
            items.Sum(i => i.Exercise.DurationMinutes ?? 0),
            items);
    }
}
