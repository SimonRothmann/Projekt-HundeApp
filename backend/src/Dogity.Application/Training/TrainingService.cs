using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Notifications;
using Dogity.Application.Planning;
using Dogity.Application.Tracking;
using Dogity.Application.Weather;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Training;

/// <summary>
/// Use Cases für das Trainingstagebuch (siehe FEATURE_MODULE.md "Training").
/// Zugriff ist immer auf Trainingseinheiten beschränkt, deren Hund dem
/// aufrufenden Benutzer über <see cref="Domain.Dogs.DogOwner"/> zugeordnet ist.
/// </summary>
public class TrainingService(IApplicationDbContext db, INotificationService notifications, IUserLookupService userLookup, IExerciseMasteryService mastery, IWeatherEnrichmentService weather) : ITrainingService
{
    /// <summary>So viele Orte werden als Schnellauswahl angeboten.</summary>
    private const int MaxRecentLocations = 5;

    /// <summary>Obergrenze der durchsuchten Trainings (siehe GetRecentLocationsAsync).</summary>
    private const int RecentSessionScanLimit = 200;


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
            return Result<IReadOnlyList<TrainingSessionDto>>.NotFound("Hund nicht gefunden.");

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

        // EIN Lookup für alle geladenen Sessions statt eines GPS-Requests pro
        // Trainings-Karte im Frontend (HTTP-N+1). Er liefert nebenbei Länge und
        // Untergrund der ersten Fährte des Tages für die Tageszeile des Tagebuchs.
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var tracks = await db.GpsTracks
            .Where(t => sessionIds.Contains(t.TrainingSessionId))
            .OrderBy(t => t.CreatedAt)
            .Select(t => new { t.TrainingSessionId, t.LengthMeters, t.Surface })
            .ToListAsync(ct);
        var firstTrackBySession = tracks
            .GroupBy(t => t.TrainingSessionId)
            .ToDictionary(g => g.Key, g => g.First());

