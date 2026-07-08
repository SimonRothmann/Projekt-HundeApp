using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

public class ClubService(IApplicationDbContext db, IUserLookupService userLookup, INotificationService notifications) : IClubService
{
    public async Task<Result<IReadOnlyList<ClubDto>>> GetClubsAsync(CancellationToken ct = default)
    {
        var clubs = await db.Clubs
            .Select(c => new ClubDto(c.Id, c.Name, c.Description, c.Trainers.Count, c.Groups.Count))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClubDto>>.Success(clubs);
    }

    public async Task<Result<IReadOnlyList<ClubDto>>> GetMyClubsAsync(Guid userId, CancellationToken ct = default)
    {
        var clubs = await db.Clubs
            .Where(c => c.Trainers.Any(t => t.UserId == userId))
            .Select(c => new ClubDto(c.Id, c.Name, c.Description, c.Trainers.Count, c.Groups.Count))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClubDto>>.Success(clubs);
    }

    public async Task<Result<ClubDetailDto>> GetDetailAsync(Guid clubId, CancellationToken ct = default)
    {
        var club = await db.Clubs
            .Include(c => c.Trainers)
            .Include(c => c.Groups)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clubId, ct);

        if (club is null)
            return Result<ClubDetailDto>.Failure("Verein nicht gefunden.");

