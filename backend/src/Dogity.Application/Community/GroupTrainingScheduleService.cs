using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Terminplanung fürs Gruppentraining (siehe docs/GROUP_TRAINING_SCHEDULE.md).
/// ClubTrainer planen/bearbeiten Termine des Vereins; Mitglieder sehen die
/// Termine ihrer Gruppen read-only. Inhalt = geordnete Bausteine und/oder
/// Freitext; mehrere zuständige Trainer:innen je Termin.
/// </summary>
public class GroupTrainingScheduleService(IApplicationDbContext db, IUserLookupService userLookup) : IGroupTrainingScheduleService
{
    public async Task<Result<IReadOnlyList<GroupTrainingSessionDto>>> GetClubScheduleAsync(
        Guid userId, Guid clubId, DateOnly from, DateOnly? to, Guid? groupId, GroupTrainingCategory? category, bool mineOnly, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<IReadOnlyList<GroupTrainingSessionDto>>.Failure("Keine Trainer-Berechtigung für diesen Verein.");

        var fromTs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var query = LoadSessionsQuery().Where(s => s.ClubId == clubId && s.StartsAt >= fromTs);
        if (to is { } toDate)
        {
            var toTs = new DateTimeOffset(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(s => s.StartsAt < toTs);
        }
        if (groupId is { } gid) query = query.Where(s => s.GroupId == gid);
        if (category is { } cat) query = query.Where(s => s.Category == cat);
        if (mineOnly) query = query.Where(s => s.Trainers.Any(t => t.UserId == userId));

        var sessions = await query.OrderBy(s => s.StartsAt).AsNoTracking().ToListAsync(ct);
        return Result<IReadOnlyList<GroupTrainingSessionDto>>.Success(await MapAsync(sessions, ct));
    }

    public async Task<Result<IReadOnlyList<GroupTrainingSessionDto>>> GetMemberScheduleAsync(Guid userId, DateOnly from, CancellationToken ct = default)
    {
        var fromTs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var groupIds = await db.GroupMembers
            .Where(m => m.UserId == userId && m.Status == GroupMemberStatus.Active)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        if (groupIds.Count == 0)
            return Result<IReadOnlyList<GroupTrainingSessionDto>>.Success(Array.Empty<GroupTrainingSessionDto>());

        var sessions = await LoadSessionsQuery()
            .Where(s => groupIds.Contains(s.GroupId) && s.StartsAt >= fromTs)
            .OrderBy(s => s.StartsAt)
            .AsNoTracking()
            .ToListAsync(ct);
        return Result<IReadOnlyList<GroupTrainingSessionDto>>.Success(await MapAsync(sessions, ct));
    }

    public async Task<Result<GroupTrainingSessionDto>> CreateSessionAsync(Guid userId, Guid clubId, CreateSessionRequest request, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<GroupTrainingSessionDto>.Failure("Keine Trainer-Berechtigung für diesen Verein.");
        var error = await ValidateAsync(clubId, request.GroupId, request.TrainerUserIds, request.Items, ct);
        if (error is not null) return Result<GroupTrainingSessionDto>.Failure(error);

        var session = new GroupTrainingSession
        {
            ClubId = clubId,
            GroupId = request.GroupId,
            Category = request.Category,
            StartsAt = request.StartsAt,
            DurationMinutes = request.DurationMinutes < 1 ? 60 : request.DurationMinutes,
            Location = Clean(request.Location),
            Notes = Clean(request.Notes),
            Status = GroupTrainingSessionStatus.Planned,
            CreatedByUserId = userId
        };
        db.GroupTrainingSessions.Add(session);
        db.GroupTrainingSessionItems.AddRange(BuildItems(session.Id, request.Items));
        db.GroupTrainingSessionTrainers.AddRange(BuildTrainers(session.Id, request.TrainerUserIds));
        await db.SaveChangesAsync(ct);
        return Result<GroupTrainingSessionDto>.Success(await LoadDtoAsync(session.Id, ct));
    }

    public async Task<Result<GroupTrainingSessionDto>> UpdateSessionAsync(Guid userId, Guid sessionId, UpdateSessionRequest request, CancellationToken ct = default)
    {
        var session = await db.GroupTrainingSessions
            .Include(s => s.Items).Include(s => s.Trainers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !await IsClubTrainerAsync(userId, session.ClubId, ct))
            return Result<GroupTrainingSessionDto>.Failure("Termin nicht gefunden.");
        var error = await ValidateAsync(session.ClubId, session.GroupId, request.TrainerUserIds, request.Items, ct);
        if (error is not null) return Result<GroupTrainingSessionDto>.Failure(error);

        session.Category = request.Category;
        session.StartsAt = request.StartsAt;
        session.DurationMinutes = request.DurationMinutes < 1 ? 60 : request.DurationMinutes;
        session.Location = Clean(request.Location);
        session.Notes = Clean(request.Notes);
        session.UpdatedAt = DateTimeOffset.UtcNow;

        db.GroupTrainingSessionItems.RemoveRange(session.Items);
        db.GroupTrainingSessionTrainers.RemoveRange(session.Trainers);
        db.GroupTrainingSessionItems.AddRange(BuildItems(session.Id, request.Items));
        db.GroupTrainingSessionTrainers.AddRange(BuildTrainers(session.Id, request.TrainerUserIds));
        await db.SaveChangesAsync(ct);
        return Result<GroupTrainingSessionDto>.Success(await LoadDtoAsync(session.Id, ct));
    }

    public async Task<Result> CancelSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.GroupTrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !await IsClubTrainerAsync(userId, session.ClubId, ct))
            return Result.Failure("Termin nicht gefunden.");
        session.Status = GroupTrainingSessionStatus.Cancelled;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.GroupTrainingSessions
            .Include(s => s.Items).Include(s => s.Trainers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !await IsClubTrainerAsync(userId, session.ClubId, ct))
            return Result.Failure("Termin nicht gefunden.");
        var now = DateTimeOffset.UtcNow;
        session.DeletedAt = now;
        foreach (var i in session.Items) i.DeletedAt = now;
        foreach (var t in session.Trainers) t.DeletedAt = now;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupTrainingSessionDto>>> GenerateSeriesAsync(Guid userId, Guid clubId, GenerateSeriesRequest request, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<IReadOnlyList<GroupTrainingSessionDto>>.Failure("Keine Trainer-Berechtigung für diesen Verein.");
        if (request.Starts is null || request.Starts.Count == 0)
            return Result<IReadOnlyList<GroupTrainingSessionDto>>.Failure("Keine Termine im Zeitraum.");
        var error = await ValidateAsync(clubId, request.GroupId, request.TrainerUserIds, request.Items, ct);
        if (error is not null) return Result<IReadOnlyList<GroupTrainingSessionDto>>.Failure(error);

        var duration = request.DurationMinutes < 1 ? 60 : request.DurationMinutes;
        var createdIds = new List<Guid>();
        foreach (var start in request.Starts.OrderBy(s => s))
        {
            var session = new GroupTrainingSession
            {
                ClubId = clubId,
                GroupId = request.GroupId,
                Category = request.Category,
                StartsAt = start,
                DurationMinutes = duration,
                Location = Clean(request.Location),
                Status = GroupTrainingSessionStatus.Planned,
                CreatedByUserId = userId
            };
            db.GroupTrainingSessions.Add(session);
            db.GroupTrainingSessionItems.AddRange(BuildItems(session.Id, request.Items));
            db.GroupTrainingSessionTrainers.AddRange(BuildTrainers(session.Id, request.TrainerUserIds));
            createdIds.Add(session.Id);
        }
        await db.SaveChangesAsync(ct);

        var sessions = await LoadSessionsQuery().Where(s => createdIds.Contains(s.Id)).OrderBy(s => s.StartsAt).AsNoTracking().ToListAsync(ct);
        return Result<IReadOnlyList<GroupTrainingSessionDto>>.Success(await MapAsync(sessions, ct));
    }

