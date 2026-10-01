using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Weather;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tracking;

/// <summary>
/// Use Cases für die Fährtenaufzeichnung (siehe FEATURE_MODULE.md "Fährte":
/// GPS, Karten, Fährtenhistorie). Eine Fährte gehört immer zu einer
/// Trainingseinheit; Zugriff folgt daher der Zugriffsprüfung des Hundes
/// dieser Trainingseinheit (Besitzer oder betreuender Trainer).
/// </summary>
/// <param name="punkteJeTrainingstag">
/// Obergrenze aller Punkte eines Trainingstags - nur für Tests abweichend von
/// <see cref="MaxPunkteJeTrainingstag"/>, damit sie nicht 200.000 Punkte anlegen müssen.
/// </param>
public class GpsTrackService(
    IApplicationDbContext db,
    IWeatherEnrichmentService weather,
    int punkteJeTrainingstag = GpsTrackService.MaxPunkteJeTrainingstag) : IGpsTrackService
{
    /// <summary>
    /// Höchstzahl der Punkte einer Aufzeichnung - gut 13 Stunden bei einem
    /// Punkt pro Sekunde; keine echte Fährte kommt in die Nähe. Ohne Grenze nahm
    /// der Server rund 250.000 Punkte je Anfrage an (so viel passt in die
    /// Standardgröße einer Anfrage), und die Auswertung eines solchen Ablaufs
    /// band den gemeinsamen Server für Minuten.
    /// </summary>
    public const int MaxPunkteJeAufzeichnung = 50_000;

    /// <summary>
    /// Höchstzahl aller GPS-Punkte eines Trainingstags (Legungen und Abläufe
    /// zusammen). Die Obergrenze je Aufzeichnung allein reichte nicht: Die
    /// Fährten eines Tages werden gemeinsam geladen und aufbereitet, und wie
    /// viele Abläufe eine Fährte hat, begrenzte nichts. Ein echter, voller
    /// Fährtentag liegt bei einigen zehntausend Punkten.
    /// </summary>
    public const int MaxPunkteJeTrainingstag = 200_000;

    private static readonly string ZuVielePunkte =
        $"Die Aufzeichnung hat zu viele Punkte (höchstens {MitTausenderpunkt(MaxPunkteJeAufzeichnung)}).";

    private string TagZuVoll =>
        $"Für diesen Trainingstag sind schon zu viele GPS-Punkte gespeichert (höchstens {MitTausenderpunkt(punkteJeTrainingstag)}).";

    // Tausenderpunkt von Hand: Der Container läuft mit neutraler Kultur, "N0"
    // ergäbe dort "50,000".
    private static string MitTausenderpunkt(int zahl) =>
        zahl.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

    /// <summary>Alle gespeicherten Punkte der Fährten und Abläufe einer Trainingseinheit.</summary>
    private async Task<int> PunkteDerEinheitAsync(Guid sessionId, CancellationToken ct)
    {
        var gelegt = await db.GpsPoints
            .CountAsync(p => db.GpsTracks.Any(t => t.Id == p.TrackId && t.TrainingSessionId == sessionId), ct);
        var abgelaufen = await db.GpsWalkPoints
            .CountAsync(p => db.GpsWalkRuns.Any(r => r.Id == p.WalkRunId
                && db.GpsTracks.Any(t => t.Id == r.TrackId && t.TrainingSessionId == sessionId)), ct);
        return gelegt + abgelaufen;
    }

    public async Task<Result<IReadOnlyList<GpsTrackDto>>> GetByTrainingSessionAsync(Guid userId, Guid trainingSessionId, CancellationToken ct = default)
    {
        if (!await HasSessionAccessAsync(userId, trainingSessionId, ct))
            return Result<IReadOnlyList<GpsTrackDto>>.NotFound("Training nicht gefunden.");

        // AsSplitQuery: zwei Collection-Includes (Points + WalkRuns.Points) in
        // EINEM Query erzeugen im JOIN eine kartesische Zeilenexplosion
        // (Zeilen ~ TrackPoints x WalkRunPoints) - bei 300 Punkten Legung und
        // 3 Abläufen à 300 Punkten überträgt Postgres ein Vielfaches der
        // Nutzdaten. Split zerlegt das in schlanke Einzelqueries pro
        // Collection; die Tracks sind append-only pro Nutzer, das
        // Konsistenzfenster zwischen den Teilqueries ist daher unkritisch.
        var tracks = await db.GpsTracks
            .Where(t => t.TrainingSessionId == trainingSessionId)
            .Include(t => t.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Stops)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        // Vereinfachung nur auf dem Lesepfad - der Client hat die Rohpunkte
        // einer soeben erstellten Aufzeichnung (CreateAsync/AddWalkRunAsync)
        // bereits selbst im Speicher, eine Vereinfachung dort brächte nichts.
        return Result<IReadOnlyList<GpsTrackDto>>.Success(tracks.Select(t => ToDto(t, simplify: true)).ToList());
    }

    public async Task<Result<GpsTrackDto>> CreateAsync(Guid userId, CreateGpsTrackRequest request, CancellationToken ct = default)
    {
        // Wiederholte Anfrage aus der Offline-Warteschlange: Die Fährte gibt
        // es schon. Nicht erneut anlegen - und vor allem die Dauer nicht ein
        // zweites Mal auf die Einheit addieren.
        if (request.Id is { } vorhandeneId)
        {
            var einheitDerFaehrte = await db.GpsTracks
                .Where(t => t.Id == vorhandeneId)
                .Select(t => (Guid?)t.TrainingSessionId)
                .FirstOrDefaultAsync(ct);
            if (einheitDerFaehrte is { } einheitId)
            {
                var liste = await GetByTrainingSessionAsync(userId, einheitId, ct);
                return liste.Succeeded
                    ? Result<GpsTrackDto>.Success(liste.Value!.First(t => t.Id == vorhandeneId))
                    : Result<GpsTrackDto>.NotFound("Training nicht gefunden.");
            }
        }

        Guid sessionId;
        TrainingSession? neueEinheit = null;
        TrainingSession? einheitDesTages = null;

        if (request.TrainingSessionId is { } angegebeneEinheit)
        {
            if (!await HasSessionAccessAsync(userId, angegebeneEinheit, ct))
                return Result<GpsTrackDto>.NotFound("Training nicht gefunden.");
            sessionId = angegebeneEinheit;
        }
        else if (request.DogId is { } dogId && request.Date is { } date)
        {
            if (!await db.HasDogAccessAsync(userId, dogId, ct))
                return Result<GpsTrackDto>.NotFound("Hund nicht gefunden.");

            // Dieselbe Regel wie beim Eintragen eines Trainings (siehe
            // TrainingService.CreateAsync "Tages-Zusammenfassung"): ein Tag,
            // eine Einheit. Eine zweite oder dritte Fährte derselben
            // Übungsstunde gehört dazu und bekommt keine eigene.
            einheitDesTages = await db.TrainingSessions
                .Where(s => s.DogId == dogId && s.Date == date)
                .OrderBy(s => s.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (einheitDesTages is null)
            {
                neueEinheit = new TrainingSession
                {
                    UserId = userId,
                    DogId = dogId,
                    Date = date,
                    DurationMinutes = Math.Max(1, request.DurationMinutes ?? 1)
                };
                sessionId = neueEinheit.Id;
            }
            else
            {
                sessionId = einheitDesTages.Id;
            }
        }
        else
        {
            return Result<GpsTrackDto>.Failure("Trainingseinheit oder Hund und Datum angeben.");
        }

        if (request.Points.Count == 0)
            return Result<GpsTrackDto>.Failure("Eine Fährte benötigt mindestens einen GPS-Punkt.");
        if (request.Points.Count > MaxPunkteJeAufzeichnung)
            return Result<GpsTrackDto>.Failure(ZuVielePunkte);
        if (neueEinheit is null && await PunkteDerEinheitAsync(sessionId, ct) + request.Points.Count > punkteJeTrainingstag)
            return Result<GpsTrackDto>.Failure(TagZuVoll);

        var zuLang = Textlaengen.ZuLang(request.Comment, Textlaengen.FaehrtenKommentar, "Der Kommentar")
            ?? Textlaengen.ZuLang(request.Surface, Textlaengen.Kurzangabe, "Der Untergrund")
            ?? Textlaengen.ZuLang(request.Weather, Textlaengen.Kurzangabe, "Die Wetterangabe")
            ?? Textlaengen.ZuLang(request.Wind, Textlaengen.Kurzangabe, "Die Windangabe")
            ?? request.Points
                .Select(p => Textlaengen.ZuLang(p.Label, Textlaengen.MarkerBeschriftung, "Die Beschriftung eines Markers"))
                .FirstOrDefault(m => m is not null);
        if (zuLang is not null)
            return Result<GpsTrackDto>.Failure(zuLang);

        // Erst nach allen Prüfungen: Eine abgelehnte Anfrage soll weder eine
        // leere Einheit hinterlassen noch die Dauer einer bestehenden ändern.
        if (neueEinheit is not null)
            db.TrainingSessions.Add(neueEinheit);
        else if (einheitDesTages is not null && request.DurationMinutes is > 0)
            einheitDesTages.DurationMinutes += request.DurationMinutes.Value;

        var track = new GpsTrack
        {
            TrainingSessionId = sessionId,
            LengthMeters = request.LengthMeters,
            AgeMinutes = request.AgeMinutes,
            Surface = request.Surface,
            Weather = request.Weather,
            Wind = request.Wind,
            Comment = request.Comment
        };
        if (request.Id is { } neueId)
            track.Id = neueId;

        foreach (var point in request.Points)
        {
            track.Points.Add(new GpsPoint
            {
                TrackId = track.Id,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Timestamp = point.Timestamp,
                Accuracy = point.Accuracy,
                PointType = point.PointType,
                Label = point.Label,
                MarkerType = point.MarkerType
            });
        }

        db.GpsTracks.Add(track);
        await db.SaveChangesAsync(ct);

        // Wetter zum Legezeitpunkt automatisch ermitteln (Ort + Zeit stecken
        // in den Punkten). Schlägt der Abruf fehl, bleibt es einfach leer.
        await weather.EnrichTrackAsync(track, ct);
        await db.SaveChangesAsync(ct);

        return Result<GpsTrackDto>.Success(ToDto(track));
    }

    public async Task<Result<GpsWalkRunDto>> AddWalkRunAsync(Guid userId, Guid trackId, CreateGpsWalkRunRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.NotFound("Fährte nicht gefunden.");

        if (request.Points.Count == 0)
            return Result<GpsWalkRunDto>.Failure("Ein Ablauf-Versuch benötigt mindestens einen GPS-Punkt.");
        if (request.Points.Count > MaxPunkteJeAufzeichnung)
            return Result<GpsWalkRunDto>.Failure(ZuVielePunkte);
        if (await PunkteDerEinheitAsync(track.TrainingSessionId, ct) + request.Points.Count > punkteJeTrainingstag)
            return Result<GpsWalkRunDto>.Failure(TagZuVoll);
        if (Textlaengen.ZuLang(request.Comment, Textlaengen.FaehrtenKommentar, "Der Kommentar") is { } zuLang)
            return Result<GpsWalkRunDto>.Failure(zuLang);

        var walkRun = new GpsWalkRun
        {
            TrackId = trackId,
            LengthMeters = request.LengthMeters,
            Comment = request.Comment
        };

        foreach (var point in request.Points)
        {
            walkRun.Points.Add(new GpsWalkPoint
            {
                WalkRunId = walkRun.Id,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                Timestamp = point.Timestamp,
                Accuracy = point.Accuracy
            });
        }

        db.GpsWalkRuns.Add(walkRun);
        await db.SaveChangesAsync(ct);

        // Auswertung direkt nach dem Speichern - die gelegten Punkte liegen
        // dafür ohnehin schon in der Datenbank.
        await EvaluateAndPersistAsync(walkRun, ct);
        // Erst mit dem ersten Ablauf gibt es einen Suchzeitpunkt - jetzt lässt
        // sich die Temperaturänderung Legen -> Suchen bestimmen.
        await weather.EnrichTrackAsync(track, ct);
        await db.SaveChangesAsync(ct);

        return Result<GpsWalkRunDto>.Success(ToWalkRunDto(walkRun));
    }

    public async Task<Result<GpsWalkRunDto>> EvaluateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.NotFound("Fährte nicht gefunden.");

        var walkRun = await db.GpsWalkRuns
            .Include(r => r.Points)
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunDto>.NotFound("Ablauf-Versuch nicht gefunden.");

        var ausgewertet = await EvaluateAndPersistAsync(walkRun, ct);
        await db.SaveChangesAsync(ct);
        if (!ausgewertet)
            return Result<GpsWalkRunDto>.Failure("Dieser Ablauf ist zu groß für die Auswertung.");

        return Result<GpsWalkRunDto>.Success(ToWalkRunDto(walkRun));
    }

    /// <summary>
    /// Wertet einen Ablauf gegen seine gelegte Fährte aus und schreibt das
    /// Ergebnis auf die Entitäten (ohne SaveChanges - der Aufrufer entscheidet,
    /// wann gespeichert wird). Bestehende Halte werden ersetzt.
    ///
    /// <c>false</c>, wenn die Auswertung zu aufwendig wäre (siehe
    /// <see cref="GpsTrackEvaluator.MaxAuswertungsAufwand"/>). Der Ablauf gilt
    /// dann trotzdem als erledigt (EvaluatedAt gesetzt, ohne Kennzahlen) -
    /// sonst lüde ihn die Nachauswertung bei jedem Serverstart erneut.
    /// </summary>
    private async Task<bool> EvaluateAndPersistAsync(GpsWalkRun walkRun, CancellationToken ct)
    {
        var laidPoints = await db.GpsPoints
            .Where(p => p.TrackId == walkRun.TrackId)
            .ToListAsync(ct);

        var walkPoints = walkRun.Points.Count > 0
            ? walkRun.Points.ToList()
            : await db.GpsWalkPoints.Where(p => p.WalkRunId == walkRun.Id).ToListAsync(ct);

        if (GpsTrackEvaluator.TryEvaluate(laidPoints, walkPoints) is not { } evaluation)
        {
            walkRun.EvaluatedAt = DateTimeOffset.UtcNow;
            return false;
        }

        var byId = walkPoints.ToDictionary(p => p.Id);
        foreach (var evaluated in evaluation.Points)
        {
            if (byId.TryGetValue(evaluated.PointId, out var point))
                point.DeviationMeters = evaluated.DeviationMeters;
        }

        walkRun.AvgDeviationMeters = evaluation.AvgDeviationMeters;
        walkRun.MaxDeviationMeters = evaluation.MaxDeviationMeters;
        walkRun.OnTrackPercent = evaluation.OnTrackPercent;
        walkRun.ArticlesFound = evaluation.ArticlesFound;
        walkRun.ArticlesTotal = evaluation.ArticlesTotal;
        walkRun.EvaluatedAt = DateTimeOffset.UtcNow;

        // Alte Halte hart entfernen statt soft-deleten: sie sind reine
        // Rechenergebnisse ohne eigenen Verlauf und würden sonst bei jeder
        // Neuberechnung als Leichen mitwachsen.
        var existingStops = await db.GpsWalkStops.Where(s => s.WalkRunId == walkRun.Id).ToListAsync(ct);
        if (existingStops.Count > 0) db.GpsWalkStops.RemoveRange(existingStops);
        walkRun.Stops.Clear();

        foreach (var stop in evaluation.Stops)
        {
            // Über das DbSet mit explizitem FK, nicht über die getrackte
            // Navigation (sonst stuft EF die neuen Zeilen per Collection-Fixup
            // als Modified statt Added ein - siehe TrainingService.CreateAsync).
            db.GpsWalkStops.Add(new GpsWalkStop
            {
                WalkRunId = walkRun.Id,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                DurationSeconds = stop.DurationSeconds,
                Kind = stop.Kind,
                MarkerLabel = stop.MarkerLabel
            });
        }

        return true;
    }

    public async Task<Result<GpsWalkRunDto>> UpdateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, UpdateGpsWalkRunRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.NotFound("Fährte nicht gefunden.");

        var walkRun = await db.GpsWalkRuns
            .Include(r => r.Points)
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunDto>.NotFound("Ablauf-Versuch nicht gefunden.");

        // Nur der Kommentar ist editierbar - die GPS-Punkte einer Aufzeichnung
        // sind Messdaten und werden bewusst nicht nachträglich verändert.
        if (Textlaengen.ZuLang(request.Comment, Textlaengen.FaehrtenKommentar, "Der Kommentar") is { } zuLang)
            return Result<GpsWalkRunDto>.Failure(zuLang);
        var comment = request.Comment?.Trim();
        walkRun.Comment = string.IsNullOrEmpty(comment) ? null : comment;
        await db.SaveChangesAsync(ct);

        return Result<GpsWalkRunDto>.Success(ToWalkRunDto(walkRun));
    }

    public async Task<Result<GpsTrackDto>> RefreshWeatherAsync(Guid userId, Guid trackId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks
            .Include(t => t.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Stops)
            .FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsTrackDto>.NotFound("Fährte nicht gefunden.");

        await weather.EnrichTrackAsync(track, ct);
        await db.SaveChangesAsync(ct);

        return Result<GpsTrackDto>.Success(ToDto(track, simplify: true));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid trackId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result.NotFound("Fährte nicht gefunden.");

        // Mit Punkten, Abläufen und Stockungen - sonst blieben sie als Rückstand
        // in der Datenbank (siehe GpsTrackRemoval).
        await GpsTrackRemoval.WeichLoeschenAsync(db, [track.Id], DateTimeOffset.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<bool> HasSessionAccessAsync(Guid userId, Guid trainingSessionId, CancellationToken ct)
    {
        var dogId = await db.TrainingSessions
            .Where(s => s.Id == trainingSessionId)
            .Select(s => s.DogId)
            .FirstOrDefaultAsync(ct);

        return dogId != Guid.Empty && await db.HasDogAccessAsync(userId, dogId, ct);
    }

    private static GpsTrackDto ToDto(GpsTrack t, bool simplify = false)
    {
        var ordered = t.Points.OrderBy(p => p.Timestamp).ToList();
        var points = ordered;
        if (simplify)
        {
            // Manuelle Marker (gelegte Gegenstände) bleiben immer vollständig
            // erhalten - nur die automatische GPS-Linie wird reduziert.
            var automatic = ordered.Where(p => p.PointType != GpsPointType.Manual).ToList();
            var manual = ordered.Where(p => p.PointType == GpsPointType.Manual);
            points = GpsTrackSimplifier.Simplify(automatic).Concat(manual).OrderBy(p => p.Timestamp).ToList();
        }

        return new(
            t.Id,
            t.TrainingSessionId,
            t.LengthMeters,
            t.AgeMinutes,
            t.Surface,
            t.Weather,
            t.Wind,
            t.Comment,
            points.Select(p => new GpsPointDto(p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, p.PointType, p.Label, p.MarkerType)).ToList(),
            t.WalkRuns.OrderBy(r => r.CreatedAt).Select(r => ToWalkRunDto(r, simplify)).ToList(),
            t.LaidTemperatureC,
            t.LaidRelativeHumidity,
            t.LaidWindSpeedKmh,
            t.LaidWeatherCode,
            t.SearchTemperatureC,
            t.SearchRelativeHumidity,
            t.SearchWindSpeedKmh,
            t.SearchWeatherCode,
            t.TemperatureDeltaC,
            t.WeatherFetchedAt);
    }

    private static GpsWalkRunDto ToWalkRunDto(GpsWalkRun r, bool simplify = false)
    {
        var ordered = r.Points.OrderBy(p => p.Timestamp).ToList();
        var points = simplify ? GpsTrackSimplifier.Simplify(ordered) : ordered;

        return new(
            r.Id,
            r.TrackId,
            r.CreatedAt,
            r.LengthMeters,
            r.Comment,
            points.Select(p => new GpsWalkPointDto(p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, p.DeviationMeters)).ToList(),
            r.AvgDeviationMeters,
            r.MaxDeviationMeters,
            r.OnTrackPercent,
            r.ArticlesFound,
            r.ArticlesTotal,
            r.EvaluatedAt,
            r.Stops.OrderByDescending(s => s.DurationSeconds)
                .Select(s => new GpsWalkStopDto(s.Latitude, s.Longitude, s.DurationSeconds, s.Kind, s.MarkerLabel))
                .ToList());
    }
}
