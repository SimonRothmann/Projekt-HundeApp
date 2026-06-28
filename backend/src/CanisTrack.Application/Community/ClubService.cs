using CanisTrack.Application.Abstractions;
using CanisTrack.Application.Common;
using CanisTrack.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Application.Community;

public class ClubService(IApplicationDbContext db, IUserLookupService userLookup) : IClubService
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
            .FirstOrDefaultAsync(c => c.Id == clubId, ct);

        if (club is null)
            return Result<ClubDetailDto>.Failure("Verein nicht gefunden.");

        var lookup = await userLookup.FindByIdsAsync(club.Trainers.Select(t => t.UserId).ToList(), ct);
        var trainers = club.Trainers
            .Select(t => lookup.TryGetValue(t.UserId, out var info)
                ? new ClubTrainerDto(t.UserId, info.Email, info.FirstName, info.LastName, t.CreatedAt)
                : new ClubTrainerDto(t.UserId, "(unbekannt)", "", "", t.CreatedAt))
            .ToList();

        var dto = new ClubDetailDto(new ClubDto(club.Id, club.Name, club.Description, trainers.Count, club.Groups.Count), trainers);
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
}
