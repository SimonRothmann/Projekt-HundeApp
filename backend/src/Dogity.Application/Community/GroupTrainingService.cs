using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Use Cases für Gruppen-Trainingseinheiten (siehe docs/GROUP_TRAINING_PLANS.md).
/// Jeder Trainer (leitet eine Gruppe oder ist Vereinstrainer) sieht die
/// vorgefertigten Vorlagen für Welpen/Junghunde, kann eigene Einheiten
/// zusammenstellen und Vorlagen in seine Gruppen kopieren + anpassen.
/// System-Vorlagen (CreatedByUserId == null) sind nie bearbeitbar.
/// </summary>
public class GroupTrainingService(IApplicationDbContext db) : IGroupTrainingService
{
    public async Task<Result<GroupTrainingLibraryDto>> GetLibraryAsync(Guid userId, CancellationToken ct = default)
    {
        if (!await IsTrainerAsync(userId, ct))
            return Result<GroupTrainingLibraryDto>.Failure("Nur Trainer haben Zugriff auf Gruppen-Trainingseinheiten.");

        var units = await db.GroupTrainingUnits
            .Include(u => u.Items)
            .Where(u => (u.CreatedByUserId == null && u.GroupId == null) || u.CreatedByUserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);

        var templates = units
            .Where(u => u.CreatedByUserId == null)
            .OrderBy(u => u.Category).ThenBy(u => u.SortOrder).ThenBy(u => u.Title)
            .Select(u => ToDto(u, u.Items, userId))
            .ToList();

        var mine = units
            .Where(u => u.CreatedByUserId == userId)
            .OrderBy(u => u.Category).ThenByDescending(u => u.CreatedAt)
            .Select(u => ToDto(u, u.Items, userId))
            .ToList();

        return Result<GroupTrainingLibraryDto>.Success(new GroupTrainingLibraryDto(templates, mine));
    }

