using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

public class ClubInviteService(IApplicationDbContext db, IClubService clubs) : IClubInviteService
{
    // Eine Meldung für jeden Grund, warum ein Code nicht zieht: unbekannt,
    // abgeschaltet, ersetzt, Verein gelöscht, falsche Form.
    private const string CodeUngueltig = "Dieser Einladungslink gilt nicht mehr.";

    // Verwalten darf, wer auch Beitrittsanfragen entscheiden darf
    // (ClubService.DecideJoinRequestAsync): Trainer:innen des Vereins. Ein
    // globaler Admin wird dort nicht anders behandelt, hier ebenso wenig.
    public async Task<Result<ClubInviteLinkDto?>> GetAsync(Guid callerId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result<ClubInviteLinkDto?>.NotFound("Verein nicht gefunden.");

        var code = await db.Clubs.Where(c => c.Id == clubId).Select(c => c.InviteCode).FirstOrDefaultAsync(ct);
        return Result<ClubInviteLinkDto?>.Success(string.IsNullOrEmpty(code) ? null : new ClubInviteLinkDto(code));
    }

    public async Task<Result<ClubInviteLinkDto>> RegenerateAsync(Guid callerId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result<ClubInviteLinkDto>.NotFound("Verein nicht gefunden.");

        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result<ClubInviteLinkDto>.NotFound("Verein nicht gefunden.");

        club.InviteCode = ClubInviteCode.Erzeugen();
        club.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<ClubInviteLinkDto>.Success(new ClubInviteLinkDto(club.InviteCode));
    }

    public async Task<Result> DisableAsync(Guid callerId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result.NotFound("Verein nicht gefunden.");

        if (club.InviteCode is null) return Result.Success();

        club.InviteCode = null;
        club.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ClubInvitePreviewDto>> GetPreviewAsync(string code, CancellationToken ct = default)
    {
        var club = await FindClubAsync(code, ct);
        return club is null
            ? Result<ClubInvitePreviewDto>.NotFound(CodeUngueltig)
            : Result<ClubInvitePreviewDto>.Success(new ClubInvitePreviewDto(club.Name));
    }

    public async Task<Result<ClubMembershipDto>> JoinAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var club = await FindClubAsync(code, ct);
        if (club is null)
            return Result<ClubMembershipDto>.NotFound(CodeUngueltig);

        var existing = await db.ClubMemberships
            .AsNoTracking()
            .Where(m => m.ClubId == club.Id && m.UserId == userId)
            .OrderByDescending(m => m.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null && existing.Status is ClubMembershipStatus.Pending or ClubMembershipStatus.Approved)
            return Result<ClubMembershipDto>.Success(
                new ClubMembershipDto(existing.Id, club.Id, club.Name, existing.Status, existing.RequestedAt, existing.DecidedAt));

        // Erst nach dem Stand von oben: Wer Trainer:in UND Mitglied ist, bekommt
        // wie jede:r andere den bestehenden Stand zurück. Eine neue Anfrage
        // legt sich die Leitung mit dem eigenen QR-Code aber nicht selbst an
        // und gibt sie sich dann selbst frei.
        if (await db.IsClubTrainerAsync(userId, club.Id, ct))
            return Result<ClubMembershipDto>.Failure("Du bist Trainer:in dieses Vereins.");

        // Frühere abgelehnte oder verlassene Mitgliedschaften behandelt
        // RequestJoinAsync wie beim Weg über die Liste - eine Stelle dafür.
        return await clubs.RequestJoinAsync(userId, club.Id, ClubMembershipSource.InviteLink, ct);
    }

    private async Task<Club?> FindClubAsync(string code, CancellationToken ct)
    {
        // Vor der Abfrage: Was nie ein Code war, braucht keinen Datenbankzugriff.
        if (!ClubInviteCode.IstGueltigeForm(code)) return null;
        return await db.Clubs.AsNoTracking().FirstOrDefaultAsync(c => c.InviteCode == code, ct);
    }
}
