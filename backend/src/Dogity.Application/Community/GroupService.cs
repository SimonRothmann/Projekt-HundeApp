using Dogity.Application.Abstractions;
using Dogity.Application.Common;
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
public class GroupService(IApplicationDbContext db, IUserLookupService userLookup) : IGroupService
{
    public async Task<Result<IReadOnlyList<GroupDto>>> GetMyGroupsAsync(Guid trainerId, CancellationToken ct = default)
    {
        var groups = await db.Groups
            .Where(g => g.TrainerId == trainerId)
            .Select(g => new GroupDto(g.Id, g.Name, g.Description, g.TrainerId, g.ClubId, g.Members.Count))
            .ToListAsync(ct);

        return Result<IReadOnlyList<GroupDto>>.Success(groups);
    }

    /// <summary>
    /// "Trainer-Sein" ist bewusst rein datengetrieben (siehe TODO.md
    /// "Rollenswitch"): wer mindestens eine Gruppe leitet oder als Trainer
    /// einem Verein zugewiesen ist, bekommt die Trainer-Perspektive im
    /// Frontend angezeigt - unabhängig von der Identity-Rolle.
    /// </summary>
    public async Task<bool> IsTrainerAsync(Guid userId, CancellationToken ct = default)
    {
        var leadsGroup = await db.Groups.AnyAsync(g => g.TrainerId == userId, ct);
        if (leadsGroup) return true;

        return await db.ClubTrainers.AnyAsync(t => t.UserId == userId, ct);
    }

    public async Task<Result<GroupDetailDto>> GetDetailAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await GetAccessibleGroupAsync(userId, groupId, ct);
        if (group is null)
            return Result<GroupDetailDto>.Failure("Gruppe nicht gefunden.");

        var memberLookup = await userLookup.FindByIdsAsync(group.Members.Select(m => m.UserId).ToList(), ct);
        var members = group.Members
            .Select(m => memberLookup.TryGetValue(m.UserId, out var info)
                ? new GroupMemberDto(m.UserId, info.Email, info.FirstName, info.LastName, m.Role, m.JoinedAt)
                : new GroupMemberDto(m.UserId, "(unbekannt)", "", "", m.Role, m.JoinedAt))
            .ToList();

        var dto = new GroupDetailDto(new GroupDto(group.Id, group.Name, group.Description, group.TrainerId, group.ClubId, members.Count), members);
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

        return Result<GroupDto>.Success(new GroupDto(group.Id, group.Name, group.Description, group.TrainerId, group.ClubId, 0));
    }

    public async Task<Result> AddMemberAsync(Guid trainerId, Guid groupId, AddMemberRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.TrainerId == trainerId, ct);
        if (group is null)
            return Result.Failure("Gruppe nicht gefunden.");

        var user = await userLookup.FindByEmailAsync(request.Email, ct);
        if (user is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        var alreadyMember = await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == user.UserId, ct);
        if (alreadyMember)
            return Result.Failure("Dieser Benutzer ist bereits Mitglied der Gruppe.");

        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = user.UserId });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid trainerId, Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.TrainerId == trainerId, ct);
        if (group is null)
            return Result.Failure("Gruppe nicht gefunden.");

        var member = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberId, ct);
        if (member is null)
            return Result.Failure("Mitglied nicht gefunden.");

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

        var alreadyAssigned = await db.TrainerAssignments.AnyAsync(t => t.TrainerId == trainerId && t.DogId == request.DogId, ct);
        if (alreadyAssigned)
            return Result.Failure("Du betreust diesen Hund bereits.");

        db.TrainerAssignments.Add(new TrainerAssignment
        {
            TrainerId = trainerId,
            MemberId = request.MemberId,
            DogId = request.DogId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupDto>>> GetGroupsByClubAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        var isClubMember = await db.ClubMemberships.AnyAsync(
            m => m.ClubId == clubId && m.UserId == userId && m.Status == ClubMembershipStatus.Approved, ct);
        var isClubTrainer = await db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId, ct);

        if (!isClubMember && !isClubTrainer)
            return Result<IReadOnlyList<GroupDto>>.Failure("Keine Berechtigung für diesen Verein.");

        var groups = await db.Groups
            .Where(g => g.ClubId == clubId)
            .Select(g => new GroupDto(g.Id, g.Name, g.Description, g.TrainerId, g.ClubId, g.Members.Count(m => m.Status == GroupMemberStatus.Active)))
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<IReadOnlyList<GroupDto>>.Success(groups);
    }

    public async Task<Result> RequestJoinGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct);
        if (group is null)
            return Result.Failure("Gruppe nicht gefunden.");

        var existing = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
        if (existing is not null)
            return existing.Status == GroupMemberStatus.Pending
                ? Result.Failure("Du hast bereits eine ausstehende Beitrittsanfrage für diese Gruppe.")
                : Result.Failure("Du bist bereits Mitglied dieser Gruppe.");

        db.GroupMembers.Add(new GroupMember { GroupId = groupId, UserId = userId, Status = GroupMemberStatus.Pending });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<GroupJoinRequestDto>>> GetGroupJoinRequestsAsync(Guid trainerId, Guid groupId, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.TrainerId == trainerId, ct);
        if (group is null)
            return Result<IReadOnlyList<GroupJoinRequestDto>>.Failure("Gruppe nicht gefunden.");

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
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.TrainerId == trainerId, ct);
        if (group is null)
            return Result.Failure("Gruppe nicht gefunden.");

        var memberRow = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberId && m.Status == GroupMemberStatus.Pending, ct);
        if (memberRow is null)
            return Result.Failure("Beitrittsanfrage nicht gefunden.");

        if (approve)
            memberRow.Status = GroupMemberStatus.Active;
        else
            memberRow.DeletedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // Nur von GetDetailAsync (reiner Lesezugriff) verwendet - daher AsNoTracking.
    // Nur Active-Mitglieder zählen als Mitglieder (Pending = noch nicht freigegebene Anfragen).
    private async Task<Group?> GetAccessibleGroupAsync(Guid userId, Guid groupId, CancellationToken ct) =>
        await db.Groups
            .Include(g => g.Members.Where(m => m.Status == GroupMemberStatus.Active))
            .Where(g => g.Id == groupId)
            .Where(g => g.TrainerId == userId || g.Members.Any(m => m.UserId == userId && m.Status == GroupMemberStatus.Active))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    private async Task<bool> IsGroupMemberAsync(Guid trainerId, Guid groupId, Guid memberId, CancellationToken ct)
    {
        var ownsGroup = await db.Groups.AnyAsync(g => g.Id == groupId && g.TrainerId == trainerId, ct);
        if (!ownsGroup) return false;

        return await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == memberId && m.Status == GroupMemberStatus.Active, ct);
    }
}