    public async Task<Result<GroupTrainingUnitDto>> GetUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default)
    {
        var unit = await db.GroupTrainingUnits
            .Include(u => u.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unitId, ct);

        if (unit is null || !await CanViewAsync(userId, unit, ct))
            return Result<GroupTrainingUnitDto>.Failure("Trainingseinheit nicht gefunden.");

        return Result<GroupTrainingUnitDto>.Success(ToDto(unit, unit.Items, userId));
    }

    public async Task<Result<IReadOnlyList<GroupTrainingUnitDto>>> GetGroupUnitsAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        if (!await IsGroupTrainerAsync(userId, groupId, ct))
            return Result<IReadOnlyList<GroupTrainingUnitDto>>.Failure("Gruppe nicht gefunden.");

        var units = await db.GroupTrainingUnits
            .Include(u => u.Items)
            .Where(u => u.GroupId == groupId)
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

        return Result<IReadOnlyList<GroupTrainingUnitDto>>.Success(units.Select(u => ToDto(u, u.Items, userId)).ToList());
    }

    public async Task<Result<GroupTrainingUnitDto>> CreateUnitAsync(Guid userId, CreateGroupTrainingUnitRequest request, CancellationToken ct = default)
    {
        if (!await IsTrainerAsync(userId, ct))
            return Result<GroupTrainingUnitDto>.Failure("Nur Trainer können Trainingseinheiten anlegen.");

        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GroupTrainingUnitDto>.Failure("Titel ist erforderlich.");

        if (request.GroupId is { } groupId && !await IsGroupTrainerAsync(userId, groupId, ct))
            return Result<GroupTrainingUnitDto>.Failure("Du leitest diese Gruppe nicht.");

        var unit = new GroupTrainingUnit
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Category = request.Category,
            CreatedByUserId = userId,
            GroupId = request.GroupId
        };
        db.GroupTrainingUnits.Add(unit);
        var items = BuildItems(unit.Id, request.Items);
        db.GroupTrainingUnitItems.AddRange(items);
        await db.SaveChangesAsync(ct);

        return Result<GroupTrainingUnitDto>.Success(ToDto(unit, items, userId));
    }

    public async Task<Result<GroupTrainingUnitDto>> UpdateUnitAsync(Guid userId, Guid unitId, UpdateGroupTrainingUnitRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<GroupTrainingUnitDto>.Failure("Titel ist erforderlich.");

        var unit = await db.GroupTrainingUnits
            .Include(u => u.Items)
            .FirstOrDefaultAsync(u => u.Id == unitId && u.CreatedByUserId == userId, ct);

        if (unit is null)
            return Result<GroupTrainingUnitDto>.Failure("Trainingseinheit nicht gefunden oder nicht bearbeitbar.");

        unit.Title = request.Title.Trim();
        unit.Description = request.Description;
        unit.Category = request.Category;
        unit.UpdatedAt = DateTimeOffset.UtcNow;

        // Items werden komplett ersetzt (reine Besitzkinder ohne Fremdbezüge).
        // Neue Items bewusst über das DbSet (nicht die getrackte Navigation)
        // hinzufügen - sonst würde die Collection-Fixup sie als Modified statt
        // Added markieren (dokumentierter EF-Fallstrick, siehe GoalService).
        db.GroupTrainingUnitItems.RemoveRange(unit.Items);
        var items = BuildItems(unit.Id, request.Items);
        db.GroupTrainingUnitItems.AddRange(items);

        await db.SaveChangesAsync(ct);

        return Result<GroupTrainingUnitDto>.Success(ToDto(unit, items, userId));
    }

    public async Task<Result> DeleteUnitAsync(Guid userId, Guid unitId, CancellationToken ct = default)
    {
        var unit = await db.GroupTrainingUnits
            .Include(u => u.Items)
            .FirstOrDefaultAsync(u => u.Id == unitId && u.CreatedByUserId == userId, ct);

        if (unit is null)
            return Result.Failure("Trainingseinheit nicht gefunden oder nicht löschbar.");

        var now = DateTimeOffset.UtcNow;
        unit.DeletedAt = now;
        foreach (var item in unit.Items)
            item.DeletedAt = now;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<GroupTrainingUnitDto>> CopyUnitToGroupAsync(Guid userId, Guid unitId, Guid groupId, CancellationToken ct = default)
    {
        var source = await db.GroupTrainingUnits
            .Include(u => u.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == unitId, ct);

        // Kopiert werden dürfen System-Vorlagen und eigene Einheiten.
        if (source is null || (source.CreatedByUserId != null && source.CreatedByUserId != userId))
            return Result<GroupTrainingUnitDto>.Failure("Trainingseinheit nicht gefunden.");

        if (!await IsGroupTrainerAsync(userId, groupId, ct))
            return Result<GroupTrainingUnitDto>.Failure("Du leitest diese Gruppe nicht.");

        var copy = new GroupTrainingUnit
        {
            Title = source.Title,
            Description = source.Description,
            Category = source.Category,
            CreatedByUserId = userId,
            GroupId = groupId
        };
        db.GroupTrainingUnits.Add(copy);
        var items = BuildItems(copy.Id, source.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => new GroupTrainingItemInput(i.Title, i.Description, i.Focus, i.DurationMinutes))
            .ToList());
        db.GroupTrainingUnitItems.AddRange(items);
        await db.SaveChangesAsync(ct);

        return Result<GroupTrainingUnitDto>.Success(ToDto(copy, items, userId));
    }

    private static List<GroupTrainingUnitItem> BuildItems(Guid unitId, IReadOnlyList<GroupTrainingItemInput>? inputs)
    {
        var result = new List<GroupTrainingUnitItem>();
        if (inputs is null) return result;
        var order = 0;
        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input.Title)) continue;
            result.Add(new GroupTrainingUnitItem
            {
                GroupTrainingUnitId = unitId,
                Title = input.Title.Trim(),
                Description = input.Description,
                Focus = string.IsNullOrWhiteSpace(input.Focus) ? null : input.Focus.Trim(),
                DurationMinutes = input.DurationMinutes,
                SortOrder = order++
            });
        }
        return result;
    }

    private async Task<bool> CanViewAsync(Guid userId, GroupTrainingUnit unit, CancellationToken ct)
    {
        if (unit.CreatedByUserId == null || unit.CreatedByUserId == userId)
            return await IsTrainerAsync(userId, ct);
        if (unit.GroupId is { } groupId)
            return await IsGroupTrainerAsync(userId, groupId, ct);
        return false;
    }

    // "Trainer-Sein" datengetrieben (analog GroupService.IsTrainerAsync): wer
    // eine Gruppe leitet oder Vereinstrainer ist.
    private async Task<bool> IsTrainerAsync(Guid userId, CancellationToken ct) =>
        await db.Groups.AnyAsync(g => g.TrainerId == userId, ct)
        || await db.ClubTrainers.AnyAsync(t => t.UserId == userId, ct);

    private async Task<bool> IsGroupTrainerAsync(Guid userId, Guid groupId, CancellationToken ct) =>
        await db.Groups.AnyAsync(g => g.Id == groupId && g.TrainerId == userId, ct);

    private static GroupTrainingUnitDto ToDto(GroupTrainingUnit unit, IEnumerable<GroupTrainingUnitItem> source, Guid userId)
    {
        var items = source
            .OrderBy(i => i.SortOrder)
            .Select(i => new GroupTrainingItemDto(i.Id, i.Title, i.Description, i.Focus, i.DurationMinutes, i.SortOrder))
            .ToList();

        return new GroupTrainingUnitDto(
            unit.Id,
            unit.Title,
            unit.Description,
            unit.Category,
            unit.GroupId,
            IsTemplate: unit.CreatedByUserId == null,
            IsMine: unit.CreatedByUserId == userId,
            TotalMinutes: items.Sum(i => i.DurationMinutes ?? 0),
            items);
    }
}