    public async Task<Result<IReadOnlyList<GroupTrainingExerciseDto>>> GenerateContentAsync(Guid userId, Guid clubId, GroupTrainingCategory category, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<IReadOnlyList<GroupTrainingExerciseDto>>.Failure("Keine Trainer-Berechtigung für diesen Verein.");

        var pool = await db.GroupTrainingExercises
            .Where(e => e.ClubId == clubId && e.Category == category)
            .AsNoTracking()
            .ToListAsync(ct);

        var picked = GroupTrainingMixGenerator.Generate(category, pool, Random.Shared);
        return Result<IReadOnlyList<GroupTrainingExerciseDto>>.Success(picked.Select(ToExerciseDto).ToList());
    }

    // ---- Helfer ----

    private async Task<string?> ValidateAsync(Guid clubId, Guid groupId, IReadOnlyList<Guid> trainerIds, IReadOnlyList<SessionContentInput> items, CancellationToken ct)
    {
        var groupOk = await db.Groups.AnyAsync(g => g.Id == groupId && g.ClubId == clubId, ct);
        if (!groupOk) return "Gruppe gehört nicht zu diesem Verein.";

        if (trainerIds is { Count: > 0 })
        {
            var distinct = trainerIds.Distinct().ToList();
            var count = await db.ClubTrainers.CountAsync(t => t.ClubId == clubId && distinct.Contains(t.UserId), ct);
            if (count != distinct.Count) return "Mindestens eine zugewiesene Trainer:in ist nicht Trainer:in dieses Vereins.";
        }

        var exerciseIds = new List<Guid>();
        foreach (var item in items ?? Array.Empty<SessionContentInput>())
        {
            var hasEx = item.ExerciseId is not null;
            var hasText = !string.IsNullOrWhiteSpace(item.FreeText);
            if (hasEx == hasText) return "Jede Inhaltsposition braucht entweder einen Baustein ODER einen Freitext.";
            if (hasEx) exerciseIds.Add(item.ExerciseId!.Value);
        }
        if (exerciseIds.Count > 0)
        {
            var distinct = exerciseIds.Distinct().ToList();
            var count = await db.GroupTrainingExercises.CountAsync(e => e.ClubId == clubId && distinct.Contains(e.Id), ct);
            if (count != distinct.Count) return "Mindestens ein Baustein gehört nicht zu diesem Verein.";
        }
        return null;
    }

