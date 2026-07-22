using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Domain.Community;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Training;

/// <summary>
/// Use Cases für das Trainingstagebuch (siehe FEATURE_MODULE.md "Training").
/// Zugriff ist immer auf Trainingseinheiten beschränkt, deren Hund dem
/// aufrufenden Benutzer über <see cref="Domain.Dogs.DogOwner"/> zugeordnet ist.
/// </summary>
public class TrainingService(IApplicationDbContext db, INotificationService notifications, IUserLookupService userLookup) : ITrainingService
{
    /// <summary>
    /// Trainings eines Hundes, optional auf einen Datumsbereich beschränkt
    /// (beide Grenzen inklusiv). OHNE from/to bleibt das Verhalten unverändert
    /// (komplette Historie) - Statistik, Druckansicht und Plan-Fortschritt
    /// nutzen weiterhin den Vollpfad, nur die Hundeseite lädt gezielt
    /// Zeiträume nach (siehe TODO.md Roadmap 5).
    /// </summary>
    public async Task<Result<IReadOnlyList<TrainingSessionDto>>> GetByDogAsync(Guid userId, Guid dogId, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<TrainingSessionDto>>.Failure("Hund nicht gefunden.");

        var query = db.TrainingSessions.Where(s => s.DogId == dogId);
        if (from is { } fromDate)
            query = query.Where(s => s.Date >= fromDate);
        if (to is { } toDate)
            query = query.Where(s => s.Date <= toDate);

        var sessions = await query
            .OrderByDescending(s => s.Date)
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Exercise)
            .AsNoTracking()
            .ToListAsync(ct);