        var approvedMemberships = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Approved)
            .AsNoTracking()
            .ToListAsync(ct);

        var userIds = club.Trainers.Select(t => t.UserId)
            .Concat(approvedMemberships.Select(m => m.UserId))
            .Distinct()
            .ToList();
        var lookup = await userLookup.FindByIdsAsync(userIds, ct);

        var trainers = club.Trainers
            .Select(t => lookup.TryGetValue(t.UserId, out var info)
                ? new ClubTrainerDto(t.UserId, info.Email, info.FirstName, info.LastName, t.CreatedAt)
                : new ClubTrainerDto(t.UserId, "(unbekannt)", "", "", t.CreatedAt))
            .ToList();

        var members = approvedMemberships
            .Select(m => lookup.TryGetValue(m.UserId, out var info)
                ? new ClubMemberDto(m.Id, m.UserId, info.Email, info.FirstName, info.LastName, m.RequestedAt, m.DecidedAt)
                : new ClubMemberDto(m.Id, m.UserId, "(unbekannt)", "", "", m.RequestedAt, m.DecidedAt))
            .ToList();

        var dto = new ClubDetailDto(new ClubDto(club.Id, club.Name, club.Description, trainers.Count, club.Groups.Count), trainers, members);
        return Result<ClubDetailDto>.Success(dto);
    }

    public async Task<Result<ClubDto>> CreateAsync(CreateClubRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ClubDto>.Failure("Name ist erforderlich.");

        var club = new Club { Name = request.Name.Trim(), Description = request.Description };
        db.Clubs.Add(club);
        await db.SaveChangesAsync(ct);

        return Result<ClubDto>.Success(new ClubDto(club.Id, club.Name, club.Description, 0, 0));
    }

    public async Task<Result> AssignTrainerAsync(Guid clubId, AssignClubTrainerRequest request, CancellationToken ct = default)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result.Failure("Verein nicht gefunden.");

        var user = await userLookup.FindByEmailAsync(request.Email, ct);
        if (user is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        var alreadyAssigned = await db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == user.UserId, ct);
        if (alreadyAssigned)
            return Result.Failure("Dieser Benutzer ist bereits Trainer dieses Vereins.");

        db.ClubTrainers.Add(new ClubTrainer { ClubId = clubId, UserId = user.UserId });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveTrainerAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        var entry = await db.ClubTrainers.FirstOrDefaultAsync(t => t.ClubId == clubId && t.UserId == userId, ct);
        if (entry is null)
            return Result.Failure("Trainer-Zuweisung nicht gefunden.");

        entry.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Admin-Weg, ein Mitglied direkt (ohne Beitrittsanfrage-Workflow) in
    /// einen Verein aufzunehmen. Nutzt dieselbe ClubMembership-Tabelle wie
    /// der Antrag-Weg, setzt Status aber sofort auf <see cref="ClubMembershipStatus.Approved"/>.
    /// </summary>
    public async Task<Result> AddMemberAsync(Guid clubId, AssignClubMemberRequest request, CancellationToken ct = default)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result.Failure("Verein nicht gefunden.");

        var user = await userLookup.FindByEmailAsync(request.Email, ct);
        if (user is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        var existing = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.UserId == user.UserId)
            .OrderByDescending(m => m.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null && existing.Status == ClubMembershipStatus.Approved)
            return Result.Failure("Dieser Benutzer ist bereits Mitglied dieses Vereins.");

        if (existing is not null && existing.Status == ClubMembershipStatus.Pending)
        {
            // Bestehende Anfrage direkt genehmigen statt eine zweite Zeile anzulegen.
            existing.Status = ClubMembershipStatus.Approved;
            existing.DecidedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.ClubMemberships.Add(new ClubMembership
            {
                ClubId = clubId,
                UserId = user.UserId,
                Status = ClubMembershipStatus.Approved,
                DecidedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        await notifications.CreateAsync(user.UserId, $"Du wurdest zum Verein \"{club.Name}\" hinzugefügt.", "/clubs", ct);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        var membership = await db.ClubMemberships
            .FirstOrDefaultAsync(m => m.ClubId == clubId && m.UserId == userId && m.Status == ClubMembershipStatus.Approved, ct);
        if (membership is null)
            return Result.Failure("Mitgliedschaft nicht gefunden.");

        membership.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ClubSummaryDto>>> GetBrowsableClubsAsync(CancellationToken ct = default)
    {
        var clubs = await db.Clubs
            .Select(c => new ClubSummaryDto(c.Id, c.Name, c.Description))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClubSummaryDto>>.Success(clubs);
    }

    public async Task<Result<IReadOnlyList<ClubMembershipDto>>> GetMyMembershipsAsync(Guid userId, CancellationToken ct = default)
    {
        var memberships = await db.ClubMemberships
            .Where(m => m.UserId == userId)
            .Select(m => new ClubMembershipDto(m.Id, m.ClubId, m.Club!.Name, m.Status, m.RequestedAt, m.DecidedAt))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClubMembershipDto>>.Success(memberships);
    }

    public async Task<Result<ClubMembershipDto>> RequestJoinAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result<ClubMembershipDto>.Failure("Verein nicht gefunden.");

        var existing = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.UserId == userId)
            .OrderByDescending(m => m.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null && existing.Status is ClubMembershipStatus.Pending or ClubMembershipStatus.Approved)
            return Result<ClubMembershipDto>.Failure("Du hast bereits eine Anfrage oder Mitgliedschaft für diesen Verein.");

        var membership = new ClubMembership { ClubId = clubId, UserId = userId };
        db.ClubMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return Result<ClubMembershipDto>.Success(new ClubMembershipDto(membership.Id, clubId, club.Name, membership.Status, membership.RequestedAt, membership.DecidedAt));
    }

    public async Task<Result<IReadOnlyList<ClubMemberDto>>> GetJoinRequestsAsync(Guid callerId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result<IReadOnlyList<ClubMemberDto>>.Failure("Verein nicht gefunden.");

        var pending = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Pending)
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(pending.Select(m => m.UserId).ToList(), ct);
        var dtos = pending
            .Select(m => lookup.TryGetValue(m.UserId, out var info)
                ? new ClubMemberDto(m.Id, m.UserId, info.Email, info.FirstName, info.LastName, m.RequestedAt, m.DecidedAt)
                : new ClubMemberDto(m.Id, m.UserId, "(unbekannt)", "", "", m.RequestedAt, m.DecidedAt))
            .ToList();

        return Result<IReadOnlyList<ClubMemberDto>>.Success(dtos);
    }

    public async Task<Result> DecideJoinRequestAsync(Guid callerId, Guid clubId, Guid membershipId, bool approve, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result.Failure("Verein nicht gefunden.");

        var membership = await db.ClubMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.ClubId == clubId, ct);
        if (membership is null || membership.Status != ClubMembershipStatus.Pending)
            return Result.Failure("Beitrittsanfrage nicht gefunden.");

        membership.Status = approve ? ClubMembershipStatus.Approved : ClubMembershipStatus.Rejected;
        membership.DecidedAt = DateTimeOffset.UtcNow;
        membership.DecidedByUserId = callerId;
        await db.SaveChangesAsync(ct);

        var club = await db.Clubs.AsNoTracking().FirstAsync(c => c.Id == clubId, ct);
        var message = approve
            ? $"Dein Beitritt zu \"{club.Name}\" wurde angenommen."
            : $"Dein Beitritt zu \"{club.Name}\" wurde abgelehnt.";
        await notifications.CreateAsync(membership.UserId, message, "/clubs", ct);

        return Result.Success();
    }

    public async Task<Result> LeaveClubAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        var membership = await db.ClubMemberships
            .FirstOrDefaultAsync(m => m.ClubId == clubId && m.UserId == userId && m.Status == ClubMembershipStatus.Approved, ct);
        if (membership is null)
            return Result.Failure("Keine aktive Mitgliedschaft gefunden.");

        membership.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ClubMemberDto>>> GetMembersAsync(Guid callerId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result<IReadOnlyList<ClubMemberDto>>.Failure("Verein nicht gefunden.");

        var members = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Approved)
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(members.Select(m => m.UserId).ToList(), ct);
        var dtos = members
            .Select(m => lookup.TryGetValue(m.UserId, out var info)
                ? new ClubMemberDto(m.Id, m.UserId, info.Email, info.FirstName, info.LastName, m.RequestedAt, m.DecidedAt)
                : new ClubMemberDto(m.Id, m.UserId, "(unbekannt)", "", "", m.RequestedAt, m.DecidedAt))
            .ToList();

        return Result<IReadOnlyList<ClubMemberDto>>.Success(dtos);
    }

    public async Task<Result> PromoteMemberToTrainerAsync(Guid callerId, Guid clubId, Guid targetUserId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result.Failure("Verein nicht gefunden.");

        var isApprovedMember = await db.ClubMemberships
            .AnyAsync(m => m.ClubId == clubId && m.UserId == targetUserId && m.Status == ClubMembershipStatus.Approved, ct);
        if (!isApprovedMember)
            return Result.Failure("Nur bestehende Mitglieder dieses Vereins können zu Trainern gemacht werden.");

        var alreadyTrainer = await db.IsClubTrainerAsync(targetUserId, clubId, ct);
        if (alreadyTrainer)
            return Result.Failure("Dieser Benutzer ist bereits Trainer dieses Vereins.");

        db.ClubTrainers.Add(new ClubTrainer { ClubId = clubId, UserId = targetUserId });
        await db.SaveChangesAsync(ct);

        var club = await db.Clubs.AsNoTracking().FirstAsync(c => c.Id == clubId, ct);
        await notifications.CreateAsync(targetUserId, $"Du bist jetzt Trainer bei \"{club.Name}\".", "/trainer", ct);

        return Result.Success();
    }
}