    private static List<GroupTrainingSessionItem> BuildItems(Guid sessionId, IReadOnlyList<SessionContentInput>? items)
    {
        var result = new List<GroupTrainingSessionItem>();
        if (items is null) return result;
        var order = 0;
        foreach (var input in items)
        {
            var hasEx = input.ExerciseId is not null;
            var hasText = !string.IsNullOrWhiteSpace(input.FreeText);
            if (hasEx == hasText) continue; // Sicherheitsnetz (Validierung greift vorher)
            result.Add(new GroupTrainingSessionItem
            {
                GroupTrainingSessionId = sessionId,
                GroupTrainingExerciseId = input.ExerciseId,
                FreeText = hasText ? input.FreeText!.Trim() : null,
                SortOrder = order++
            });
        }
        return result;
    }

    private static List<GroupTrainingSessionTrainer> BuildTrainers(Guid sessionId, IReadOnlyList<Guid>? trainerIds) =>
        (trainerIds ?? Array.Empty<Guid>())
        .Distinct()
        .Select(id => new GroupTrainingSessionTrainer { GroupTrainingSessionId = sessionId, UserId = id })
        .ToList();

    private IQueryable<GroupTrainingSession> LoadSessionsQuery() =>
        db.GroupTrainingSessions
            .Include(s => s.Group)
            .Include(s => s.Items).ThenInclude(i => i.Exercise)
            .Include(s => s.Trainers);

    private async Task<GroupTrainingSessionDto> LoadDtoAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await LoadSessionsQuery().AsNoTracking().FirstAsync(s => s.Id == sessionId, ct);
        return (await MapAsync([session], ct))[0];
    }

    private async Task<IReadOnlyList<GroupTrainingSessionDto>> MapAsync(List<GroupTrainingSession> sessions, CancellationToken ct)
    {
        var trainerIds = sessions.SelectMany(s => s.Trainers.Select(t => t.UserId)).Distinct().ToList();
        IReadOnlyDictionary<Guid, UserLookupResult> names = trainerIds.Count == 0
            ? new Dictionary<Guid, UserLookupResult>()
            : await userLookup.FindByIdsAsync(trainerIds, ct);

        return sessions.Select(s => ToDto(s, names)).ToList();
    }

    private static GroupTrainingSessionDto ToDto(GroupTrainingSession s, IReadOnlyDictionary<Guid, UserLookupResult> names)
    {
        var items = s.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => new SessionItemDto(
                i.Id, i.GroupTrainingExerciseId, i.FreeText, i.SortOrder,
                i.Exercise is null ? null : ToExerciseDto(i.Exercise)))
            .ToList();

        var trainers = s.Trainers
            .Select(t => names.TryGetValue(t.UserId, out var n)
                ? new SessionTrainerDto(t.UserId, n.FirstName, n.LastName)
                : new SessionTrainerDto(t.UserId, "", ""))
            .ToList();

        return new GroupTrainingSessionDto(
            s.Id, s.ClubId, s.GroupId, s.Group?.Name ?? "", s.Category,
            s.StartsAt, s.DurationMinutes, s.Location, s.Notes, s.Status,
            items.Sum(i => i.Exercise?.DurationMinutes ?? 0),
            items, trainers);
    }

    private static GroupTrainingExerciseDto ToExerciseDto(GroupTrainingExercise e) =>
        new(e.Id, e.ClubId, e.Category, e.Title, e.Focus, e.DurationMinutes, e.Description, e.ExamTargets);

    private Task<bool> IsClubTrainerAsync(Guid userId, Guid clubId, CancellationToken ct) =>
        db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId, ct);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
