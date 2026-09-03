using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <inheritdoc />
public class ClubRegistrationService(
    IApplicationDbContext db,
    IUserLookupService userLookup,
    INotificationService notifications,
    ITrainerRoleService trainerRoles) : IClubRegistrationService
{
    /// <summary>
    /// Als Zeichenkette und nicht über die Konstante aus Infrastructure:
    /// Application kennt Infrastructure nicht (siehe ARCHITECTURE.md), und
    /// ListUserIdsInRoleAsync verlangt den Namen ohnehin als Text - genauso
    /// hält es TrainerRoleService.
    /// </summary>
    private const string AdminRoleName = "ADMIN";

    public async Task<Result<ClubRegistrationDto>> RequestAsync(Guid userId, CreateClubRegistrationRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result<ClubRegistrationDto>.Failure("Name des Vereins ist erforderlich.");
        if (name.Length > 200)
            return Result<ClubRegistrationDto>.Failure("Der Name ist zu lang.");

        // Gibt es den Verein schon, ist der Antrag sinnlos - dann will die
        // Person vermutlich beitreten, nicht gründen. Das gleich zu sagen
        // spart ihr das Warten auf eine Ablehnung.
        var vorhanden = await db.Clubs.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct);
        if (vorhanden)
            return Result<ClubRegistrationDto>.Failure(
                "Diesen Verein gibt es bereits. Stelle stattdessen eine Beitrittsanfrage.");

        // Ein offener Antrag je Person. Ohne diese Grenze ließe sich die
        // Freigabeliste mit Anträgen fluten, und genau davor soll die
        // Freigabe ja schützen.
        var offener = await db.ClubRegistrations
            .AnyAsync(r => r.RequestedByUserId == userId && r.Status == ClubRegistrationStatus.Pending, ct);
        if (offener)
            return Result<ClubRegistrationDto>.Failure(
                "Du hast bereits einen Antrag offen. Warte, bis er entschieden ist.");

        var eintrag = new ClubRegistration
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            RequestedByUserId = userId,
        };
        db.ClubRegistrations.Add(eintrag);
        await db.SaveChangesAsync(ct);

        // Alle Admins informieren - sonst bliebe der Antrag liegen, bis
        // zufällig jemand in die Verwaltung schaut.
        foreach (var adminId in await userLookup.ListUserIdsInRoleAsync(AdminRoleName, ct))
            await notifications.CreateAsync(adminId, $"Neuer Vereinsantrag: \"{name}\".", "/admin", ct);

        return Result<ClubRegistrationDto>.Success(await ZuDtoAsync(eintrag, ct));
    }

    public async Task<Result<IReadOnlyList<ClubRegistrationDto>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var eintraege = await db.ClubRegistrations
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClubRegistrationDto>>.Success(await ZuDtosAsync(eintraege, ct));
    }

    public async Task<Result<IReadOnlyList<ClubRegistrationDto>>> GetPendingAsync(CancellationToken ct = default)
    {
        var eintraege = await db.ClubRegistrations
            .Where(r => r.Status == ClubRegistrationStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClubRegistrationDto>>.Success(await ZuDtosAsync(eintraege, ct));
    }

    public async Task<Result> ApproveAsync(Guid adminId, Guid registrationId, CancellationToken ct = default)
    {
        var eintrag = await db.ClubRegistrations.FirstOrDefaultAsync(r => r.Id == registrationId, ct);
        if (eintrag is null) return Result.NotFound("Antrag nicht gefunden.");
        if (eintrag.Status != ClubRegistrationStatus.Pending)
            return Result.Failure("Über diesen Antrag wurde bereits entschieden.");

        // Zwischen Antrag und Freigabe kann jemand anderes denselben Verein
        // angelegt bekommen haben - dann darf hier kein Zwilling entstehen.
        if (await db.Clubs.AnyAsync(c => c.Name.ToLower() == eintrag.Name.ToLower(), ct))
            return Result.Failure("Einen Verein mit diesem Namen gibt es inzwischen. Bitte den Antrag ablehnen.");

        var verein = new Club { Name = eintrag.Name, Description = eintrag.Description };
        db.Clubs.Add(verein);

        // Der Antragsteller wird erste verwaltende Person - sonst entstünde
        // ein Verein, den niemand betreuen kann, und die Freigabe hätte nur
        // Arbeit verschoben statt sie zu erledigen.
        db.ClubTrainers.Add(new ClubTrainer
        {
            ClubId = verein.Id,
            UserId = eintrag.RequestedByUserId,
            Role = ClubRole.Verwaltung,
        });

        eintrag.Status = ClubRegistrationStatus.Approved;
        eintrag.DecidedAt = DateTimeOffset.UtcNow;
        eintrag.DecidedByUserId = adminId;
        eintrag.ClubId = verein.Id;
        eintrag.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await trainerRoles.SyncAsync(eintrag.RequestedByUserId, ct);
        await notifications.CreateAsync(
            eintrag.RequestedByUserId,
            $"Dein Verein \"{verein.Name}\" wurde freigegeben. Du verwaltest ihn.",
            "/trainer",
            ct);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid adminId, Guid registrationId, DecideClubRegistrationRequest request, CancellationToken ct = default)
    {
        var eintrag = await db.ClubRegistrations.FirstOrDefaultAsync(r => r.Id == registrationId, ct);
        if (eintrag is null) return Result.NotFound("Antrag nicht gefunden.");
        if (eintrag.Status != ClubRegistrationStatus.Pending)
            return Result.Failure("Über diesen Antrag wurde bereits entschieden.");

        eintrag.Status = ClubRegistrationStatus.Rejected;
        eintrag.DecidedAt = DateTimeOffset.UtcNow;
        eintrag.DecidedByUserId = adminId;
        eintrag.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        eintrag.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var grund = eintrag.DecisionNote is null ? "" : $" Begründung: {eintrag.DecisionNote}";
        await notifications.CreateAsync(
            eintrag.RequestedByUserId,
            $"Dein Vereinsantrag \"{eintrag.Name}\" wurde abgelehnt.{grund}",
            "/clubs",
            ct);

        return Result.Success();
    }

    private async Task<ClubRegistrationDto> ZuDtoAsync(ClubRegistration eintrag, CancellationToken ct) =>
        (await ZuDtosAsync([eintrag], ct))[0];

    private async Task<IReadOnlyList<ClubRegistrationDto>> ZuDtosAsync(IReadOnlyList<ClubRegistration> eintraege, CancellationToken ct)
    {
        if (eintraege.Count == 0) return [];

        var lookup = await userLookup.FindByIdsAsync(eintraege.Select(r => r.RequestedByUserId).Distinct().ToList(), ct);

        return eintraege.Select(r =>
        {
            var gefunden = lookup.TryGetValue(r.RequestedByUserId, out var info);
            return new ClubRegistrationDto(
                r.Id,
                r.Name,
                r.Description,
                r.RequestedByUserId,
                gefunden ? info!.Email : "(unbekannt)",
                gefunden ? $"{info!.FirstName} {info.LastName}".Trim() : "(unbekannt)",
                r.Status,
                r.CreatedAt,
                r.DecidedAt,
                r.DecisionNote,
                r.ClubId);
        }).ToList();
    }
}