        return Result<IReadOnlyList<TrainingSessionDto>>.Success(
            sessions.Select(s => firstTrackBySession.TryGetValue(s.Id, out var track)
                ? ToDto(s, hasGpsTrack: true, track.LengthMeters, track.Surface)
                : ToDto(s, hasGpsTrack: false)).ToList());
    }

    public async Task<Result<TrainingSessionDto>> GetByIdAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct, track: false);
        return session is null
            ? Result<TrainingSessionDto>.NotFound("Training nicht gefunden.")
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
            return Result<TrainingSessionDto>.NotFound("Hund nicht gefunden.");

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
                return Result<TrainingSessionDto>.NotFound("Eine oder mehrere Übungen wurden nicht gefunden.");
        }

        var planItemError = await ValidatePlanItemsAsync(request, ct);
        if (planItemError is not null)
            return Result<TrainingSessionDto>.Failure(planItemError);

        // Tages-Zusammenfassung: Existiert für diesen Hund an diesem Datum
        // bereits eine Trainingseinheit, werden die Übungen ANGEHÄNGT statt
        // eine neue Einheit anzulegen - das Tagebuch soll pro Trainingstag
        // EIN Feld zeigen, nicht pro abgehaktem Plan-Durchgang einen eigenen
        // Eintrag. Ausnahme: Requests mit client-generierter Id - dort
        // referenzieren nachfolgende gequeute Requests genau diese Id, ein
        // Merge würde die Referenz brechen. Das betrifft nur noch ältere
        // Fährtenrecorder und bereits wartende Offline-Anfragen: Seit
        // 2026-09-10 schickt der Recorder Hund und Datum direkt an
        // GpsTrackService.CreateAsync, der die Fährte selbst an die Einheit
        // des Tages hängt. Die UI gruppiert Alt-Einheiten weiter in dieselbe
        // Tages-Karte.
        if (request.Id is null)
        {
            var daySession = await db.TrainingSessions
                .FirstOrDefaultAsync(s => s.DogId == request.DogId && s.Date == request.Date, ct);

            // Die Notiz wird an die des Tages angehängt. Passt beides zusammen
            // nicht mehr in die Spalte, bekommt der Eintrag stattdessen eine
            // eigene Einheit am selben Tag (die Oberfläche zeigt sie in derselben
            // Tageskarte). Abgelehnt wird er dafür nicht: Kommt er aus der
            // Offline-Warteschlange, wäre eine Ablehnung endgültig, und das
            // ganze Training samt Übungen wäre verloren - obwohl jeder Teil für
            // sich gültig war.
            var newNotes = request.Notes?.Trim();
            var tagesNotiz = daySession is null || string.IsNullOrEmpty(newNotes)
                ? daySession?.Notes
                : string.IsNullOrWhiteSpace(daySession.Notes) ? newNotes : $"{daySession.Notes}\n{newNotes}";
            if (daySession is not null && Textlaengen.ZuLang(tagesNotiz, Textlaengen.TrainingsNotiz, "") is null)
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

                    // Wiedervorlage-Zustand der Katalog-Übung mitziehen (P2) - wird
                    // vom gemeinsamen SaveChanges unten mitgespeichert.
                    if (exercise.ExerciseId is { } exId)
                        await mastery.ApplyLogAsync(request.DogId, exId, exercise.Rating, exercise.Success, request.Date, ct);
                }

                // Verfassung nur eintragen, wenn der Tag noch keine hat. Ein
                // zweiter Eintrag desselben Tages soll die erste Einschätzung
                // nicht stillschweigend überschreiben - ändern geht über die
                // Trainingskarte.
                daySession.Condition ??= request.Condition;

                // Uhrzeit und Ort der Anfrage gehen nicht verloren, wenn der Tag
                // sie noch nicht hat (typisch: Der Tag begann mit einer gelegten
                // Fährte ohne Ort, das Training kommt mit Ort und Zeit dazu).
                // Jeder Wert wird einzeln übernommen und ein vorhandener nie
                // überschrieben: Hat der Tag nur einen Namen, bekommt er trotzdem
                // die Koordinaten (und damit das Wetter), und umgekehrt. Die
                // beiden Koordinaten wandern nur zusammen, eine halbe Position
                // nützt dem Wetterdienst nichts.
                var wetterNeu = false;
                if (daySession.StartTime is null && request.StartTime is { } startTime)
                {
                    daySession.StartTime = startTime;
                    wetterNeu = true;
                }
                if (daySession.Latitude is null && daySession.Longitude is null
                    && request.Latitude is { } breite && request.Longitude is { } laenge)
                {
                    daySession.Latitude = breite;
                    daySession.Longitude = laenge;
                    wetterNeu = true;
                }
                if (string.IsNullOrWhiteSpace(daySession.LocationName) && !string.IsNullOrWhiteSpace(request.LocationName))
                    daySession.LocationName = request.LocationName.Trim();

                daySession.DurationMinutes += request.DurationMinutes;
                daySession.Notes = tagesNotiz;

                // Wetter wie im Neu-Anlegen-Zweig nachziehen, sobald durch die
                // Übernahme Zeit oder Koordinaten neu hinzukamen (die Anreicherung
                // prüft selbst, ob beides vorliegt). Ohne Übernahme
                // bleibt ein vorhandenes Wetter des Tages unangetastet.
                if (wetterNeu)
                    await RefreshWeatherAsync(daySession, ct);

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
            StartTime = request.StartTime,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationName = request.LocationName,
            DurationMinutes = request.DurationMinutes,
            Notes = request.Notes,
            Condition = request.Condition
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

            // Wiedervorlage-Zustand der Katalog-Übung mitziehen (P2).
            if (exercise.ExerciseId is { } exId)
                await mastery.ApplyLogAsync(request.DogId, exId, exercise.Rating, exercise.Success, request.Date, ct);
        }

        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync(ct);

        // Wetter automatisch nachziehen, sofern Ort UND Uhrzeit vorliegen.
        await weather.EnrichSessionAsync(session, ct);
        await db.SaveChangesAsync(ct);

        var created = await GetOwnedSessionAsync(userId, session.Id, ct, track: false);
        // Frisch angelegtes Training kann noch keine Fährte haben.
        return Result<TrainingSessionDto>.Success(ToDto(created!, hasGpsTrack: false));
    }

    /// <summary>So viele offene Feedbacks liefert die Startseite höchstens (die Karte zeigt eines, der Rest wird gezählt).</summary>
    private const int MaxOpenFeedback = 20;

    public async Task<Result<IReadOnlyList<OpenFeedbackDto>>> GetOpenFeedbackAsync(Guid userId, IReadOnlyCollection<Guid> dogIds, CancellationToken ct = default)
    {
        if (dogIds.Count == 0)
            return Result<IReadOnlyList<OpenFeedbackDto>>.Success([]);

        // Zugriff wie bei der Antwort selbst (ReplyToFeedbackAsync): nur
        // Besitzer:innen - die betreuende Trainer:in soll ihr eigenes Feedback
        // nicht als offene Aufgabe sehen. Die übergebenen Ids sind nur eine
        // Vorauswahl; ein fremder Hund fällt hier heraus.
        var offen = await db.TrainingSessions
            .Where(s => dogIds.Contains(s.DogId)
                        && s.TrainerFeedback != null && s.TrainerFeedback != ""
                        && s.OwnerReaction == null && s.OwnerReply == null
                        && db.DogOwners.Any(o => o.DogId == s.DogId && o.UserId == userId))
            .OrderByDescending(s => s.FeedbackAt)
            .ThenByDescending(s => s.Date)
            .Take(MaxOpenFeedback)
            .Select(s => new
            {
                s.Id,
                s.DogId,
                DogName = db.Dogs.Where(d => d.Id == s.DogId).Select(d => d.Name).FirstOrDefault(),
                s.FeedbackByTrainerId,
                Feedback = s.TrainerFeedback!,
                s.FeedbackAt
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var trainerIds = offen.Where(o => o.FeedbackByTrainerId.HasValue).Select(o => o.FeedbackByTrainerId!.Value).Distinct().ToList();
        var trainer = trainerIds.Count == 0
            ? new Dictionary<Guid, UserLookupResult>()
            : (await userLookup.FindByIdsAsync(trainerIds, ct)).ToDictionary(k => k.Key, k => k.Value);

        return Result<IReadOnlyList<OpenFeedbackDto>>.Success(offen
            .Select(o =>
            {
                string? name = null;
                if (o.FeedbackByTrainerId is { } id && trainer.TryGetValue(id, out var t))
                    name = $"{t.FirstName} {t.LastName}".Trim();
                return new OpenFeedbackDto(o.Id, o.DogId, o.DogName ?? "", string.IsNullOrEmpty(name) ? null : name, o.Feedback, o.FeedbackAt);
            })
            .ToList());
    }

    /// <summary>
    /// Die zuletzt benutzten Trainingsorte des Nutzers, jeder Ort nur einmal
    /// und der neueste zuerst.
    /// </summary>
    public async Task<Result<IReadOnlyList<RecentLocationDto>>> GetRecentLocationsAsync(Guid userId, CancellationToken ct = default)
    {
        var dogIds = db.DogOwners.Where(o => o.UserId == userId).Select(o => o.DogId)
            .Union(db.TrainerAssignments.Where(t => t.TrainerId == userId).Select(t => t.DogId));

        // Erst die jüngsten Trainings holen, dann im Speicher entdoppeln: den
        // "neuesten Datensatz je Gruppe" kann EF nicht sauber übersetzen, und
        // bei dieser Datenmenge lohnt keine Fensterfunktion. Das Limit deckelt
        // den Aufwand - wer öfter als RecentSessionScanLimit an EINEM Ort
        // trainiert hat, sieht ältere Orte nicht mehr; das ist hinnehmbar,
        // weil genau dieser eine Ort dann der gesuchte ist.
        var recent = await db.TrainingSessions
            .Where(s => dogIds.Contains(s.DogId)
                        && s.LocationName != null
                        && s.Latitude != null
                        && s.Longitude != null)
            .OrderByDescending(s => s.Date)
            .Select(s => new { s.LocationName, s.Latitude, s.Longitude, s.Date })
            .Take(RecentSessionScanLimit)
            .AsNoTracking()
            .ToListAsync(ct);

        var locations = recent
            .GroupBy(s => s.LocationName!, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentLocations)
            .Select(g => new RecentLocationDto(
                g.First().LocationName!, g.First().Latitude!.Value, g.First().Longitude!.Value, g.First().Date))
            .ToList();

        return Result<IReadOnlyList<RecentLocationDto>>.Success(locations);
    }

    public async Task<Result<TrainingSessionDto>> SetSessionContextAsync(Guid userId, Guid sessionId, UpdateSessionContextRequest request, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        if (session is null)
            return Result<TrainingSessionDto>.NotFound("Training nicht gefunden.");

        if (request.Latitude is { } lat && (lat < -90 || lat > 90))
            return Result<TrainingSessionDto>.Failure("Ungültiger Breitengrad.");
        if (request.Longitude is { } lon && (lon < -180 || lon > 180))
            return Result<TrainingSessionDto>.Failure("Ungültiger Längengrad.");

        session.StartTime = request.StartTime;
        session.Latitude = request.Latitude;
        session.Longitude = request.Longitude;
        var name = request.LocationName?.Trim();
        session.LocationName = string.IsNullOrEmpty(name) ? null : name;
        session.Condition = request.Condition;

        await RefreshWeatherAsync(session, ct);
        await db.SaveChangesAsync(ct);

        var updated = await GetOwnedSessionAsync(userId, sessionId, ct, track: false);
        return Result<TrainingSessionDto>.Success(ToDto(updated!, await HasGpsTrackAsync(sessionId, ct)));
    }

    public async Task<Result<TrainingSessionDto>> MoveTrainingDayAsync(Guid userId, Guid sessionId, DateOnly date, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        if (session is null)
            return Result<TrainingSessionDto>.NotFound("Training nicht gefunden.");

        if (date == default)
            return Result<TrainingSessionDto>.Failure("Datum ist erforderlich.");

        if (session.Date != date)
        {
            // Alle Einheiten dieses Hundes am selben Tag mitnehmen (siehe
            // ITrainingService) - der Zugriff auf den Hund ist über die
            // gefundene Einheit bereits geprüft. Gemeinsames SaveChanges, damit
            // der Trainingstag nicht auf halbem Weg zerfällt, falls eine der
            // Wetterabfragen scheitert.
            var oldDate = session.Date;
            var daySessions = await db.TrainingSessions
                .Where(s => s.DogId == session.DogId && s.Date == oldDate)
                .ToListAsync(ct);

            foreach (var moved in daySessions)
            {
                moved.Date = date;
                await RefreshWeatherAsync(moved, ct);
            }

            // Landet der Tag auf einem Datum, an dem schon trainiert wurde,
            // bleiben es bewusst getrennte Einheiten: das Tagebuch fasst sie
            // ohnehin in EINER Tages-Karte zusammen (siehe SessionHistory). Ein
            // echtes Verschmelzen würde Übungen umhängen und Dauern addieren -
            // nicht mehr rückgängig zu machen, wenn sich jemand vertippt hat.
            await db.SaveChangesAsync(ct);

            // LastTrainedAt und DueAt hängen am Datum: ohne Neuberechnung
            // stünde die Wiedervorlage weiter auf dem alten Tag, und der
            // adaptive Generator plante gegen ein Datum, das es nicht mehr gibt.
            var movedIds = daySessions.Select(d => d.Id).ToList();
            var affected = await db.TrainingExercises
                .Where(e => movedIds.Contains(e.TrainingSessionId) && e.ExerciseId != null)
                .Select(e => e.ExerciseId!.Value)
                .Distinct()
                .ToListAsync(ct);
            foreach (var exerciseId in affected)
                await mastery.RecomputeAsync(session.DogId, exerciseId, ct);
            if (affected.Count > 0) await db.SaveChangesAsync(ct);
        }

        var updated = await GetOwnedSessionAsync(userId, sessionId, ct, track: false);
        return Result<TrainingSessionDto>.Success(ToDto(updated!, await HasGpsTrackAsync(sessionId, ct)));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        if (session is null)
            return Result.NotFound("Training nicht gefunden.");

        // Katalog-Übungen der Einheit merken, BEVOR sie weggefiltert ist.
        var affected = session.Exercises
            .Where(e => e.ExerciseId is not null)
            .Select(e => e.ExerciseId!.Value)
            .Distinct()
            .ToList();

        // Die Fährten gehen mit dem Training: Über das Training erreicht sie
        // niemand mehr, sie würden aber weiter mitgezählt und ihre GPS-Punkte
        // blieben liegen. Beides in einem SaveChanges.
        var jetzt = DateTimeOffset.UtcNow;
        var faehrtenIds = await db.GpsTracks.IgnoreQueryFilters()
            .Where(t => t.TrainingSessionId == sessionId)
            .Select(t => t.Id)
            .ToListAsync(ct);
        await GpsTrackRemoval.WeichLoeschenAsync(db, faehrtenIds, jetzt, ct);

        session.DeletedAt = jetzt;
        await db.SaveChangesAsync(ct);

        // Der Wiedervorlage-Zustand trägt das gelöschte Ergebnis sonst weiter
        // in Leitner-Box und Schnitt - der adaptive Generator plante dann gegen
        // ein Training, das es nicht mehr gibt.
        foreach (var exerciseId in affected)
            await mastery.RecomputeAsync(session.DogId, exerciseId, ct);
        if (affected.Count > 0) await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> UpdateSessionNotesAsync(Guid userId, Guid sessionId, string? notes, CancellationToken ct = default)
    {
        if (Textlaengen.ZuLang(notes, Textlaengen.TrainingsNotiz, "Der Kommentar zum Trainingstag") is { } zuLang)
            return Result.Failure(zuLang);

        var session = await GetOwnedSessionAsync(userId, sessionId, ct);
        if (session is null)
            return Result.NotFound("Training nicht gefunden.");

        var trimmed = notes?.Trim();
        session.Notes = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateExerciseNotesAsync(Guid userId, Guid exerciseId, string? notes, CancellationToken ct = default)
    {
        if (Textlaengen.ZuLang(notes, Textlaengen.UebungsNotiz, "Der Kommentar zur Übung") is { } zuLang)
            return Result.Failure(zuLang);

        // Übung über ihre Trainingseinheit dem Hund zuordnen und Zugriff prüfen.
        var exercise = await db.TrainingExercises
            .Include(e => e.TrainingSession)
            .FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise?.TrainingSession is null || !await db.HasDogAccessAsync(userId, exercise.TrainingSession.DogId, ct))
            return Result.NotFound("Übung nicht gefunden.");

        var trimmed = notes?.Trim();
        exercise.Notes = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<TrainingSessionDto>> UpdateExerciseAsync(Guid userId, Guid exerciseId, UpdateTrainingExerciseRequest request, CancellationToken ct = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
            return Result<TrainingSessionDto>.Failure("Bewertung muss zwischen 1 und 5 liegen.");
        if (Textlaengen.ZuLang(request.Notes, Textlaengen.UebungsNotiz, "Der Kommentar zur Übung") is { } zuLang)
            return Result<TrainingSessionDto>.Failure(zuLang);

        var exercise = await db.TrainingExercises
            .Include(e => e.TrainingSession)
            .FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise?.TrainingSession is null || !await db.HasDogAccessAsync(userId, exercise.TrainingSession.DogId, ct))
            return Result<TrainingSessionDto>.NotFound("Übung nicht gefunden.");

        var ratingChanged = exercise.Rating != request.Rating || exercise.Success != request.Success;

        exercise.Rating = request.Rating;
        exercise.Success = request.Success;
        var trimmed = request.Notes?.Trim();
        exercise.Notes = string.IsNullOrEmpty(trimmed) ? null : trimmed;

        // Der Wiedervorlage-Zustand hängt an Bewertung und Erfolg - eine
        // Korrektur muss dort ankommen, sonst plant der adaptive Generator
        // weiter mit dem alten Stand. Neu RECHNEN statt fortschreiben: das
        // ursprüngliche Ergebnis steckt schon in der Box, ein zweites
        // ApplyLog würde dasselbe Training doppelt zählen.
        if (ratingChanged && exercise.ExerciseId is { } catalogExerciseId)
        {
            await db.SaveChangesAsync(ct);
            await mastery.RecomputeAsync(exercise.TrainingSession.DogId, catalogExerciseId, ct);
        }

        await db.SaveChangesAsync(ct);

        var updated = await GetOwnedSessionAsync(userId, exercise.TrainingSessionId, ct, track: false);
        return updated is null
            ? Result<TrainingSessionDto>.NotFound("Training nicht gefunden.")
            : Result<TrainingSessionDto>.Success(ToDto(updated, await HasGpsTrackAsync(updated.Id, ct)));
    }

    public async Task<Result> SetFeedbackAsync(Guid trainerId, Guid sessionId, SetFeedbackRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Feedback))
            return Result.Failure("Feedback darf nicht leer sein.");
        if (Textlaengen.ZuLang(request.Feedback, Textlaengen.TrainerRueckmeldung, "Das Feedback") is { } zuLang)
            return Result.Failure(zuLang);

        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
            return Result.NotFound("Training nicht gefunden.");

        var isAssignedTrainer = await db.TrainerAssignments.AnyAsync(t => t.DogId == session.DogId && t.TrainerId == trainerId, ct);
        if (!isAssignedTrainer)
            return Result.Failure("Nur ein für diesen Hund zugewiesener Trainer kann Feedback geben.");

        session.TrainerFeedback = request.Feedback.Trim();
        session.FeedbackByTrainerId = trainerId;
        session.FeedbackAt = DateTimeOffset.UtcNow;
        // Danke/Verstanden und Rückfrage bezogen sich auf den bisherigen Text.
        // Stünden sie unter dem neuen Feedback, sähe es aus, als hätte die
        // Besitzerin schon darauf geantwortet - und die Trainer:in hielte die
        // Rückfrage für beantwortet oder erledigt.
        session.OwnerReaction = null;
        session.OwnerReply = null;
        session.OwnerReplyAt = null;
        await db.SaveChangesAsync(ct);

        // Bei mehreren Hunden muss die Meldung sagen, um wen es geht. "Training
        // von Max" statt "Maxs Training": die Genitivform (Max' / Maxens /
        // Felix') stimmt nicht für jeden Namen, die Umschreibung mit "von"
        // dagegen immer.
        var hundName = await db.Dogs.Where(d => d.Id == session.DogId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        var training = string.IsNullOrWhiteSpace(hundName)
            ? $"zu deinem Training vom {session.Date:dd.MM.yyyy}"
            : $"zum Training von {hundName} vom {session.Date:dd.MM.yyyy}";

        // Der Link öffnet den Eintrag selbst, nicht nur die Hundeseite.
        await notifications.CreateAsync(
            session.UserId,
            $"Dein Trainer hat Feedback {training} hinterlassen.",
            $"/dogs/{session.DogId}?eintrag={session.Id}",
            ct);

        return Result.Success();
    }

    public async Task<Result> ReplyToFeedbackAsync(Guid userId, Guid sessionId, FeedbackReplyRequest request, CancellationToken ct = default)
    {
        if (request.Reaction is { } reaction && !Enum.IsDefined(reaction))
            return Result.Failure("Unbekannte Reaktion.");
        if (Textlaengen.ZuLang(request.Reply, Textlaengen.FeedbackRueckfrage, "Die Rückfrage") is { } zuLang)
            return Result.Failure(zuLang);

        // Nur die Besitzer:innen des Hundes antworten - nicht die betreuende
        // Trainer:in auf ihr eigenes Feedback (anders als GetOwnedSessionAsync,
        // das auch Trainer:innen durchlässt). Wer sonst nicht dazugehört,
        // erfährt nicht einmal, dass es den Eintrag gibt.
        var session = await db.TrainingSessions
            .Where(s => s.Id == sessionId)
            .Where(s => db.DogOwners.Any(o => o.DogId == s.DogId && o.UserId == userId))
            .FirstOrDefaultAsync(ct);
        if (session is null)
            return Result.NotFound("Training nicht gefunden.");
        if (string.IsNullOrWhiteSpace(session.TrainerFeedback))
            return Result.Failure("Zu diesem Training gibt es noch kein Feedback.");

        var reply = string.IsNullOrWhiteSpace(request.Reply) ? null : request.Reply.Trim();
        var reactionChanged = request.Reaction is not null && request.Reaction != session.OwnerReaction;
        var replyChanged = reply is not null && reply != session.OwnerReply;

        session.OwnerReaction = request.Reaction;
        if (reply != session.OwnerReply)
            session.OwnerReplyAt = reply is null ? null : DateTimeOffset.UtcNow;
        session.OwnerReply = reply;
        await db.SaveChangesAsync(ct);

        // Benachrichtigt wird nur, wenn etwas dazukam - ein Zurücknehmen oder
        // dasselbe noch einmal meldet der Trainer:in nichts. Ist die Trainer:in
        // nicht mehr da (Konto gelöscht), gibt es niemanden zu benachrichtigen.
        if ((reactionChanged || replyChanged) && session.FeedbackByTrainerId is { } trainerId)
        {
            var lookup = await userLookup.FindByIdsAsync([userId], ct);
            var vorname = lookup.TryGetValue(userId, out var owner) && !string.IsNullOrWhiteSpace(owner.FirstName)
                ? owner.FirstName
                : "Jemand";
            var nachricht = $"{vorname} hat auf dein Feedback zum Training vom {session.Date:dd.MM.yyyy} geantwortet.";
            var link = $"/dogs/{session.DogId}?eintrag={session.Id}";
            // Wer die Chips mehrfach umschaltet, soll die Glocke nicht mit
            // gleichlautenden Meldungen füllen: Liegt dieselbe noch ungelesen
            // da, genügt sie - der Link führt ohnehin zum aktuellen Stand.
            var schonOffen = await db.Notifications.AnyAsync(
                n => n.UserId == trainerId && !n.IsRead && n.LinkPath == link && n.Message == nachricht, ct);
            if (!schonOffen)
                await notifications.CreateAsync(trainerId, nachricht, link, ct);
        }

        return Result.Success();
    }

    public async Task<Result> SetExerciseTrainerRatingAsync(Guid trainerId, Guid exerciseId, int rating, string? note, CancellationToken ct = default)
    {
        if (rating < 1 || rating > 5)
            return Result.Failure("Bewertung muss zwischen 1 und 5 liegen.");
        if (Textlaengen.ZuLang(note, Textlaengen.UebungsNotiz, "Die Notiz zur Übung") is { } zuLang)
            return Result.Failure(zuLang);

        // Übung über ihre Trainingseinheit dem Hund zuordnen und Trainer-Zugriff
        // prüfen. Wie SetFeedbackAsync ist das dem zugewiesenen Trainer
        // vorbehalten - nicht dem Besitzer (der bewertet über Rating selbst).
        var exercise = await db.TrainingExercises
            .Include(e => e.TrainingSession)
            .FirstOrDefaultAsync(e => e.Id == exerciseId, ct);
        if (exercise?.TrainingSession is null)
            return Result.NotFound("Übung nicht gefunden.");

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

    public async Task<Result> MarkSessionReviewedAsync(Guid trainerId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
            return Result.NotFound("Training nicht gefunden.");
        if (!await IsAssignedTrainerAsync(trainerId, session.DogId, ct))
            return Result.Failure("Nur ein für diesen Hund zugewiesener Trainer kann ein Training abhaken.");

        // Ein zweites "Fertig" lässt den ersten Zeitpunkt stehen.
        session.TrainerReviewedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UnmarkSessionReviewedAsync(Guid trainerId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
            return Result.NotFound("Training nicht gefunden.");
        if (!await IsAssignedTrainerAsync(trainerId, session.DogId, ct))
            return Result.Failure("Nur ein für diesen Hund zugewiesener Trainer kann ein Training abhaken.");

        session.TrainerReviewedAt = null;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AcceptSelfRatingsAsync(Guid trainerId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.TrainingSessions
            .Include(s => s.Exercises)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null)
            return Result.NotFound("Training nicht gefunden.");
        if (!await IsAssignedTrainerAsync(trainerId, session.DogId, ct))
            return Result.Failure("Nur ein für diesen Hund zugewiesener Trainer kann Übungen bewerten.");

        // Nur Übungen ohne Trainer-Bewertung, und nur dort, wo es eine
        // Selbsteinschätzung gibt (1-5). Eine vorhandene Trainer-Bewertung samt
        // Notiz bleibt unangetastet; die Notiz der Hundeführer:in wird nicht
        // mitkopiert, sie steht ohnehin an der Übung.
        foreach (var exercise in session.Exercises.Where(e => e.TrainerRating is null && e.Rating is >= 1 and <= 5))
            exercise.TrainerRating = exercise.Rating;

        session.TrainerReviewedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Wie SetExerciseTrainerRatingAsync: keine Benachrichtigung. Das normale
        // Bewerten meldet der Hundeführer:in nichts; "Passt so" tut dasselbe
        // und gibt deshalb auch keine Meldung aus.
        return Result.Success();
    }

    private Task<bool> IsAssignedTrainerAsync(Guid trainerId, Guid dogId, CancellationToken ct) =>
        db.TrainerAssignments.AnyAsync(t => t.DogId == dogId && t.TrainerId == trainerId, ct);

    public async Task<Result<IReadOnlyList<TrainerSessionToRateDto>>> GetSessionsToRateAsync(Guid trainerId, CancellationToken ct = default)
    {
        // Nur Hunde mit direkter Trainer-Zuweisung: genau diese darf der Trainer
        // auch kommentieren UND bewerten (siehe SetFeedbackAsync /
        // SetExerciseTrainerRatingAsync). Gleiche Reichweite für beides - so
        // enthält die Liste keine Trainings, an denen eine Aktion später 403
        // liefert (frühere Gruppen-Reichweite des offenen Feedbacks entfällt).
        var assignedDogIds = await db.TrainerAssignments
            .Where(t => t.TrainerId == trainerId)
            .Select(t => t.DogId)
            .ToListAsync(ct);
        if (assignedDogIds.Count == 0)
            return Result<IReadOnlyList<TrainerSessionToRateDto>>.Success([]);

        // Offen = nicht abgehakt, ohne Feedback und mit unbewerteten Übungen
        // (oder ohne Übungen) - Definition in TrainerSessionQueries.
        // Geladen werden ALLE Übungen des Trainings (auch bereits bewertete),
        // damit der Trainer den ganzen Trainingstag auf einen Blick sieht.
        var sessions = await db.TrainingSessionsToRate(assignedDogIds)
            .OrderByDescending(s => s.Date)
            .Include(s => s.Exercises)
            .ThenInclude(e => e.Exercise)
            .AsNoTracking()
            .ToListAsync(ct);

        var dogNames = await db.Dogs
            .Where(d => assignedDogIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var lookup = await userLookup.FindByIdsAsync(sessions.Select(s => s.UserId).Distinct().ToList(), ct);

        var dtos = sessions
            .Select(s => new TrainerSessionToRateDto(
                s.Id,
                s.DogId,
                dogNames.GetValueOrDefault(s.DogId, "(unbekannt)"),
                lookup.TryGetValue(s.UserId, out var handler) ? $"{handler.FirstName} {handler.LastName}" : "(unbekannt)",
                s.Date,
                s.DurationMinutes,
                s.TrainerFeedback,
                s.Exercises
                    .Select(e => new TrainerSessionExerciseDto(
                        e.Id,
                        e.Exercise?.Name ?? e.FreeTextLabel ?? string.Empty,
                        e.Rating,
                        e.Success,
                        e.TrainerRating,
                        e.TrainerNote))
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<TrainerSessionToRateDto>>.Success(dtos);
    }

    /// <summary>
    /// Gespeicherten Wetterstand verwerfen und neu ermitteln. Nötig nach jeder
    /// Änderung an Datum, Uhrzeit oder Ort: die Werte gehörten zum vorherigen
    /// Zeitpunkt und wären danach die Temperatur eines anderen Tages oder Orts.
    /// Erst das Leeren, dann der Abruf - schlägt er fehl (Ort ohne Koordinaten,
    /// Wetterdienst nicht erreichbar), steht lieber gar kein Wert da als ein
    /// falscher.
    /// </summary>
    private async Task RefreshWeatherAsync(TrainingSession session, CancellationToken ct)
    {
        session.TemperatureC = null;
        session.RelativeHumidity = null;
        session.WindSpeedKmh = null;
        session.WeatherCode = null;
        session.WeatherFetchedAt = null;

        await weather.EnrichSessionAsync(session, ct);
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

        var zuLang = Textlaengen.ZuLang(request.Notes, Textlaengen.TrainingsNotiz, "Der Kommentar zum Trainingstag")
            ?? request.Exercises
                .Select(e => Textlaengen.ZuLang(e.Notes, Textlaengen.UebungsNotiz, "Ein Kommentar zu einer Übung")
                    ?? Textlaengen.ZuLang(e.FreeTextLabel, Textlaengen.EigeneUebung, "Der Name einer eigenen Übung"))
                .FirstOrDefault(m => m is not null);
        if (zuLang is not null)
            return zuLang;

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

    private static TrainingSessionDto ToDto(TrainingSession s, bool hasGpsTrack, double? trackLengthMeters = null, string? trackSurface = null) => new(
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
        s.StartTime,
        s.Latitude,
        s.Longitude,
        s.LocationName,
        s.TemperatureC,
        s.RelativeHumidity,
        s.WindSpeedKmh,
        s.WeatherCode,
        s.Condition,
        hasGpsTrack,
        s.OwnerReaction,
        s.OwnerReply,
        s.OwnerReplyAt,
        s.TrainerReviewedAt,
        trackLengthMeters,
        trackSurface);
}
