using System.Data.Common;
using System.Globalization;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Community;

/// <summary>
/// Terminplanung fürs Gruppentraining (siehe docs/GROUP_TRAINING_SCHEDULE.md).
/// ClubTrainer planen/bearbeiten Termine des Vereins; Mitglieder sehen die
/// Termine ihrer Gruppen read-only. Inhalt = geordnete Bausteine und/oder
/// Freitext; mehrere zuständige Trainer:innen je Termin. Mitglieder können
/// zu- oder absagen; Namen dazu sehen nur die Trainer:innen.
/// </summary>
public class GroupTrainingScheduleService(IApplicationDbContext db, IUserLookupService userLookup, INotificationService notifications) : IGroupTrainingScheduleService
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
        return Result<IReadOnlyList<GroupTrainingSessionDto>>.Success(await MapAsync(sessions, userId, ct));
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
        return Result<IReadOnlyList<GroupTrainingSessionDto>>.Success(await MapAsync(sessions, userId, ct));
    }

    public async Task<Result<GroupTrainingSessionDto>> RespondAsync(Guid userId, Guid sessionId, bool attending, CancellationToken ct = default)
    {
        var session = await db.GroupTrainingSessions
            .Include(s => s.Group).Include(s => s.Trainers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        // Wer nicht (mehr) aktives Mitglied der Gruppe ist, bekommt dieselbe
        // Antwort wie bei einem Termin, den es nicht gibt - dass er existiert,
        // geht Fremde nichts an.
        var istMitglied = session?.Group is not null && await db.GroupMembers.AnyAsync(
            m => m.GroupId == session.GroupId && m.UserId == userId && m.Status == GroupMemberStatus.Active, ct);
        if (session is null || !istMitglied)
            return Result<GroupTrainingSessionDto>.NotFound("Termin nicht gefunden.");

        if (session.Status != GroupTrainingSessionStatus.Planned)
            return Result<GroupTrainingSessionDto>.Failure("Dieser Termin ist abgesagt.");
        var jetzt = DateTimeOffset.UtcNow;
        if (session.StartsAt <= jetzt)
            return Result<GroupTrainingSessionDto>.Failure("Der Termin hat schon begonnen.");

        // Auch entfernte Zeilen ansehen (Soft-Delete + eindeutiger Index), sonst
        // gäbe es beim zweiten Antworten ein Duplikat bzw. einen 500er.
        var (antwort, istAktiv) = await db.GroupTrainingSessionResponses
            .FindIncludingRemovedAsync(r => r.GroupTrainingSessionId == sessionId && r.UserId == userId, ct);

        // Nur eine NEUE Absage oder der Wechsel von Zusage auf Absage ist eine
        // Nachricht an die Trainer:innen wert - dieselbe Absage noch einmal zu
        // senden, meldet nichts Neues. Zusagen lösen nie eine aus.
        var meldeAbsage = !attending && (antwort is null || !istAktiv || antwort.IsAttending);

        if (antwort is null)
        {
            var neu = new GroupTrainingSessionResponse
            {
                GroupTrainingSessionId = sessionId,
                UserId = userId,
                IsAttending = attending,
                RespondedAt = jetzt
            };
            db.GroupTrainingSessionResponses.Add(neu);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: EindeutigerIndexVerletzt })
            {
                // Zwei erste Antworten derselben Person laufen gleichzeitig ein
                // (zwei Geräte, Doppel-Tipp): Die andere war schneller, der
                // eindeutige Index weist unsere Zeile ab. Kein Fehler - wir
                // verwerfen die eigene Zeile und behandeln die Antwort wie
                // eine Änderung der gerade angelegten. Ob das eine Absage
                // meldet, hängt so vom Stand der anderen Anfrage ab: gleiche
                // Antwort, keine zweite Nachricht; Wechsel, eine.
                db.GroupTrainingSessionResponses.Remove(neu);
                var (vorhanden, _) = await db.GroupTrainingSessionResponses
                    .FindIncludingRemovedAsync(r => r.GroupTrainingSessionId == sessionId && r.UserId == userId, ct);
                if (vorhanden is null) throw;
                meldeAbsage = !attending && vorhanden.IsAttending;
                AktualisiereAntwort(vorhanden, attending, jetzt);
                await db.SaveChangesAsync(ct);
            }
        }
        else
        {
            AktualisiereAntwort(antwort, attending, jetzt);
            await db.SaveChangesAsync(ct);
        }

        if (meldeAbsage)
            await NotifyAbsageAsync(session, userId, ct);

        return Result<GroupTrainingSessionDto>.Success(await LoadDtoAsync(sessionId, userId, ct));
    }

    /// <summary>SQLSTATE "unique_violation" (Postgres).</summary>
    private const string EindeutigerIndexVerletzt = "23505";

    private static void AktualisiereAntwort(GroupTrainingSessionResponse antwort, bool attending, DateTimeOffset jetzt)
    {
        antwort.DeletedAt = null;
        antwort.IsAttending = attending;
        antwort.RespondedAt = jetzt;
        antwort.UpdatedAt = jetzt;
    }

    /// <summary>
    /// Sagt ein Mitglied ab, erfahren es die Trainer:innen des Termins; ist
    /// keine eingetragen, die aktiven Trainer:innen der Gruppe. Mit Link auf
    /// die Terminübersicht, wo die Zählung steht.
    /// </summary>
    private async Task NotifyAbsageAsync(GroupTrainingSession session, Guid userId, CancellationToken ct)
    {
        var empfaenger = session.Trainers.Select(t => t.UserId).ToList();
        if (empfaenger.Count == 0)
        {
            // Der globale Filter blendet offene Einladungen schon aus.
            empfaenger = await db.GroupTrainers.Where(t => t.GroupId == session.GroupId).Select(t => t.UserId).ToListAsync(ct);
            empfaenger.Add(session.Group!.TrainerId);
        }
        empfaenger = empfaenger.Where(id => id != userId).Distinct().ToList();
        if (empfaenger.Count == 0) return;

        var namen = await userLookup.FindByIdsAsync([userId], ct);
        var vorname = namen.TryGetValue(userId, out var n) && !string.IsNullOrWhiteSpace(n.FirstName) ? n.FirstName : "Ein Mitglied";
        var text = $"{vorname} hat für {session.Group!.Name} am {FormatDatum(session.StartsAt)} abgesagt.";
        foreach (var id in empfaenger)
            await notifications.CreateAsync(id, text, "/trainer/schedule", ct);
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
        return Result<GroupTrainingSessionDto>.Success(await LoadDtoAsync(session.Id, userId, ct));
    }

    public async Task<Result<GroupTrainingSessionDto>> UpdateSessionAsync(Guid userId, Guid sessionId, UpdateSessionRequest request, CancellationToken ct = default)
    {
        var session = await db.GroupTrainingSessions
            .Include(s => s.Items).Include(s => s.Trainers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !await IsClubTrainerAsync(userId, session.ClubId, ct))
            return Result<GroupTrainingSessionDto>.NotFound("Termin nicht gefunden.");
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
        return Result<GroupTrainingSessionDto>.Success(await LoadDtoAsync(session.Id, userId, ct));
    }

    public async Task<Result> CancelSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.GroupTrainingSessions.Include(s => s.Group).FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !await IsClubTrainerAsync(userId, session.ClubId, ct))
            return Result.NotFound("Termin nicht gefunden.");
        // Schon abgesagt: nichts mehr zu tun - und vor allem keine zweite
        // "fällt aus"-Nachricht an dieselben Leute.
        if (session.Status == GroupTrainingSessionStatus.Cancelled) return Result.Success();

        var jetzt = DateTimeOffset.UtcNow;
        session.Status = GroupTrainingSessionStatus.Cancelled;
        session.UpdatedAt = jetzt;
        await db.SaveChangesAsync(ct);

        // Wer zugesagt hatte, plant mit dem Termin - und erfährt es, statt vor
        // verschlossener Tür zu stehen. Ein schon vergangener Termin braucht
        // keine Nachricht mehr; ebenso nicht, wer nicht mehr in der Gruppe ist.
        if (session.StartsAt > jetzt)
        {
            var zugesagt = await db.GroupTrainingSessionResponses
                .Where(r => r.GroupTrainingSessionId == sessionId && r.IsAttending)
                .Select(r => r.UserId)
                .ToListAsync(ct);
            var empfaenger = await db.GroupMembers
                .Where(m => m.GroupId == session.GroupId && m.Status == GroupMemberStatus.Active && zugesagt.Contains(m.UserId))
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync(ct);
            var text = $"Das Training {session.Group?.Name} am {FormatDatum(session.StartsAt)} fällt aus.";
            foreach (var id in empfaenger.Where(id => id != userId))
                await notifications.CreateAsync(id, text, "/", ct);
        }
        return Result.Success();
    }

    public async Task<Result> DeleteSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.GroupTrainingSessions
            .Include(s => s.Items).Include(s => s.Trainers)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || !await IsClubTrainerAsync(userId, session.ClubId, ct))
            return Result.NotFound("Termin nicht gefunden.");
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
        // Für "je Termin generieren" den Baustein-Pool der Kategorie einmal laden.
        var pool = request.AutoGenerateContent
            ? await db.GroupTrainingExercises.Where(e => e.ClubId == clubId && e.Category == request.Category).AsNoTracking().ToListAsync(ct)
            : new List<GroupTrainingExercise>();

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

            var items = request.AutoGenerateContent
                ? GroupTrainingMixGenerator.Generate(request.Category, pool, Random.Shared)
                    .Select((e, idx) => new GroupTrainingSessionItem { GroupTrainingSessionId = session.Id, GroupTrainingExerciseId = e.Id, SortOrder = idx })
                    .ToList()
                : BuildItems(session.Id, request.Items);
            db.GroupTrainingSessionItems.AddRange(items);
            db.GroupTrainingSessionTrainers.AddRange(BuildTrainers(session.Id, request.TrainerUserIds));
            createdIds.Add(session.Id);
        }
        await db.SaveChangesAsync(ct);

        var sessions = await LoadSessionsQuery().Where(s => createdIds.Contains(s.Id)).OrderBy(s => s.StartsAt).AsNoTracking().ToListAsync(ct);
        return Result<IReadOnlyList<GroupTrainingSessionDto>>.Success(await MapAsync(sessions, userId, ct));
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

    public async Task<Result<IReadOnlyList<SessionTrainerDto>>> GetClubTrainersAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        if (!await IsClubTrainerAsync(userId, clubId, ct))
            return Result<IReadOnlyList<SessionTrainerDto>>.Failure("Keine Trainer-Berechtigung für diesen Verein.");

        var ids = await db.ClubTrainers.Where(t => t.ClubId == clubId).Select(t => t.UserId).ToListAsync(ct);
        if (ids.Count == 0)
            return Result<IReadOnlyList<SessionTrainerDto>>.Success(Array.Empty<SessionTrainerDto>());

        var names = await userLookup.FindByIdsAsync(ids, ct);
        var list = ids
            .Select(id => names.TryGetValue(id, out var n) ? new SessionTrainerDto(id, n.FirstName, n.LastName) : new SessionTrainerDto(id, "", ""))
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .ToList();
        return Result<IReadOnlyList<SessionTrainerDto>>.Success(list);
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

    private async Task<GroupTrainingSessionDto> LoadDtoAsync(Guid sessionId, Guid callerId, CancellationToken ct)
    {
        var session = await LoadSessionsQuery().AsNoTracking().FirstAsync(s => s.Id == sessionId, ct);
        return (await MapAsync([session], callerId, ct))[0];
    }

    /// <summary>
    /// Bildet Termine auf DTOs ab. Zu- und Absagen, Gruppenmitglieder und Namen
    /// werden für ALLE Termine in je einer Abfrage geholt (kein N+1 je Termin).
    /// <paramref name="callerId"/> entscheidet über die eigene Antwort und
    /// darüber, ob Namen mitgeliefert werden.
    /// </summary>
    private async Task<IReadOnlyList<GroupTrainingSessionDto>> MapAsync(List<GroupTrainingSession> sessions, Guid callerId, CancellationToken ct)
    {
        if (sessions.Count == 0) return [];

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var groupIds = sessions.Select(s => s.GroupId).Distinct().ToList();
        var clubIds = sessions.Select(s => s.ClubId).Distinct().ToList();

        // Namen sehen die Trainer:innen des Vereins und die des Termins - nicht
        // die Mitglieder, die nur Zahlen bekommen.
        var callerClubIds = (await db.ClubTrainers
            .Where(t => t.UserId == callerId && clubIds.Contains(t.ClubId))
            .Select(t => t.ClubId)
            .ToListAsync(ct)).ToHashSet();
        bool SiehtNamen(GroupTrainingSession s) =>
            callerClubIds.Contains(s.ClubId) || s.Trainers.Any(t => t.UserId == callerId);

        // Anmeldungen zählt nur, wer die Gruppe verwalten darf - dieselbe
        // Reichweite wie GetManageableGroupAsync (Leitung, aktive Mit-
        // Trainer:in, Trainer:in des Vereins). Eine gruppierte Abfrage für
        // alle Gruppen der Termine; wer nichts verwaltet, löst gar keine aus.
        var weitereGruppen = (await db.GroupTrainers
            .Where(t => t.UserId == callerId && groupIds.Contains(t.GroupId))
            .Select(t => t.GroupId)
            .ToListAsync(ct)).ToHashSet();
        var gruppenClubIds = sessions.Where(s => s.Group?.ClubId != null).Select(s => s.Group!.ClubId!.Value).Distinct().ToList();
        var gruppenClubsAlsTrainer = (await db.ClubTrainers
            .Where(t => t.UserId == callerId && gruppenClubIds.Contains(t.ClubId))
            .Select(t => t.ClubId)
            .ToListAsync(ct)).ToHashSet();
        var verwaltbareGruppen = sessions
            .Where(s => s.Group is { } g
                && (g.TrainerId == callerId || weitereGruppen.Contains(g.Id) || (g.ClubId is { } cid && gruppenClubsAlsTrainer.Contains(cid))))
            .Select(s => s.GroupId)
            .Distinct()
            .ToList();
        var anmeldungen = verwaltbareGruppen.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.GroupRegistrations
                .Where(r => verwaltbareGruppen.Contains(r.GroupId))
                .GroupBy(r => r.GroupId)
                .Select(g => new { GroupId = g.Key, Anzahl = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Anzahl, ct);

        var responses = await db.GroupTrainingSessionResponses
            .Where(r => sessionIds.Contains(r.GroupTrainingSessionId))
            .Select(r => new { r.GroupTrainingSessionId, r.UserId, r.IsAttending })
            .AsNoTracking()
            .ToListAsync(ct);

        // Gezählt wird nur, wer JETZT aktives Mitglied der Gruppe ist: Wer
        // ausgetreten ist, soll die Zahl nicht mehr verfälschen, und die Basis
        // für "offen" ist die aktuelle Mitgliederzahl.
        var members = (await db.GroupMembers
            .Where(m => groupIds.Contains(m.GroupId) && m.Status == GroupMemberStatus.Active)
            .Select(m => new { m.GroupId, m.UserId })
            .AsNoTracking()
            .ToListAsync(ct))
            .GroupBy(m => m.GroupId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.UserId).ToHashSet());

        var responsesBySession = responses.ToLookup(r => r.GroupTrainingSessionId);

        var lookupIds = sessions.SelectMany(s => s.Trainers.Select(t => t.UserId)).ToList();
        foreach (var s in sessions.Where(SiehtNamen))
        {
            var aktive = members.GetValueOrDefault(s.GroupId) ?? [];
            lookupIds.AddRange(responsesBySession[s.Id].Where(r => aktive.Contains(r.UserId)).Select(r => r.UserId));
        }
        lookupIds = lookupIds.Distinct().ToList();
        IReadOnlyDictionary<Guid, UserLookupResult> names = lookupIds.Count == 0
            ? new Dictionary<Guid, UserLookupResult>()
            : await userLookup.FindByIdsAsync(lookupIds, ct);

        return sessions.Select(s =>
        {
            var aktive = members.GetValueOrDefault(s.GroupId) ?? [];
            var gezaehlt = responsesBySession[s.Id].Where(r => aktive.Contains(r.UserId)).ToList();
            var zusagen = gezaehlt.Count(r => r.IsAttending);
            var absagen = gezaehlt.Count - zusagen;
            bool? meine = responsesBySession[s.Id].FirstOrDefault(r => r.UserId == callerId)?.IsAttending;

            var liste = SiehtNamen(s)
                ? gezaehlt
                    .Select(r => names.TryGetValue(r.UserId, out var n)
                        ? new SessionResponseDto(r.UserId, n.FirstName, n.LastName, r.IsAttending)
                        : new SessionResponseDto(r.UserId, "", "", r.IsAttending))
                    .OrderBy(r => r.FirstName).ThenBy(r => r.LastName)
                    .ToList()
                : new List<SessionResponseDto>();

            // "Offen" ergäbe mit Zu- und Absagen die Mitgliederzahl der Gruppe -
            // die ist Sache der Trainer:innen, Mitglieder bekommen 0.
            var offen = SiehtNamen(s) ? Math.Max(0, aktive.Count - gezaehlt.Count) : 0;
            return ToDto(s, names, meine, zusagen, absagen, offen, liste, anmeldungen.GetValueOrDefault(s.GroupId));
        }).ToList();
    }

    private static GroupTrainingSessionDto ToDto(
        GroupTrainingSession s, IReadOnlyDictionary<Guid, UserLookupResult> names,
        bool? myResponse, int attending, int declining, int open, IReadOnlyList<SessionResponseDto> responses, int registrations)
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
            items, trainers,
            myResponse, attending, declining, open, responses, registrations);
    }

    private static GroupTrainingExerciseDto ToExerciseDto(GroupTrainingExercise e) =>
        new(e.Id, e.ClubId, e.Category, e.Title, e.Focus, e.DurationMinutes, e.Description, e.ExamTargets);

    private Task<bool> IsClubTrainerAsync(Guid userId, Guid clubId, CancellationToken ct) =>
        db.ClubTrainers.AnyAsync(t => t.ClubId == clubId && t.UserId == userId, ct);

    private static string FormatDatum(DateTimeOffset startsAt) =>
        TimeZoneInfo.ConvertTime(startsAt, Vereinszeit.Zone).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
