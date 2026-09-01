using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Use Cases für die Trainer-Übersicht (siehe FEATURE_MODULE.md "Community":
/// Gruppen, Trainer). Ein Trainer legt Gruppen an, verwaltet Mitglieder und
/// kann sich per <see cref="TrainerAssignment"/> als Trainer für den Hund
/// eines Mitglieds eintragen - das gewährt anschließend Zugriff auf
/// Training/Ziele dieses Hundes (siehe <see cref="DogAccessQueries"/>).
/// </summary>
public class GroupService(IApplicationDbContext db, IUserLookupService userLookup, ITrainerRoleService trainerRoles, INotificationService notifications) : IGroupService
{
    public async Task<Result<IReadOnlyList<GroupDto>>> GetMyGroupsAsync(Guid trainerId, CancellationToken ct = default)
    {
        // Sichtbar sind eigene Gruppen, Gruppen, in denen man als weitere:r
        // Trainer:in mit-betreut, UND alle Gruppen der Vereine, in denen man
        // Trainer:in ist - so kann jede:r Vereinstrainer:in die Gruppen des
        // Vereins sehen, bearbeiten und einer/m Trainer:in zuweisen.
        var clubIds = await db.ClubTrainers
            .Where(t => t.UserId == trainerId)
            .Select(t => t.ClubId)
            .ToListAsync(ct);

        var rows = await db.Groups
            .Where(g => g.TrainerId == trainerId
                || g.Trainers.Any(t => t.UserId == trainerId)
                || (g.ClubId != null && clubIds.Contains(g.ClubId.Value)))
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.TrainerId,
                g.ClubId,
                MemberCount = g.Members.Count(m => m.Status == GroupMemberStatus.Active)
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var trainerLookup = await userLookup.FindByIdsAsync(rows.Select(r => r.TrainerId).Distinct().ToList(), ct);
        var groups = rows
            .Select(r => new GroupDto(r.Id, r.Name, r.Description, r.TrainerId, r.ClubId, r.MemberCount, TrainerDisplayName(trainerLookup, r.TrainerId)))
            .ToList();

        return Result<IReadOnlyList<GroupDto>>.Success(groups);
    }

    private static string? TrainerDisplayName(IReadOnlyDictionary<Guid, UserLookupResult> lookup, Guid trainerId) =>
        lookup.TryGetValue(trainerId, out var info) ? $"{info.FirstName} {info.LastName}".Trim() : null;

    private static GroupTrainerDto ToTrainerDto(IReadOnlyDictionary<Guid, UserLookupResult> lookup, Guid userId, bool isLead) =>
        lookup.TryGetValue(userId, out var info)
            ? new GroupTrainerDto(userId, info.Email, info.FirstName, info.LastName, isLead)
            : new GroupTrainerDto(userId, "(unbekannt)", "", "", isLead);

    /// <summary>
    /// "Trainer-Sein" ist bewusst rein datengetrieben (siehe TODO.md
    /// "Rollenswitch"): wer eine Gruppe leitet, eine Gruppe mit-betreut oder
    /// als Trainer:in einem Verein zugewiesen ist, bekommt die
    /// Trainer-Perspektive im Frontend.
    ///
    /// Dieselbe Abfrage bestimmt auch die Identity-Rolle TRAINER
    /// (siehe <see cref="ITrainerRoleService"/>) - deshalb steht sie an einer
    /// gemeinsamen Stelle und nicht zweimal hier.
    /// </summary>
    public Task<bool> IsTrainerAsync(Guid userId, CancellationToken ct = default) =>
        db.IsAnyTrainerAsync(userId, ct);

    public async Task<Result<GroupDetailDto>> GetDetailAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await GetAccessibleGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result<GroupDetailDto>.NotFound("Gruppe nicht gefunden.");

        var coTrainerIds = group.Trainers.Select(t => t.UserId).ToList();
        var lookupIds = group.Members.Select(m => m.UserId)
            .Append(group.TrainerId)
            .Concat(coTrainerIds)
            .ToList();
        var memberLookup = await userLookup.FindByIdsAsync(lookupIds, ct);
        var members = group.Members
            .Select(m => memberLookup.TryGetValue(m.UserId, out var info)
                ? new GroupMemberDto(m.UserId, info.Email, info.FirstName, info.LastName, m.Role, m.JoinedAt)
                : new GroupMemberDto(m.UserId, "(unbekannt)", "", "", m.Role, m.JoinedAt))
            .ToList();

        // Hauptverantwortliche:r zuerst, danach die weiteren Trainer:innen
        // alphabetisch - die Liste steht so in der Oberfläche.
        var trainers = new List<GroupTrainerDto> { ToTrainerDto(memberLookup, group.TrainerId, isLead: true) };
        trainers.AddRange(coTrainerIds
            .Where(id => id != group.TrainerId)
            .Select(id => ToTrainerDto(memberLookup, id, isLead: false))
            .OrderBy(t => t.FirstName)
            .ThenBy(t => t.LastName));

        var dto = new GroupDetailDto(
            new GroupDto(group.Id, group.Name, group.Description, group.TrainerId, group.ClubId, members.Count, TrainerDisplayName(memberLookup, group.TrainerId)),
            members,
            trainers);
        return Result<GroupDetailDto>.Success(dto);
    }

    public async Task<Result<GroupDto>> CreateAsync(Guid trainerId, CreateGroupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<GroupDto>.Failure("Name ist erforderlich.");

        if (request.ClubId is { } clubId)
        {
            var isClubTrainer = await db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == trainerId, ct);
            if (!isClubTrainer)
                return Result<GroupDto>.Failure("Du bist für diesen Verein nicht als Trainer eingetragen.");
        }

        var group = new Group
        {
            TrainerId = trainerId,
            Name = request.Name.Trim(),
            Description = request.Description,
            ClubId = request.ClubId
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
        await trainerRoles.SyncAsync(trainerId, ct);

        return Result<GroupDto>.Success(new GroupDto(group.Id, group.Name, group.Description, group.TrainerId, group.ClubId, 0));
    }

    public async Task<Result<GroupDto>> UpdateGroupAsync(Guid userId, Guid groupId, UpdateGroupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<GroupDto>.Failure("Name ist erforderlich.");

        var group = await GetManageableGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result<GroupDto>.NotFound("Gruppe nicht gefunden.");

        group.Name = request.Name.Trim();
        group.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await db.SaveChangesAsync(ct);

        var memberCount = await db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.Status == GroupMemberStatus.Active, ct);
        var trainerLookup = await userLookup.FindByIdsAsync(new[] { group.TrainerId }, ct);
        return Result<GroupDto>.Success(new GroupDto(group.Id, group.Name, group.Description, group.TrainerId, group.ClubId, memberCount, TrainerDisplayName(trainerLookup, group.TrainerId)));
    }

    /// <summary>
    /// Gruppe auflösen. Bis hierher ließ sich eine Gruppe nur ANLEGEN - eine
    /// versehentlich erstellte blieb dem Verein für immer erhalten, samt
    /// "Beitreten"-Knopf für alle Mitglieder.
    ///
    /// Mitgliedschaften und Mit-Betreuungen werden mit gelöst. Die
    /// Trainingsdaten der Hunde bleiben unangetastet - sie hängen am Hund,
    /// nicht an der Gruppe. Bestehende Betreuungen einzelner Hunde
    /// (TrainerAssignment) bleiben ebenfalls: Sie überdauern einen
    /// Gruppenwechsel bewusst und lassen sich einzeln beenden.
    /// </summary>
    public async Task<Result> DeleteGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        var plannedSessions = await db.GroupTrainingSessions
            .CountAsync(s => s.GroupId == groupId && s.Status == GroupTrainingSessionStatus.Planned && s.StartsAt > DateTimeOffset.UtcNow, ct);
        if (plannedSessions > 0)
            return Result.Failure($"Für diese Gruppe stehen noch {plannedSessions} Termine im Kalender. Bitte zuerst absagen oder löschen.");

        var now = DateTimeOffset.UtcNow;
        var members = await db.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync(ct);
        foreach (var m in members) m.DeletedAt = now;

        var coTrainers = await db.GroupTrainers.Where(t => t.GroupId == groupId).ToListAsync(ct);
        foreach (var t in coTrainers) t.DeletedAt = now;

        group.DeletedAt = now;
        await db.SaveChangesAsync(ct);

        // Wer nur diese eine Gruppe geleitet hat, ist jetzt nirgends mehr
        // Trainer:in und verliert das Kennzeichen.
        await trainerRoles.SyncAsync(coTrainers.Select(t => t.UserId).Append(group.TrainerId), ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupTrainerOptionDto>>> GetAssignableTrainersAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result<IReadOnlyList<GroupTrainerOptionDto>>.NotFound("Gruppe nicht gefunden.");

        // Ohne Verein gibt es keinen Trainer-Pool - nur der/die aktuelle Trainer:in.
        if (group.ClubId is not { } clubId)
            return Result<IReadOnlyList<GroupTrainerOptionDto>>.Success(Array.Empty<GroupTrainerOptionDto>());

        var trainerIds = await db.ClubTrainers
            .Where(t => t.ClubId == clubId)
            .Select(t => t.UserId)
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(trainerIds, ct);
        var dtos = trainerIds
            .Select(id => lookup.TryGetValue(id, out var info)
                ? new GroupTrainerOptionDto(id, info.FirstName, info.LastName, info.Email)
                : new GroupTrainerOptionDto(id, "", "", "(unbekannt)"))
            .OrderBy(t => t.FirstName)
            .ThenBy(t => t.LastName)
            .ToList();

        return Result<IReadOnlyList<GroupTrainerOptionDto>>.Success(dtos);
    }

    public async Task<Result> AssignGroupTrainerAsync(Guid userId, Guid groupId, AssignGroupTrainerRequest request, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        if (group.ClubId is not { } clubId)
            return Result.Failure("Nur Vereinsgruppen können einer/m anderen Trainer:in zugewiesen werden.");

        var isClubTrainer = await db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == request.TrainerId, ct);
        if (!isClubTrainer)
            return Result.Failure("Die/der gewählte Trainer:in gehört nicht zu diesem Verein.");

        var previousTrainerId = group.TrainerId;
        group.TrainerId = request.TrainerId;
        await db.SaveChangesAsync(ct);
        // Beide Seiten abgleichen: die/der bisherige Hauptverantwortliche kann
        // dadurch das Trainer-Kennzeichen verlieren, die/der neue es bekommen.
        await trainerRoles.SyncAsync([previousTrainerId, request.TrainerId], ct);
        return Result.Success();
    }

    public async Task<Result> AddGroupTrainerAsync(Guid userId, Guid groupId, AddGroupTrainerRequest request, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result.Failure("E-Mail-Adresse ist erforderlich.");

        var user = await userLookup.FindByEmailAsync(request.Email.Trim(), ct);
        if (user is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        if (user.UserId == group.TrainerId)
            return Result.Failure("Diese Person ist bereits hauptverantwortlich für diese Gruppe.");

        // Auch weichgelöschte Zeilen ansehen: sonst scheitert das erneute
        // Hinzufügen einer zuvor entfernten Trainer:in am Unique-Index.
        var existing = await db.GroupTrainers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.GroupId == groupId && t.UserId == user.UserId, ct);

        if (existing is not null)
        {
            if (existing.DeletedAt is null)
                return Result.Failure("Diese Person ist bereits Trainer:in dieser Gruppe.");
            existing.DeletedAt = null;
        }
        else
        {
            db.GroupTrainers.Add(new GroupTrainer { GroupId = groupId, UserId = user.UserId });
        }

        await db.SaveChangesAsync(ct);
        await trainerRoles.SyncAsync(user.UserId, ct);
        await notifications.CreateAsync(user.UserId, $"Du bist jetzt Trainer:in der Gruppe \"{group.Name}\".", $"/trainer/{groupId}", ct);
        return Result.Success();
    }

    public async Task<Result> RemoveGroupTrainerAsync(Guid userId, Guid groupId, Guid trainerUserId, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        if (trainerUserId == group.TrainerId)
            return Result.Failure("Die/der Hauptverantwortliche kann nicht entfernt werden - erst eine andere Person zuweisen.");

        var entry = await db.GroupTrainers.FirstOrDefaultAsync(t => t.GroupId == groupId && t.UserId == trainerUserId, ct);
        if (entry is null)
            return Result.NotFound("Trainer-Zuordnung nicht gefunden.");

        entry.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await trainerRoles.SyncAsync(trainerUserId, ct);
        return Result.Success();
    }

    public async Task<Result> AddMemberAsync(Guid trainerId, Guid groupId, AddMemberRequest request, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(trainerId, groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        var user = await userLookup.FindByEmailAsync(request.Email, ct);
        if (user is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index, siehe
        // SoftDeleteRevival) - sonst scheitert das erneute Aufnehmen eines
        // zuvor entfernten Mitglieds mit einem 500er.
        var (existing, isActive) = await db.GroupMembers
            .FindIncludingRemovedAsync(m => m.GroupId == groupId && m.UserId == user.UserId, ct);
        if (isActive)
            return Result.Failure("Dieser Benutzer ist bereits Mitglied der Gruppe.");

        if (existing is not null)
        {
            existing.DeletedAt = null;
            // Wiederaufnahme durch die Trainer:in - keine erneute Freigabe nötig.
            existing.Status = GroupMemberStatus.Active;
            existing.JoinedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = user.UserId });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid trainerId, Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(trainerId, groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        var member = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberId, ct);
        if (member is null)
            return Result.NotFound("Mitglied nicht gefunden.");

        member.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<MemberDogDto>>> GetMemberDogsAsync(Guid trainerId, Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        var isMember = await IsGroupMemberAsync(trainerId, groupId, memberId, ct);
        if (!isMember)
            return Result<IReadOnlyList<MemberDogDto>>.Failure("Mitglied nicht in dieser Gruppe gefunden.");

        var dogs = await db.DogOwners
            .Where(o => o.UserId == memberId)
            .Select(o => o.Dog!)
            .Select(d => new MemberDogDto(
                d.Id,
                d.Name,
                d.Breed,
                db.TrainerAssignments.Any(t => t.TrainerId == trainerId && t.DogId == d.Id)))
            .ToListAsync(ct);

        return Result<IReadOnlyList<MemberDogDto>>.Success(dogs);
    }

    public async Task<Result> AssignTrainerToDogAsync(Guid trainerId, Guid groupId, AssignTrainerRequest request, CancellationToken ct = default)
    {
        var isMember = await IsGroupMemberAsync(trainerId, groupId, request.MemberId, ct);
        if (!isMember)
            return Result.Failure("Mitglied nicht in dieser Gruppe gefunden.");

        var ownsDog = await db.DogOwners.AnyAsync(o => o.DogId == request.DogId && o.UserId == request.MemberId, ct);
        if (!ownsDog)
            return Result.Failure("Hund gehört nicht zu diesem Mitglied.");

        // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index) -
        // sonst ließe sich eine beendete Betreuung nie wieder aufnehmen.
        var (existing, isActive) = await db.TrainerAssignments
            .FindIncludingRemovedAsync(t => t.TrainerId == trainerId && t.DogId == request.DogId, ct);
        if (isActive)
            return Result.Failure("Du betreust diesen Hund bereits.");

        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.MemberId = request.MemberId;
            existing.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else
        {
            db.TrainerAssignments.Add(new TrainerAssignment
            {
                TrainerId = trainerId,
                MemberId = request.MemberId,
                DogId = request.DogId,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Betreuung eines Hundes wieder beenden. Bis hierher ließ sich eine
    /// <see cref="TrainerAssignment"/> nur ANLEGEN - einmal betreut, behielt
    /// eine Trainer:in dauerhaft Zugriff auf Tagebuch, Ziele und Trainingsplan
    /// des Hundes, auch nach einem Gruppen- oder Vereinswechsel.
    ///
    /// Beenden darf es die Trainer:in selbst und jede:r, die die Gruppe
    /// verwaltet (Hauptverantwortliche, weitere Trainer:innen, Vereinstrainer:innen).
    /// </summary>
    public async Task<Result> RemoveTrainerFromDogAsync(Guid userId, Guid groupId, Guid trainerUserId, Guid dogId, CancellationToken ct = default)
    {
        var canManage = await GetManageableGroupAsync(userId, groupId, ct) is not null;
        if (!canManage && userId != trainerUserId)
            return Result.NotFound("Gruppe nicht gefunden.");

        var assignment = await db.TrainerAssignments
            .FirstOrDefaultAsync(t => t.TrainerId == trainerUserId && t.DogId == dogId, ct);
        if (assignment is null)
            return Result.NotFound("Betreuung nicht gefunden.");

        assignment.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupDto>>> GetGroupsByClubAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        var isClubMember = await db.ClubMemberships.AnyAsync(
            m => m.ClubId == clubId && m.UserId == userId && m.Status == ClubMembershipStatus.Approved, ct);
        var isClubTrainer = await db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId, ct);

        if (!isClubMember && !isClubTrainer)
            return Result<IReadOnlyList<GroupDto>>.NotFound("Keine Berechtigung für diesen Verein.");

        var rows = await db.Groups
            .Where(g => g.ClubId == clubId)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.TrainerId,
                g.ClubId,
                MemberCount = g.Members.Count(m => m.Status == GroupMemberStatus.Active),
                // Verhältnis der aufrufenden Person - sonst bietet die
                // Vereinsseite auch der Trainer:in einen Beitritt an.
                IsCoTrainer = g.Trainers.Any(t => t.UserId == userId),
                MyMembership = g.Members
                    .Where(m => m.UserId == userId)
                    .Select(m => (GroupMemberStatus?)m.Status)
                    .FirstOrDefault(),
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var trainerLookup = await userLookup.FindByIdsAsync(rows.Select(r => r.TrainerId).Distinct().ToList(), ct);
        var groups = rows
            .Select(r => new GroupDto(
                r.Id, r.Name, r.Description, r.TrainerId, r.ClubId, r.MemberCount,
                TrainerDisplayName(trainerLookup, r.TrainerId),
                r.TrainerId == userId || r.IsCoTrainer || isClubTrainer
                    ? GroupRelation.Trainer
                    : r.MyMembership switch
                    {
                        GroupMemberStatus.Active => GroupRelation.Member,
                        GroupMemberStatus.Pending => GroupRelation.Pending,
                        _ => GroupRelation.None,
                    }))
            .ToList();

        return Result<IReadOnlyList<GroupDto>>.Success(groups);
    }

    public async Task<Result> RequestJoinGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        // Wer die Gruppe ohnehin betreut, braucht keinen Beitritt - vorher
        // konnte sich eine Trainer:in bei ihrer eigenen Gruppe bewerben und
        // fand die Anfrage anschließend in ihrer eigenen Freigabeliste wieder.
        if (await GetManageableGroupAsync(userId, groupId, ct) is not null)
            return Result.Failure("Du betreust diese Gruppe bereits als Trainer:in.");

        // Einschließlich entfernter Zeilen suchen: Eine ABGELEHNTE Anfrage wird
        // weich gelöscht (siehe DecideGroupJoinRequestAsync). Ohne das hier
        // hätte dieselbe Person sich nie wieder bewerben können - der Insert
        // wäre am eindeutigen Index gescheitert, und niemand hätte gesehen,
        // warum.
        var (existing, isActive) = await db.GroupMembers
            .FindIncludingRemovedAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
        if (isActive)
            return existing!.Status == GroupMemberStatus.Pending
                ? Result.Failure("Du hast bereits eine ausstehende Beitrittsanfrage für diese Gruppe.")
                : Result.Failure("Du bist bereits Mitglied dieser Gruppe.");

        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.Status = GroupMemberStatus.Pending;
            existing.JoinedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId, Status = GroupMemberStatus.Pending });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupJoinRequestDto>>> GetGroupJoinRequestsAsync(Guid trainerId, Guid groupId, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(trainerId, groupId, ct);
        if (group is null)
            return Result<IReadOnlyList<GroupJoinRequestDto>>.NotFound("Gruppe nicht gefunden.");

        var pending = await db.GroupMembers
            .Where(m => m.GroupId == groupId && m.Status == GroupMemberStatus.Pending)
            .Select(m => new { m.UserId, m.JoinedAt })
            .AsNoTracking()
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(pending.Select(p => p.UserId).ToList(), ct);
        var dtos = pending
            .Select(p => lookup.TryGetValue(p.UserId, out var info)
                ? new GroupJoinRequestDto(p.UserId, info.Email, info.FirstName, info.LastName, p.JoinedAt)
                : new GroupJoinRequestDto(p.UserId, "(unbekannt)", "", "", p.JoinedAt))
            .ToList();

        return Result<IReadOnlyList<GroupJoinRequestDto>>.Success(dtos);
    }

    public async Task<Result> DecideGroupJoinRequestAsync(Guid trainerId, Guid groupId, Guid memberId, bool approve, CancellationToken ct = default)
    {
        var group = await GetManageableGroupAsync(trainerId, groupId, ct);
        if (group is null)
            return Result.NotFound("Gruppe nicht gefunden.");

        var memberRow = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberId && m.Status == GroupMemberStatus.Pending, ct);
        if (memberRow is null)
            return Result.NotFound("Beitrittsanfrage nicht gefunden.");

        if (approve)
            memberRow.Status = GroupMemberStatus.Active;
        else
            memberRow.DeletedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // Nur von GetDetailAsync (reiner Lesezugriff) verwendet - daher AsNoTracking.
    // Nur Active-Mitglieder zählen als Mitglieder (Pending = noch nicht freigegebene Anfragen).
    // Zugriff hat: der/die Gruppen-Trainer:in, aktive Mitglieder ODER jede:r
    // Trainer:in des Vereins, dem die Gruppe gehört.
    private async Task<Group?> GetAccessibleGroupAsync(Guid userId, Guid groupId, CancellationToken ct) =>
        await db.Groups
            .Include(g => g.Members.Where(m => m.Status == GroupMemberStatus.Active))
            .Include(g => g.Trainers)
            .Where(g => g.Id == groupId)
            .Where(g => g.TrainerId == userId
                || g.Trainers.Any(t => t.UserId == userId)
                || g.Members.Any(m => m.UserId == userId && m.Status == GroupMemberStatus.Active)
                || (g.ClubId != null && db.ClubTrainers.Any(t => t.ClubId == g.ClubId && t.UserId == userId)))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    // Ob der/die Nutzer:in die Gruppe verwalten darf: als Hauptverantwortliche:r,
    // als weitere:r Trainer:in dieser Gruppe ODER als Trainer:in des Vereins,
    // dem die Gruppe gehört ("jede:r Vereinstrainer:in").
    // Liefert die getrackte Entität zurück, damit Aufrufer sie direkt ändern können.
    private async Task<Group?> GetManageableGroupAsync(Guid userId, Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null) return null;
        if (group.TrainerId == userId) return group;
        if (await db.GroupTrainers.AnyAsync(t => t.GroupId == groupId && t.UserId == userId, ct)) return group;
        if (group.ClubId is { } clubId && await db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId, ct))
            return group;
        return null;
    }

    private async Task<bool> IsGroupMemberAsync(Guid trainerId, Guid groupId, Guid memberId, CancellationToken ct)
    {
        var canManage = await GetManageableGroupAsync(trainerId, groupId, ct) is not null;
        if (!canManage) return false;

        return await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == memberId && m.Status == GroupMemberStatus.Active, ct);
    }
}
