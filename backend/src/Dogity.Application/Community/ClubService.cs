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
            // Dieselbe Definition wie CanManageClubAsync (Rolle Verwaltung), hier
            // als Teil der Abfrage statt einer weiteren je Verein.
            .Select(c => new ClubDto(c.Id, c.Name, c.Description, c.Trainers.Count, c.Groups.Count,
                c.Trainers.Any(t => t.UserId == userId && t.Role == ClubRole.Verwaltung)))
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

        // Eine Vereinsverwaltung beruft nur aus den eigenen Mitgliedern. Sonst
        // genügte die E-Mail-Adresse irgendeiner registrierten Person, um sie
        // ohne ihr Zutun zur Trainer:in zu machen - mit Einblick in alle
        // Mitglieder und, über die Betreuung, in deren Hunde. Wer noch nicht
        // dazugehört, wird zuerst eingeladen und muss zusagen. Nur der Betreiber
        // (Admin) legt den ersten Trainer eines neuen Vereins ohne Mitglieder an.
        if (!isAdmin && !await IstFreigegebenesMitgliedAsync(clubId, user.UserId, ct))
            return Result.Failure("Nur Mitglieder dieses Vereins können Trainer:in werden. Lade die Person zuerst in den Verein ein.");

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
    /// Jemanden per E-Mail-Adresse in den Verein holen.
    ///
    /// Der Betreiber (Admin) nimmt direkt auf - Status sofort
    /// <see cref="ClubMembershipStatus.Approved"/>. Eine Vereinstrainer:in
    /// LÄDT EIN (<see cref="ClubMembershipStatus.Invited"/>): Mitglied wird die
    /// Person erst mit ihrer Zusage. Vorher nahm jede Trainer:in jede
    /// registrierte Person sofort auf und sah danach deren Namen und
    /// E-Mail-Adresse. Bis zur Zusage sieht der Verein nur die Adresse, die er
    /// selbst eingegeben hat (siehe <see cref="GetInvitationsAsync"/>).
    ///
    /// Hat die Person selbst schon um Aufnahme gebeten, wollen es beide
    /// Seiten - dann wird die Anfrage gleich freigegeben.
    /// </summary>
    /// <summary>Wie lange nach einem Nein derselbe Verein nicht erneut einladen kann (siehe AddMemberAsync).</summary>
    public static readonly TimeSpan EinladungsSperreNachNein = TimeSpan.FromDays(30);

    public async Task<Result<AddClubMemberResultDto>> AddMemberAsync(Guid callerId, bool isAdmin, Guid clubId, AssignClubMemberRequest request, CancellationToken ct = default)
    {
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Id == clubId, ct);
        if (club is null)
            return Result<AddClubMemberResultDto>.NotFound("Verein nicht gefunden.");

        // Die Prüfung gehört hierher und nicht in den Controller: Der Aufruf
        // hängt jetzt an zwei Routen (Admin und Verein), und eine Regel, die
        // an der Route klebt, geht beim Hinzufügen der zweiten verloren.
        if (!isAdmin && !await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result<AddClubMemberResultDto>.NotFound("Verein nicht gefunden.");

        var user = await userLookup.FindByEmailAsync(request.Email, ct);
        if (user is null)
            return Result<AddClubMemberResultDto>.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        var existing = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.UserId == user.UserId)
            .OrderByDescending(m => m.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null && existing.Status == ClubMembershipStatus.Approved)
            return Result<AddClubMemberResultDto>.Failure("Dieser Benutzer ist bereits Mitglied dieses Vereins.");

        if (!isAdmin && existing is not null && existing.Status == ClubMembershipStatus.Invited)
            return Result<AddClubMemberResultDto>.Failure("Diese Person ist bereits eingeladen.");

        // Wer eine Einladung dieses Vereins abgelehnt hat, bekommt eine Weile
        // keine neue. Sonst könnte ein Verein - auch ein fremder, dem nur die
        // Adresse bekannt ist - dieselbe Person nach jedem Nein sofort wieder
        // einladen und sie mit Benachrichtigungen zuschütten. Selbst beitreten
        // kann sie jederzeit (Anfrage oder Einladungslink).
        if (!isAdmin
            && existing is { Status: ClubMembershipStatus.Rejected, Source: ClubMembershipSource.Invitation, DecidedAt: { } abgelehntAm }
            && abgelehntAm > DateTimeOffset.UtcNow - EinladungsSperreNachNein)
            return Result<AddClubMemberResultDto>.Failure("Diese Person hat eine Einladung eures Vereins vor Kurzem abgelehnt. Beitreten kann sie jederzeit selbst, etwa über euren Einladungslink.");

        // Direkt aufnehmen darf nur der Betreiber - oder wer eine Person freigibt,
        // die selbst angefragt hat (Pending): Dann haben beide Seiten zugestimmt.
        var hatSelbstAngefragt = existing is not null && existing.Status == ClubMembershipStatus.Pending;
        if (isAdmin || hatSelbstAngefragt)
        {
            // Bestehende Anfrage (oder, beim Betreiber, Einladung) direkt genehmigen
            // statt eine zweite Zeile anzulegen.
            if (existing is not null && (existing.Status == ClubMembershipStatus.Pending || existing.Status == ClubMembershipStatus.Invited))
            {
                existing.Status = ClubMembershipStatus.Approved;
                existing.DecidedAt = DateTimeOffset.UtcNow;
                existing.DecidedByUserId = callerId;
                existing.InvitedEmail = null;
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
            // Mail: Die Aufnahme geschah ohne Zutun der Person - sie soll es erfahren.
            await notifications.CreateAsync(user.UserId, Meldung.Von("Du wurdest zum Verein \"{verein}\" hinzugefügt.", ("verein", club.Name)), "/clubs", ct, wichtig: true);
            return Result<AddClubMemberResultDto>.Success(new AddClubMemberResultDto(Joined: true));
        }

        db.ClubMemberships.Add(new ClubMembership
        {
            ClubId = clubId,
            UserId = user.UserId,
            Status = ClubMembershipStatus.Invited,
            Source = ClubMembershipSource.Invitation,
            InvitedEmail = request.Email.Trim(),
            RequestedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var einladende = await userLookup.FindByIdsAsync([callerId], ct);
        var name = einladende.TryGetValue(callerId, out var info) && !string.IsNullOrWhiteSpace($"{info.FirstName} {info.LastName}")
            ? $"{info.FirstName} {info.LastName}".Trim()
            : null;
        var einladungsMeldung = name is null
            ? Meldung.Von("Eine Trainer:in lädt dich in den Verein \"{verein}\" ein.", ("verein", club.Name))
            : Meldung.Von("{name} lädt dich in den Verein \"{verein}\" ein.", ("name", name), ("verein", club.Name));
        // Mail: Eine Einladung wartet auf eine Antwort - die Person soll sie nicht erst in der App finden müssen.
        await notifications.CreateAsync(user.UserId, einladungsMeldung, "/clubs", ct, wichtig: true);
        return Result<AddClubMemberResultDto>.Success(new AddClubMemberResultDto(Joined: false));
    }

    /// <summary>
    /// Die offenen Einladungen eines Vereins - für dessen Trainer:innen.
    /// Bewusst nur Adresse und Zeitpunkt: Vor der Zusage gehört der Name der
    /// eingeladenen Person ihr (eigenes DTO statt <see cref="ClubMemberDto"/>,
    /// damit ein Name hier gar nicht erst mitgeliefert werden kann).
    /// </summary>
    public async Task<Result<IReadOnlyList<ClubInvitationDto>>> GetInvitationsAsync(Guid callerId, Guid clubId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result<IReadOnlyList<ClubInvitationDto>>.NotFound("Verein nicht gefunden.");

        var einladungen = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.Status == ClubMembershipStatus.Invited)
            .OrderBy(m => m.RequestedAt)
            .Select(m => new ClubInvitationDto(m.Id, m.InvitedEmail ?? "", m.RequestedAt))
            .AsNoTracking()
            .ToListAsync(ct);

        return Result<IReadOnlyList<ClubInvitationDto>>.Success(einladungen);
    }

    /// <summary>Eine offene Einladung zurückziehen - nur durch Trainer:innen dieses Vereins.</summary>
    public async Task<Result> WithdrawInvitationAsync(Guid callerId, Guid clubId, Guid membershipId, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        // Nur Einladungen: Eine Mitgliedschaft oder Anfrage zieht niemand über
        // diesen Weg zurück.
        var einladung = await db.ClubMemberships.FirstOrDefaultAsync(
            m => m.Id == membershipId && m.ClubId == clubId && m.Status == ClubMembershipStatus.Invited, ct);
        if (einladung is null)
            return Result.NotFound("Einladung nicht gefunden.");

        einladung.DeletedAt = DateTimeOffset.UtcNow;
        // Die eingegebene Adresse wird nicht länger gebraucht.
        einladung.InvitedEmail = null;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Die Einladung in einen Verein annehmen oder ablehnen - nur die eigene.</summary>
    public async Task<Result> RespondToInvitationAsync(Guid userId, Guid clubId, bool accept, CancellationToken ct = default)
    {
        // Alle offenen Einladungen dieses Vereins: Zwei fast gleichzeitige Einladungen
        // (Doppelklick, zwei Trainer:innen) legen zwei Zeilen an, und eine übrig
        // gebliebene bliebe sonst als Einladung stehen, obwohl die Person längst
        // geantwortet hat.
        var offene = await db.ClubMemberships
            .Where(m => m.ClubId == clubId && m.UserId == userId && m.Status == ClubMembershipStatus.Invited)
            .OrderByDescending(m => m.RequestedAt)
            .ToListAsync(ct);
        if (offene.Count == 0)
            return Result.NotFound("Einladung nicht gefunden.");

        // Abgelehnt bleibt als Rejected stehen (wie eine abgelehnte Anfrage),
        // die eingegebene Adresse wird in beiden Fällen gelöscht.
        var einladung = offene[0];
        einladung.Status = accept ? ClubMembershipStatus.Approved : ClubMembershipStatus.Rejected;
        einladung.DecidedAt = DateTimeOffset.UtcNow;
        einladung.InvitedEmail = null;
        foreach (var doppelt in offene.Skip(1))
        {
            doppelt.DeletedAt = DateTimeOffset.UtcNow;
            doppelt.InvitedEmail = null;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<(Guid ClubId, string ClubName)>> GetMyOpenInvitationsAsync(Guid userId, CancellationToken ct = default)
    {
        // Die Startseite fragt das bei jedem Öffnen: nur die zwei Felder der Karte.
        var rows = await db.ClubMemberships
            .Where(m => m.UserId == userId && m.Status == ClubMembershipStatus.Invited)
            .OrderBy(m => m.RequestedAt)
            .Select(m => new { m.ClubId, ClubName = m.Club!.Name })
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.Select(r => (r.ClubId, r.ClubName)).ToList();
    }

    private Task<bool> IstFreigegebenesMitgliedAsync(Guid clubId, Guid userId, CancellationToken ct) =>
        db.ClubMemberships.AnyAsync(m => m.ClubId == clubId && m.UserId == userId && m.Status == ClubMembershipStatus.Approved, ct);

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

    public async Task<Result<IReadOnlyList<ClubSummaryDto>>> GetBrowsableClubsAsync(Guid userId, CancellationToken ct = default)
    {
        var clubs = await db.Clubs
            // Vereinstrainer:innen haben keine ClubMembership (siehe
            // ClubAccessQueries.BelongsToAnyClubAsync) - ohne dieses Kennzeichen
            // bot die Liste ihnen bei ihrem eigenen Verein "Beitreten" an.
            .Select(c => new ClubSummaryDto(c.Id, c.Name, c.Description, c.Trainers.Any(t => t.UserId == userId)))
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

    public async Task<Result<ClubMembershipDto>> RequestJoinAsync(Guid userId, Guid clubId, ClubMembershipSource source = ClubMembershipSource.Directory, CancellationToken ct = default)
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

        // Der Verein hat die Person schon eingeladen und sie fragt jetzt selbst
        // an (Liste oder Einladungslink): beide Seiten wollen es - gleich
        // aufnehmen, statt eine zweite Zeile mit einer Anfrage anzulegen, die
        // dann noch jemand freigeben müsste.
        if (existing is not null && existing.Status == ClubMembershipStatus.Invited)
        {
            existing.Status = ClubMembershipStatus.Approved;
            existing.DecidedAt = DateTimeOffset.UtcNow;
            existing.InvitedEmail = null;
            await db.SaveChangesAsync(ct);
            return Result<ClubMembershipDto>.Success(new ClubMembershipDto(existing.Id, clubId, club.Name, existing.Status, existing.RequestedAt, existing.DecidedAt));
        }

        var membership = new ClubMembership { ClubId = clubId, UserId = userId, Source = source };
        db.ClubMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        // Ohne Hinweis bliebe eine Anfrage liegen, bis zufällig jemand die
        // Trainer-Seite öffnet - und über den Aushang am Vereinsheim kommen
        // Anfragen gerade dann, wenn niemand danach schaut.
        var trainerIds = await db.ClubTrainers
            .Where(t => t.ClubId == clubId && t.UserId != userId)
            .Select(t => t.UserId)
            .ToListAsync(ct);
        if (trainerIds.Count > 0)
        {
            // Mit Namen: Wer die Glocke öffnet, soll ohne Umweg sehen, wer wartet.
            var anfragende = await userLookup.FindByIdsAsync([userId], ct);
            var name = anfragende.TryGetValue(userId, out var info) && !string.IsNullOrWhiteSpace(info.FirstName)
                ? $"{info.FirstName} {info.LastName}".Trim()
                : null;
            var anfrage = name is null
                ? Meldung.Von("Jemand möchte dem Verein \"{verein}\" beitreten.", ("verein", club.Name))
                : Meldung.Von("{name} möchte dem Verein \"{verein}\" beitreten.", ("name", name), ("verein", club.Name));
            foreach (var trainerId in trainerIds)
                await notifications.CreateAsync(trainerId, anfrage, "/trainer/anfragen", ct);
        }

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

    public async Task<Result> DecideJoinRequestAsync(Guid callerId, Guid clubId, Guid membershipId, bool approve, Guid? groupId = null, CancellationToken ct = default)
    {
        if (!await db.IsClubTrainerAsync(callerId, clubId, ct))
            return Result.NotFound("Verein nicht gefunden.");

        var membership = await db.ClubMemberships.FirstOrDefaultAsync(m => m.Id == membershipId && m.ClubId == clubId, ct);
        if (membership is null || membership.Status != ClubMembershipStatus.Pending)
            return Result.NotFound("Beitrittsanfrage nicht gefunden.");

        // Die Gruppe wird VOR jeder Änderung geprüft: Scheitert sie, bleibt auch
        // der Vereinsbeitritt offen (alles oder nichts). Beim Ablehnen zählt sie nicht.
        Group? group = null;
        if (approve && groupId is { } gruppenId)
        {
            // Dieselbe Rechteprüfung wie bei jeder Gruppenverwaltung.
            group = await db.GetManageableGroupAsync(callerId, gruppenId, ct);
            if (group is null)
                return Result.NotFound("Gruppe nicht gefunden.");
            if (group.ClubId != clubId)
                return Result.Failure("Diese Gruppe gehört nicht zu diesem Verein.");
        }

        membership.Status = approve ? ClubMembershipStatus.Approved : ClubMembershipStatus.Rejected;
        membership.DecidedAt = DateTimeOffset.UtcNow;
        membership.DecidedByUserId = callerId;

        // Eine Einladung, keine Aufnahme: Die Vereinsanfrage ist keine
        // Zustimmung zur Gruppe (siehe GroupInvitations). Beides wird in einem
        // Rutsch gespeichert.
        var einladung = GroupInvitationOutcome.AlreadyMember;
        if (group is not null)
        {
            var nutzer = await userLookup.FindByIdsAsync([membership.UserId], ct);
            var mail = nutzer.TryGetValue(membership.UserId, out var info) ? info.Email : null;
            einladung = await GroupInvitations.PrepareAsync(db, group.Id, membership.UserId, mail, ct);
        }

        await db.SaveChangesAsync(ct);

        var club = await db.Clubs.AsNoTracking().FirstAsync(c => c.Id == clubId, ct);
        // EINE Meldung statt zwei: Verein und Gruppe in einem Satz, mit Link auf
        // die Stelle, an der man annimmt (Karte auf der Startseite).
        var message = !approve
            ? Meldung.Von("Dein Beitritt zu \"{verein}\" wurde abgelehnt.", ("verein", club.Name))
            : group is null || einladung == GroupInvitationOutcome.AlreadyMember
                ? Meldung.Von("Dein Beitritt zu \"{verein}\" wurde angenommen.", ("verein", club.Name))
                : einladung == GroupInvitationOutcome.JoinedDirectly
                    ? Meldung.Von("Dein Beitritt zu \"{verein}\" wurde angenommen. Du bist jetzt Mitglied der Gruppe \"{gruppe}\".", ("verein", club.Name), ("gruppe", group.Name))
                    : Meldung.Von("Dein Beitritt zu \"{verein}\" wurde angenommen. Du bist in die Gruppe \"{gruppe}\" eingeladen - einmal tippen zum Annehmen.", ("verein", club.Name), ("gruppe", group.Name));
        var link = einladung is GroupInvitationOutcome.Invited or GroupInvitationOutcome.AlreadyInvited && group is not null
            ? "/dashboard"
            : "/clubs";
        // Mail: Wer um Aufnahme gebeten hat, wartet auf genau diese Antwort (angenommen wie abgelehnt).
        await notifications.CreateAsync(membership.UserId, message, link, ct, wichtig: true);

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
    private async Task DetachFromClubAsync(Guid clubId, Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var groupIds = await db.Groups.Where(g => g.ClubId == clubId).Select(g => g.Id).ToListAsync(ct);

        var memberships = await db.GroupMembers
            .Where(m => m.UserId == userId && groupIds.Contains(m.GroupId))
            .ToListAsync(ct);
        foreach (var m in memberships) m.DeletedAt = now;

        // Einschließlich offener Einladungen - wer den Verein verlässt, soll
        // eine liegengebliebene Einladung nicht später noch annehmen können.
        var coTrainerRows = await db.GroupTrainers
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.DeletedAt == null && groupIds.Contains(t.GroupId))
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
            ? new ClubMemberDto(m.Id, m.UserId, info.Email, info.FirstName, info.LastName, m.RequestedAt, m.DecidedAt, istTrainer, m.Source)
            : new ClubMemberDto(m.Id, m.UserId, "(unbekannt)", "", "", m.RequestedAt, m.DecidedAt, istTrainer, m.Source);
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
        await notifications.CreateAsync(targetUserId, Meldung.Von("Du bist jetzt Trainer bei \"{verein}\".", ("verein", club.Name)), "/trainer", ct);

        return Result.Success();
    }
}