        // EIN Existenz-Lookup für alle geladenen Sessions statt eines
        // GPS-Requests pro Trainings-Karte im Frontend (HTTP-N+1).
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var idsWithTrack = (await db.GpsTracks
            .Where(t => sessionIds.Contains(t.TrainingSessionId))
            .Select(t => t.TrainingSessionId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();

        return Result<IReadOnlyList<TrainingSessionDto>>.Success(
            sessions.Select(s => ToDto(s, idsWithTrack.Contains(s.Id))).ToList());
    }

    public async Task<Result<TrainingSessionDto>> GetByIdAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct, track: false);
        return session is null
            ? Result<TrainingSessionDto>.Failure("Training nicht gefunden.")
            : Result<TrainingSessionDto>.Success(ToDto(session, await HasGpsTrackAsync(session.Id, ct)));
    }

    private Task<bool> HasGpsTrackAsync(Guid sessionId, CancellationToken ct)
        => db.GpsTracks.AnyAsync(t => t.TrainingSessionId == sessionId, ct);

    public async Task<Result<TrainingSessionDto>> CreateAsync(Guid userId, CreateTrainingSessionRequest request, CancellationToken ct = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return Result<TrainingSessionDto>.Failure(validationError);

        if (!await db.HasDogAccessAsync(userId, request.DogId, ct))
            return Result<TrainingSessionDto>.Failure("Hund nicht gefunden.");

        if (request.Id is { } existingId)
        {
            // Idempotenz für die Offline-Warteschlange: falls dieselbe
            // client-generierte Id schon synchronisiert wurde (z.B. erneuter
            // Sync-Versuch), nicht doppelt anlegen.
            var alreadyCreated = await GetOwnedSessionAsync(userId, existingId, ct, track: false);
            if (alreadyCreated is not null)
                return Result<TrainingSessionDto>.Success(ToDto(alreadyCreated, await HasGpsTrackAsync(alreadyCreated.Id, ct)));
        }

        var exerciseIds = request.Exercises.Where(e => e.ExerciseId is not null).Select(e => e.ExerciseId!.Value).Distinct().ToList();
        if (exerciseIds.Count > 0)
        {
            var existingExerciseCount = await db.Exercises.CountAsync(e => exerciseIds.Contains(e.Id), ct);
            if (existingExerciseCount != exerciseIds.Count)
                return Result<TrainingSessionDto>.Failure("Eine oder mehrere Übungen wurden nicht gefunden.");
        }

        var planItemError = await ValidatePlanItemsAsync(request, ct);
        if (planItemError is not null)
            return Result<TrainingSessionDto>.Failure(planItemError);

        // Tages-Zusammenfassung: Existiert für diesen Hund an diesem Datum
        // bereits eine Trainingseinheit, werden die Übungen ANGEHÄNGT statt
        // eine neue Einheit anzulegen - das Tagebuch soll pro Trainingstag
        // EIN Feld zeigen, nicht pro abgehaktem Plan-Durchgang einen eigenen
        // Eintrag. Ausnahme: Requests mit client-generierter Id (Offline-
        // Idempotenz, z.B. FahrteRecorder) - dort referenzieren nachfolgende
        // gequeute Requests (GPS-Track) genau diese Id, ein Merge würde die
        // Referenz brechen. Die UI gruppiert solche Einheiten trotzdem in
        // dieselbe Tages-Karte.
        if (request.Id is null)
        {
            var daySession = await db.TrainingSessions
                .FirstOrDefaultAsync(s => s.DogId == request.DogId && s.Date == request.Date, ct);
            if (daySession is not null)
            {
                foreach (var exercise in request.Exercises)
                {
                    // Explizit über das DbSet hinzufügen (nicht über die
                    // Navigation der getrackten Session): die Entities tragen
                    // client-generierte Guid-Keys, per Collection-Fixup würde
                    // EF sie als Modified statt Added einstufen.
                    db.TrainingExercises.Add(new TrainingExercise
                    {
                        TrainingSessionId = daySession.Id,
                        ExerciseId = exercise.ExerciseId,
                        FreeTextLabel = exercise.ExerciseId is null ? exercise.FreeTextLabel!.Trim() : null,
                        Rating = exercise.Rating,
                        Difficulty = exercise.Difficulty,
                        Success = exercise.Success,
                        Notes = exercise.Notes,
                        TrainingPlanItemId = exercise.TrainingPlanItemId
                    });
                }

                daySession.DurationMinutes += request.DurationMinutes;
                var newNotes = request.Notes?.Trim();
                if (!string.IsNullOrEmpty(newNotes))
                {
                    daySession.Notes = string.IsNullOrWhiteSpace(daySession.Notes)
                        ? newNotes
                        : $"{daySession.Notes}\n{newNotes}";
                }

                await db.SaveChangesAsync(ct);

                var merged = await GetOwnedSessionAsync(userId, daySession.Id, ct, track: false);
                return Result<TrainingSessionDto>.Success(ToDto(merged!, await HasGpsTrackAsync(daySession.Id, ct)));
            }
        }

        var session = new TrainingSession
        {
            UserId = userId,
            DogId = request.DogId,
            Date = request.Date,
            DurationMinutes = request.DurationMinutes,
            Notes = request.Notes
        };

        if (request.Id is { } id)
            session.Id = id;

        foreach (var exercise in request.Exercises)
        {
            session.Exercises.Add(new TrainingExercise
            {
                TrainingSessionId = session.Id,
                ExerciseId = exercise.ExerciseId,
                FreeTextLabel = exercise.ExerciseId is null ? exercise.FreeTextLabel!.Trim() : null,
                Rating = exercise.Rating,
                Difficulty = exercise.Difficulty,
                Success = exercise.Success,
                Notes = exercise.Notes,
                TrainingPlanItemId = exercise.TrainingPlanItemId
            });
        }

        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var created = await GetOwnedSessionAsync(userId, session.Id, ct, track: false);
        // Frisch angelegtes Training kann noch keine Fährte haben.
        return Result<TrainingSessionDto>.Success(ToDto(created!, hasGpsTrack: false));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        if (session is null)
            return Result.Failure("Training nicht gefunden.");

        session.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateSessionNotesAsync(Guid userId, Guid sessionId, string? notes, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        if (session is null)
            return Result.Failure("Training nicht gefunden.");

        var trimmed = notes?.Trim();
        session.Notes = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateExerciseNotesAsync(Guid userId, Guid exerciseId, string? notes, CancellationToken ct = default)
    {
        // Übung über ihre Trainingseinheit dem Hund zuordnen und Zugriff prüfen.
        var exercise = await db.TrainingExercises
            .Include(e => e.TrainingSession)
            .FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise?.TrainingSession is null || !await db.HasDogAccessAsync(userId, exercise.TrainingSession.DogId, ct))
            return Result.Failure("Übung nicht gefunden.");

        var trimmed = notes?.Trim();
        exercise.Notes = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetFeedbackAsync(Guid trainerId, Guid sessionId, SetFeedbackRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Feedback))
            return Result.Failure("Feedback darf nicht leer sein.");

        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
            return Result.Failure("Training nicht gefunden.");

        var isAssignedTrainer = await db.TrainerAssignments.AnyAsync(t => t.DogId == session.DogId && t.TrainerId == trainerId, ct);
        if (!isAssignedTrainer)
            return Result.Failure("Nur ein für diesen Hund zugewiesener Trainer kann Feedback geben.");

        session.TrainerFeedback = request.Feedback.Trim();
        session.FeedbackByTrainerId = trainerId;
        session.FeedbackAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await notifications.CreateAsync(
            session.UserId,
            $"Dein Trainer hat Feedback zu deinem Training vom {session.Date:dd.MM.yyyy} hinterlassen.",
            $"/dogs/{session.DogId}",
            ct);

        return Result.Success();
    }

    public async Task<Result> SetExerciseTrainerRatingAsync(Guid trainerId, Guid exerciseId, int rating, string? note, CancellationToken ct = default)
    {
        if (rating < 1 || rating > 5)
            return Result.Failure("Bewertung muss zwischen 1 und 5 liegen.");

        // Übung über ihre Trainingseinheit dem Hund zuordnen und Trainer-Zugriff
        // prüfen. Wie SetFeedbackAsync ist das dem zugewiesenen Trainer
        // vorbehalten - nicht dem Besitzer (der bewertet über Rating selbst).
        var exercise = await db.TrainingExercises
            .Include(e => e.TrainingSession)
            .FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise?.TrainingSession is null)
            return Result.Failure("Übung nicht gefunden.");

        var isAssignedTrainer = await db.TrainerAssignments.AnyAsync(t => t.DogId == exercise.TrainingSession.DogId && t.TrainerId == trainerId, ct);
        if (!isAssignedTrainer)
            return Result.Failure("Nur ein für diesen Hund zugewiesener Trainer kann Übungen bewerten.");

        exercise.TrainerRating = rating;
        var trimmedNote = note?.Trim();
        exercise.TrainerNote = string.IsNullOrEmpty(trimmedNote) ? null : trimmedNote;
        await db.SaveChangesAsync(ct);

        // Bewusst KEINE Benachrichtigung pro Übung - ein Trainer bewertet
        // typischerweise mehrere Übungen einer Einheit; die Session-weite
        // Feedback-Notiz (SetFeedbackAsync) benachrichtigt bereits einmal.
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<PendingFeedbackDto>>> GetPendingFeedbackAsync(Guid trainerId, CancellationToken ct = default)
    {
        // Trainer sieht offenes Feedback ausschließlich für Hunde von aktiven
        // Mitgliedern SEINER Gruppen. Die zusätzliche Berücksichtigung von
        // TrainerAssignments (direkte Hund-Trainer-Zuweisung) bleibt erhalten
        // - so kann ein Trainer auch außerhalb einer Gruppen-Struktur einzelne
        // Hunde betreuen (siehe DogService.AssignTrainer).
        var groupMemberUserIds = db.Groups
            .Where(g => g.TrainerId == trainerId)
            .SelectMany(g => g.Members
                .Where(m => m.Status == GroupMemberStatus.Active)
                .Select(m => m.UserId));

        var sessions = await db.TrainingSessions
            .Where(s => s.TrainerFeedback == null)
            .Where(s =>
                db.DogOwners.Any(o => o.DogId == s.DogId && groupMemberUserIds.Contains(o.UserId)) ||
                db.TrainerAssignments.Any(t => t.DogId == s.DogId && t.TrainerId == trainerId))
            .Join(db.Dogs, s => s.DogId, d => d.Id, (s, d) => new { s.Id, s.DogId, DogName = d.Name, s.UserId, s.Date, s.DurationMinutes })
            .OrderBy(s => s.Date)
            .AsNoTracking()
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(sessions.Select(s => s.UserId).Distinct().ToList(), ct);
        var dtos = sessions
            .Select(s => lookup.TryGetValue(s.UserId, out var owner)
                ? new PendingFeedbackDto(s.Id, s.DogId, s.DogName, $"{owner.FirstName} {owner.LastName}", s.Date, s.DurationMinutes)
                : new PendingFeedbackDto(s.Id, s.DogId, s.DogName, "(unbekannt)", s.Date, s.DurationMinutes))
            .ToList();

        return Result<IReadOnlyList<PendingFeedbackDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<TrainerExerciseToRateDto>>> GetExercisesToRateAsync(Guid trainerId, CancellationToken ct = default)
    {
        // Nur Hunde mit direkter Trainer-Zuweisung: genau diese darf der Trainer
        // auch bewerten (siehe SetExerciseTrainerRatingAsync) - so enthält die
        // Liste keine Übungen, an denen der Bewerten-Button später 403 liefert.
        var assignedDogIds = await db.TrainerAssignments
            .Where(t => t.TrainerId == trainerId)
            .Select(t => t.DogId)
            .ToListAsync(ct);
        if (assignedDogIds.Count == 0)
            return Result<IReadOnlyList<TrainerExerciseToRateDto>>.Success([]);

        var rows = await (
            from e in db.TrainingExercises
            where e.TrainerRating == null && assignedDogIds.Contains(e.TrainingSession!.DogId)
            join d in db.Dogs on e.TrainingSession!.DogId equals d.Id
            orderby e.TrainingSession!.Date descending
            select new
            {
                ExerciseId = e.Id,
                DogId = d.Id,
                DogName = d.Name,
                HandlerUserId = e.TrainingSession!.UserId,
                Date = e.TrainingSession!.Date,
                ExerciseName = e.Exercise != null ? e.Exercise.Name : e.FreeTextLabel!,
                e.Rating,
                e.Success
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(rows.Select(r => r.HandlerUserId).Distinct().ToList(), ct);
        var dtos = rows
            .Select(r => new TrainerExerciseToRateDto(
                r.ExerciseId,
                r.DogId,
                r.DogName,
                lookup.TryGetValue(r.HandlerUserId, out var handler) ? $"{handler.FirstName} {handler.LastName}" : "(unbekannt)",
                r.Date,
                r.ExerciseName,
                r.Rating,
                r.Success))
            .ToList();

        return Result<IReadOnlyList<TrainerExerciseToRateDto>>.Success(dtos);
    }

    // track: false fuer reine Lesezugriffe (kein SaveChangesAsync im selben
    // Aufruf) - vermeidet unnoetiges Change-Tracking. DeleteAsync braucht
    // weiterhin ein getracktes Entity (Default true).
    private async Task<TrainingSession?> GetOwnedSessionAsync(Guid userId, Guid sessionId, CancellationToken ct, bool track = true)
    {
        IQueryable<TrainingSession> query = db.TrainingSessions
            .Where(s => s.Id == sessionId)
            .Where(s =>
                db.DogOwners.Any(o => o.DogId == s.DogId && o.UserId == userId) ||
                db.TrainerAssignments.Any(t => t.DogId == s.DogId && t.TrainerId == userId))
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Exercise);

        if (!track) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(ct);
    }

    // Stellt sicher, dass ein referenziertes Plan-Ziel (1) tatsächlich
    // existiert, (2) zum selben Hund gehört (sonst könnte ein Tagebucheintrag
    // fälschlich den Fortschritt eines fremden Hundes erhöhen) und (3) zur
    // selben Übung gehört wie der Tagebucheintrag - sonst könnte z.B. ein
    // "Sitz"-Eintrag fälschlich als Erfüllung eines "Platz"-Ziels gezählt werden.
    private async Task<string?> ValidatePlanItemsAsync(CreateTrainingSessionRequest request, CancellationToken ct)
    {
        var planItemIds = request.Exercises
            .Where(e => e.TrainingPlanItemId is not null)
            .Select(e => e.TrainingPlanItemId!.Value)
            .Distinct()
            .ToList();
        if (planItemIds.Count == 0) return null;

        var planItems = await db.TrainingPlanItems
            .Where(i => planItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ExerciseId, i.IsRestWeek, DogId = i.TrainingPlan!.Goal!.DogId })
            .ToListAsync(ct);

        if (planItems.Count != planItemIds.Count)
            return "Ein oder mehrere Plan-Ziele wurden nicht gefunden.";

        var planItemsById = planItems.ToDictionary(i => i.Id);
        foreach (var exercise in request.Exercises.Where(e => e.TrainingPlanItemId is not null))
        {
            var planItem = planItemsById[exercise.TrainingPlanItemId!.Value];
            if (planItem.DogId != request.DogId)
                return "Ein Plan-Ziel gehört nicht zu diesem Hund.";
            // Pausenwochen haben wie Freitext-Ziele ExerciseId null - ohne
            // diesen Check würde der ExerciseId-Vergleich darunter einen
            // Freitext-Eintrag fälschlich auf eine Pausenwoche buchen lassen.
            if (planItem.IsRestWeek)
                return "Eine Pausenwoche kann nicht als Übung eingetragen werden.";
            // Katalog-Übung muss zu Katalog-Plan-Ziel passen (gleiche Übung),
            // Freitext-Eintrag (ExerciseId null) nur zu Freitext-Plan-Ziel.
            if (planItem.ExerciseId != exercise.ExerciseId)
                return "Ein Plan-Ziel passt nicht zur ausgewählten Übung.";
        }

        return null;
    }

    private static string? Validate(CreateTrainingSessionRequest request)
    {
        if (request.Date == default)
            return "Datum ist erforderlich.";
        if (request.DurationMinutes <= 0)
            return "Dauer muss größer als 0 sein.";
        if (request.Exercises.Any(e => e.Rating < 1 || e.Rating > 5))
            return "Bewertung muss zwischen 1 und 5 liegen.";

        foreach (var exercise in request.Exercises)
        {
            var hasExerciseId = exercise.ExerciseId is not null;
            var hasFreeText = !string.IsNullOrWhiteSpace(exercise.FreeTextLabel);
            if (hasExerciseId == hasFreeText)
                return "Jede Übung braucht entweder eine Katalog-Übung oder einen Freitext, nicht beides oder keins.";
            // Freitext + Plan-Ziel ist erlaubt, seit Plan-Items selbst Freitext
            // sein können - dass Übungsart und Plan-Ziel-Art zusammenpassen
            // (Katalog zu Katalog, Freitext zu Freitext), stellt
            // ValidatePlanItemsAsync über den ExerciseId-Vergleich sicher.
        }

        return null;
    }

    private static TrainingSessionDto ToDto(TrainingSession s, bool hasGpsTrack) => new(
        s.Id,
        s.DogId,
        s.Date,
        s.DurationMinutes,
        s.Notes,
        s.Exercises.Select(e => new TrainingExerciseDto(
            e.Id,
            e.ExerciseId,
            e.Exercise?.Name ?? e.FreeTextLabel ?? string.Empty,
            e.Rating,
            e.Difficulty,
            e.Success,
            e.Notes,
            e.TrainingPlanItemId,
            e.TrainerRating,
            e.TrainerNote)).ToList(),
        s.TrainerFeedback,
        s.FeedbackAt,
        hasGpsTrack);
}
