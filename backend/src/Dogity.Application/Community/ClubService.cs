using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

public class ClubService(IApplicationDbContext db, IUserLookupService userLookup, INotificationService notifications, ITrainerRoleService trainerRoles) : IClubService
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
            return Result<ClubDetailDto>.NotFound("Verein nicht gefunden.");

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

        // Ohne zusätzliche Abfrage: die Trainer:innen sind hier schon geladen.
        var trainerIds = club.Trainers.Select(t => t.UserId).ToHashSet();
        var members = approvedMemberships
            .Select(m => ZuMitglied(m, lookup, trainerIds))
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

    /// <summary>
    /// Trainer:in berufen. Die Berechtigungsprüfung liegt HIER und nicht am
    /// Controller: Der Aufruf hängt an zwei Routen (Verein und Admin), und
    /// eine Regel, die an der Route klebt, geht beim Hinzufügen der zweiten
    /// verloren.
    /// </summary>
    public async Task<Result> AssignTrainerAsync(Guid callerId, bool isAdmin, Guid clubId, AssignClubTrainerRequest request, CancellationToken ct = default)
    {
        if (!isAdmin && !await db.CanManageClubAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result.NotFound("Verein nicht gefunden.");

        var user = await userLookup.FindByEmailAsync(request.Email, ct);
        if (user is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index).
        var (existing, isActive) = await db.ClubTrainers
            .FindIncludingRemovedAsync(t => t.ClubId == clubId && t.UserId == user.UserId, ct);
        if (isActive)
            return Result.Failure("Dieser Benutzer ist bereits Trainer dieses Vereins.");

        if (existing is not null)
        {
            existing.DeletedAt = null;
            existing.Role = request.Role;
        }
        else
        {
            db.ClubTrainers.Add(new ClubTrainer { ClubId = clubId, UserId = user.UserId, Role = request.Role });
        }

        await db.SaveChangesAsync(ct);
        await trainerRoles.SyncAsync(user.UserId, ct);
        return Result.Success();
    }

    /// <summary>
    /// Stammdaten des Vereins ändern - für Verwaltende des Vereins und für
    /// globale Admins.
    ///
    /// Bisher gab es das überhaupt nicht: Ein einmal angelegter Verein ließ
    /// sich von niemandem umbenennen, auch nicht vom Admin.
    /// </summary>
    public async Task<Result> UpdateClubAsync(Guid callerId, bool isAdmin, Guid clubId, UpdateClubRequest request, CancellationToken ct = default)
    {
        if (!isAdmin && !await db.CanManageClubAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Name ist erforderlich.");

        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null) return Result.NotFound("Verein nicht gefunden.");

        club.Name = name;
        club.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        club.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Rolle einer Trainerin oder eines Trainers innerhalb des Vereins
    /// ändern. Derselbe Schutz wie beim Entfernen: Die letzte verwaltende
    /// Person kann sich nicht selbst herabstufen.
    /// </summary>
    public async Task<Result> UpdateTrainerRoleAsync(Guid callerId, bool isAdmin, Guid clubId, Guid userId, ClubRole role, CancellationToken ct = default)
    {
        if (!isAdmin && !await db.CanManageClubAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var entry = await db.ClubTrainers.FirstOrDefaultAsync(t => t.ClubId == clubId && t.UserId == userId, ct);
        if (entry is null) return Result.NotFound("Trainer-Zuweisung nicht gefunden.");
        if (entry.Role == role) return Result.Success();

        if (entry.Role == ClubRole.Verwaltung)
        {
            var weitere = await db.ClubTrainers
                .CountAsync(t => t.ClubId == clubId && t.Role == ClubRole.Verwaltung && t.UserId != userId, ct);
            if (weitere == 0)
                return Result.Failure("Das ist die letzte verwaltende Person des Vereins. Bestimme zuerst jemand anderen.");
        }

        entry.Role = role;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Trainer:in abberufen - Berechtigung wie bei AssignTrainerAsync.</summary>
    public async Task<Result> RemoveTrainerAsync(Guid callerId, bool isAdmin, Guid clubId, Guid userId, CancellationToken ct = default)
    {
        if (!isAdmin && !await db.CanManageClubAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var entry = await db.ClubTrainers.FirstOrDefaultAsync(t => t.ClubId == clubId && t.UserId == userId, ct);
        if (entry is null)
            return Result.NotFound("Trainer-Zuweisung nicht gefunden.");

        // Die letzte verwaltende Person darf nicht gehen. Sonst bliebe ein
        // Verein zurück, den niemand mehr verwalten kann - weder Trainer
        // berufen noch Stammdaten ändern -, und nur ein globaler Admin käme
        // wieder heran. Genau diese Sackgasse soll die Selbstverwaltung
        // vermeiden.
        if (entry.Role == ClubRole.Verwaltung)
        {
            var weitere = await db.ClubTrainers
                .CountAsync(t => t.ClubId == clubId && t.Role == ClubRole.Verwaltung && t.UserId != userId, ct);
            if (weitere == 0)
                return Result.Failure("Das ist die letzte verwaltende Person des Vereins. Bestimme zuerst jemand anderen.");
        }

        entry.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        // Nach dem Speichern: wer sonst nirgends Trainer:in ist, verliert das
        // Kennzeichen wieder.
        await trainerRoles.SyncAsync(userId, ct);
        return Result.Success();
    }

    /// <summary>
    /// Admin-Weg, ein Mitglied direkt (ohne Beitrittsanfrage-Workflow) in
    /// einen Verein aufzunehmen. Nutzt dieselbe ClubMembership-Tabelle wie
    /// der Antrag-Weg, setzt Status aber sofort auf <see cref="ClubMembershipStatus.Approved"/>.
    /// </summary>
    public async Task<Result> AddMemberAsync(Guid callerId, bool isAdmin, Guid clubId, AssignClubMemberRequest request, CancellationToken ct = default)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result.NotFound("Verein nicht gefunden.");

        // Die Prüfung gehört hierher und nicht in den Controller: Der Aufruf
        // hängt jetzt an zwei Routen (Admin und Verein), und eine Regel, die
        // an der Route klebt, geht beim Hinzufügen der zweiten verloren.
        if (!isAdmin && !await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

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
            return Result.NotFound("Mitgliedschaft nicht gefunden.");

        membership.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await DetachFromClubAsync(clubId, userId, ct);
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
            return Result<ClubMembershipDto>.NotFound("Verein nicht gefunden.");

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
            return Result<IReadOnlyList<ClubMemberDto>>.NotFound("Verein nicht gefunden.");

        var pending = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Pending)
            .ToListAsync(ct);

        var trainerIds = await TrainerIdsAsync(clubId, ct);
        var lookup = await userLookup.FindByIdsAsync(pending.Select(m => m.UserId).ToList(), ct);
        var dtos = pending
            .Select(m => ZuMitglied(m, lookup, trainerIds))
            .ToList();

        return Result<IReadOnlyList<ClubMemberDto>>.Success(dtos);
    }

    public async Task<Result> DecideJoinRequestAsync(Guid callerId, Guid clubId, Guid membershipId, bool approve, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var membership = await db.ClubMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.ClubId == clubId, ct);
        if (membership is null || membership.Status != ClubMembershipStatus.Pending)
            return Result.NotFound("Beitrittsanfrage nicht gefunden.");

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

    /// <summary>
    /// Alles lösen, was an der Vereinszugehörigkeit hängt.
    ///
    /// Vorher wurde beim Austritt nur die ClubMembership weich gelöscht: Wer
    /// den Verein verließ, blieb Mitglied seiner Trainingsgruppen, stand
    /// weiter in der Mitgliederliste der Trainer:innen, behielt eine etwaige
    /// Trainer-Zuweisung des Vereins - und vor allem behielten dessen
    /// Trainer:innen weiter Zugriff auf Tagebuch, Ziele und Trainingsplan
    /// seiner Hunde.
    ///
    /// Gelöst werden deshalb:
    /// - Mitgliedschaften in allen Gruppen dieses Vereins,
    /// - eine Mit-Betreuung von Gruppen dieses Vereins,
    /// - die Trainer-Zuweisung zum Verein selbst,
    /// - Betreuungen der eigenen Hunde durch Trainer:innen dieses Vereins,
    /// - Betreuungen, die diese Person bei Mitgliedern dieses Vereins hält.
    ///
    /// Die Trainingsdaten selbst bleiben unangetastet - sie gehören dem
    /// Besitzer, nicht dem Verein.
    /// </summary>
    public async Task PurgeUserAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var m in await db.ClubMemberships.Where(m => m.UserId == userId).ToListAsync(ct))
            m.DeletedAt = now;
        foreach (var t in await db.ClubTrainers.Where(t => t.UserId == userId).ToListAsync(ct))
            t.DeletedAt = now;
        foreach (var m in await db.GroupMembers.Where(m => m.UserId == userId).ToListAsync(ct))
            m.DeletedAt = now;
        foreach (var t in await db.GroupTrainers.Where(t => t.UserId == userId).ToListAsync(ct))
            t.DeletedAt = now;
        foreach (var a in await db.TrainerAssignments
                     .Where(a => a.TrainerId == userId || a.MemberId == userId).ToListAsync(ct))
            a.DeletedAt = now;

        await db.SaveChangesAsync(ct);
    }

    private async Task DetachFromClubAsync(Guid clubId, Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var groupIds = await db.Groups.Where(g => g.ClubId == clubId).Select(g => g.Id).ToListAsync(ct);

        var memberships = await db.GroupMembers
            .Where(m => m.UserId == userId && groupIds.Contains(m.GroupId))
            .ToListAsync(ct);
        foreach (var m in memberships) m.DeletedAt = now;

        var coTrainerRows = await db.GroupTrainers
            .Where(t => t.UserId == userId && groupIds.Contains(t.GroupId))
            .ToListAsync(ct);
        foreach (var t in coTrainerRows) t.DeletedAt = now;

        var clubTrainer = await db.ClubTrainers.FirstOrDefaultAsync(t => t.ClubId == clubId && t.UserId == userId, ct);
        if (clubTrainer is not null) clubTrainer.DeletedAt = now;

        var clubTrainerIds = await db.ClubTrainers
            .Where(t => t.ClubId == clubId)
            .Select(t => t.UserId)
            .ToListAsync(ct);
        var clubMemberIds = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Approved)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var myDogIds = await db.DogOwners.Where(o => o.UserId == userId).Select(o => o.DogId).ToListAsync(ct);

        var assignments = await db.TrainerAssignments
            .Where(a =>
                // Trainer:innen dieses Vereins verlieren den Zugriff auf meine Hunde ...
                (myDogIds.Contains(a.DogId) && clubTrainerIds.Contains(a.TrainerId))
                // ... und ich verliere den Zugriff auf die Hunde der Vereinsmitglieder.
                || (a.TrainerId == userId && clubMemberIds.Contains(a.MemberId)))
            .ToListAsync(ct);
        foreach (var a in assignments) a.DeletedAt = now;

        await db.SaveChangesAsync(ct);
        // Wer dadurch nirgends mehr Trainer:in ist, verliert das Kennzeichen.
        await trainerRoles.SyncAsync(userId, ct);
    }

    public async Task<Result> LeaveClubAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        var membership = await db.ClubMemberships
            .FirstOrDefaultAsync(m => m.ClubId == clubId && m.UserId == userId && m.Status == ClubMembershipStatus.Approved, ct);
        if (membership is null)
            return Result.Failure("Keine aktive Mitgliedschaft gefunden.");

        membership.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await DetachFromClubAsync(clubId, userId, ct);
        return Result.Success();
    }

    /// <summary>Die Nutzer-Ids aller Trainer:innen eines Vereins.</summary>
    private async Task<IReadOnlySet<Guid>> TrainerIdsAsync(Guid clubId, CancellationToken ct) =>
        (await db.ClubTrainers
            .Where(t => t.ClubId == clubId)
            .Select(t => t.UserId)
            .ToListAsync(ct))
        .ToHashSet();

    /// <summary>
    /// Baut die Mitgliederzeile - inklusive der Frage, ob die Person zugleich
    /// Trainer:in des Vereins ist.
    ///
    /// Eine Stelle, weil es vorher dreimal wortgleich dastand (Vereinsdetail,
    /// Beitrittsanfragen, Mitgliederliste) und ein neues Feld sonst an zwei
    /// davon vergessen worden wäre.
    /// </summary>
    private static ClubMemberDto ZuMitglied(
        ClubMembership m,
        IReadOnlyDictionary<Guid, UserLookupResult> lookup,
        IReadOnlySet<Guid> trainerIds)
    {
        var istTrainer = trainerIds.Contains(m.UserId);
        return lookup.TryGetValue(m.UserId, out var info)
            ? new ClubMemberDto(m.Id, m.UserId, info.Email, info.FirstName, info.LastName, m.RequestedAt, m.DecidedAt, istTrainer)
            : new ClubMemberDto(m.Id, m.UserId, "(unbekannt)", "", "", m.RequestedAt, m.DecidedAt, istTrainer);
    }

    public async Task<Result<IReadOnlyList<ClubMemberDto>>> GetMembersAsync(Guid callerId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result<IReadOnlyList<ClubMemberDto>>.NotFound("Verein nicht gefunden.");

        var members = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Approved)
            .ToListAsync(ct);

        var trainerIds = await TrainerIdsAsync(clubId, ct);
        var lookup = await userLookup.FindByIdsAsync(members.Select(m => m.UserId).ToList(), ct);
        var dtos = members
            .Select(m => ZuMitglied(m, lookup, trainerIds))
            .ToList();

        return Result<IReadOnlyList<ClubMemberDto>>.Success(dtos);
    }

    public async Task<Result> PromoteMemberToTrainerAsync(Guid callerId, Guid clubId, Guid targetUserId, CancellationToken ct = default)
    {
        // Trainer:innen zu berufen ist eine verwaltende Handlung - egal ob
        // über die E-Mail-Zuweisung oder über die Beförderung eines
        // Mitglieds. Vorher genügte hier "ist Trainer", während der andere
        // Weg strenger war; damit wäre dieselbe Befugnis je nach Weg
        // unterschiedlich streng gewesen.
        if (!await db.CanManageClubAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var isApprovedMember = await db.ClubMemberships
            .AnyAsync(m => m.ClubId == clubId && m.UserId == targetUserId && m.Status == ClubMembershipStatus.Approved, ct);
        if (!isApprovedMember)
            return Result.Failure("Nur bestehende Mitglieder dieses Vereins können zu Trainern gemacht werden.");

        var (existingTrainer, isActiveTrainer) = await db.ClubTrainers
            .FindIncludingRemovedAsync(t => t.ClubId == clubId && t.UserId == targetUserId, ct);
        if (isActiveTrainer)
            return Result.Failure("Dieser Benutzer ist bereits Trainer dieses Vereins.");

        if (existingTrainer is not null) existingTrainer.DeletedAt = null;
        else db.ClubTrainers.Add(new ClubTrainer { ClubId = clubId, UserId = targetUserId });

        await db.SaveChangesAsync(ct);
        await trainerRoles.SyncAsync(targetUserId, ct);

        var club = await db.Clubs.AsNoTracking().FirstAsync(c => c.Id == clubId, ct);
        await notifications.CreateAsync(targetUserId, $"Du bist jetzt Trainer bei \"{club.Name}\".", "/trainer", ct);

        return Result.Success();
    }
}
