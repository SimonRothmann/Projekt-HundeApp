using System.Data.Common;
using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Application.Training;
using Dogity.Application.Weather;
using Dogity.Domain.Tracking;
using Dogity.Domain.Training;
using Microsoft.EntityFrameworkCore;
using static Dogity.Application.Common.SqlStates;

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

    /// <summary>Längstes Trailalter, das sich eintragen lässt: eine Woche (in Minuten).</summary>
    public const int MaxTrailAlterMinuten = 7 * 24 * 60;

    private const string VerdecktNachDemLegen = "Verdeckt absuchen stellst du nach dem Legen in den Fährtendetails ein.";
    private const string NurMantrailing = "Diese Angaben gibt es nur beim Mantrailing.";
    private const string NurSchweissfaehrte = "Diese Angaben gibt es nur bei der Schweißfährte.";
    private const string NurFaehrte = "Diese Angabe gibt es nur bei der Fährte.";
    private const string TrailalterBereich = "Das Trailalter muss zwischen 0 und 168 Stunden liegen.";

    private static readonly string ZuVielePunkte = ZuVielePunkteMeldung(MaxPunkteJeAufzeichnung);

    private string TagZuVoll => TagZuVollMeldung(punkteJeTrainingstag);

    // Die Werte stehen als einfache Namen im Satz: Das Frontend übersetzt die
    // Meldung über genau diesen Wortlaut (siehe i18n.test.ts, Platzhalter).
    private static string ZuVielePunkteMeldung(int grenze)
    {
        var hoechstens = MitTausenderpunkt(grenze);
        return $"Die Aufzeichnung hat zu viele Punkte (höchstens {hoechstens}).";
    }

    private static string TagZuVollMeldung(int grenze)
    {
        var hoechstens = MitTausenderpunkt(grenze);
        return $"Für diesen Trainingstag sind schon zu viele GPS-Punkte gespeichert (höchstens {hoechstens}).";
    }

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
            .Include(t => t.WalkRuns).ThenInclude(r => r.Legs)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Corners)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Articles)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);

        // Vereinfachung nur auf dem Lesepfad - der Client hat die Rohpunkte
        // einer soeben erstellten Aufzeichnung (CreateAsync/AddWalkRunAsync)
        // bereits selbst im Speicher, eine Vereinfachung dort brächte nichts.
        return Result<IReadOnlyList<GpsTrackDto>>.Success(tracks.Select(t => ToDto(t, simplify: true)).ToList());
    }

    public async Task<Result<IReadOnlyList<WartendeSpurDto>>> GetWartendeAsync(
        Guid userId, IReadOnlyCollection<Guid> dogIds, DateOnly einheitenAb, DateOnly einheitenBis, DateTimeOffset jetzt, CancellationToken ct = default)
    {
        // Zugriff wie überall über die zentrale Regel; die Einheiten werden gleich in der Abfrage darauf beschränkt.
        var einheiten = await db.TrainingSessions
            .Where(s => dogIds.Contains(s.DogId) && s.Date >= einheitenAb && s.Date <= einheitenBis
                && db.ZugaenglicheHundeIds(userId).Contains(s.DogId))
            .Select(s => new { s.Id, s.DogId, s.Date })
            .ToListAsync(ct);
        if (einheiten.Count == 0)
            return Result<IReadOnlyList<WartendeSpurDto>>.Success([]);
        var hundJeEinheit = einheiten.ToDictionary(e => e.Id, e => e.DogId);
        var tagJeEinheit = einheiten.ToDictionary(e => e.Id, e => e.Date);
        var einheitIds = hundJeEinheit.Keys.ToList();

        // Grobe Vorauswahl in SQL (längste Frist, damit keine Punkte abgelaufener Spuren geladen werden);
        // die genaue Frist je Art prüft Ablauffristen danach. Nur eine Collection per Include: keine
        // Zeilenvervielfachung, daher ohne AsSplitQuery.
        var fruehestesEnde = jetzt - Ablauffristen.Schweissfaehrte;
        var spuren = await db.GpsTracks
            .Where(t => einheitIds.Contains(t.TrainingSessionId)
                && !db.GpsWalkRuns.Any(r => r.TrackId == t.Id)
                && t.Points.Where(p => p.PointType != GpsPointType.Manual).Max(p => (DateTimeOffset?)p.Timestamp) >= fruehestesEnde)
            .Include(t => t.Points)
            .AsNoTracking()
            .ToListAsync(ct);

        var wartende = new List<WartendeSpurDto>();
        // Neueste Einheit zuerst, wie die Einheitenliste, aus der die Startseite die Spuren früher zog.
        foreach (var t in spuren.OrderByDescending(t => tagJeEinheit[t.TrainingSessionId]))
        {
            // Dieselbe Vereinfachung wie auf dem Lesepfad der Fährtenliste - die Antwort bleibt inhaltlich gleich.
            var punkte = ToDto(t, simplify: true).Points;
            if (!Ablauffristen.NochZuSuchen(Ablauffristen.LegeEnde(punkte), t.Kind, jetzt)) continue;
            wartende.Add(new WartendeSpurDto(
                hundJeEinheit[t.TrainingSessionId], t.Id, t.TrainingSessionId, t.LengthMeters, t.AgeMinutes, t.Surface,
                t.Kind, t.DoubleBlind, t.NegativeTrail, t.ScentArticle, punkte,
                t.Verfahren, t.Wildart, t.SchweissMl, t.StehzeitStunden));
        }
        return Result<IReadOnlyList<WartendeSpurDto>>.Success(wartende);
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

            // Wurde die Fährte inzwischen gelöscht, nicht neu anlegen: Dieselbe Id
            // liegt als weich gelöschte Zeile noch vor (Schlüsselkonflikt), und die
            // Löschung war gewollt. Die 404 lässt das Frontend den Eintrag verwerfen.
            if (await db.GpsTracks.IgnoreQueryFilters().AnyAsync(t => t.Id == vorhandeneId, ct))
                return Result<GpsTrackDto>.NotFound("Fährte wurde inzwischen gelöscht.");
        }

        Guid sessionId;
        TrainingSession? neueEinheit = null;
        TrainingSession? einheitDesTages = null;

        if (request.TrainingSessionId is { } angegebeneEinheit)
        {
            // Eine Fährte ändert das Training, an dem sie hängt (TrainingsRechte).
            if (!await DarfEinheitBearbeitenAsync(userId, angegebeneEinheit, ct))
                return Result<GpsTrackDto>.NotFound("Training nicht gefunden.");
            sessionId = angegebeneEinheit;
        }
        else if (request.DogId is { } dogId && request.Date is { } date)
        {
            if (!await db.HasDogAccessAsync(userId, dogId, ct))
                return Result<GpsTrackDto>.NotFound("Hund nicht gefunden.");

            // Dieselbe Regel wie beim Eintragen eines Trainings: ein Tag,
            // eine Einheit. Eine zweite oder dritte Fährte derselben
            // Übungsstunde gehört dazu und bekommt keine eigene.
            einheitDesTages = await db.EinheitDesTagesAsync(userId, dogId, date, ct);

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
            ?? Textlaengen.ZuLang(request.GroundCondition, Textlaengen.Kurzangabe, "Der Bodenzustand")
            ?? Textlaengen.ZuLang(request.Weather, Textlaengen.Kurzangabe, "Die Wetterangabe")
            ?? Textlaengen.ZuLang(request.Wind, Textlaengen.Kurzangabe, "Die Windangabe")
            ?? request.Points
                .Select(p => Textlaengen.ZuLang(p.Label, Textlaengen.MarkerBeschriftung, "Die Beschriftung eines Markers"))
                .FirstOrDefault(m => m is not null);
        if (zuLang is not null)
            return Result<GpsTrackDto>.Failure(zuLang);

        // Die Mantrailing-Angaben gehören nur an eine Mantrailing-Spur: An einer Fährte
        // blieben sie ohne Bedeutung stehen und täuschten Auswertungen vor, die es nicht gibt.
        if (!Enum.IsDefined(request.Kind))
            return Result<GpsTrackDto>.Failure("Unbekannte Art der Aufzeichnung.");
        if (request.Kind == GpsTrackKind.Faehrte && request.DoubleBlind)
            return Result<GpsTrackDto>.Failure(VerdecktNachDemLegen);
        if (request.Kind == GpsTrackKind.Schweissfaehrte && request.DoubleBlind)
            return Result<GpsTrackDto>.Failure(NurFaehrte);
        if (request.Kind != GpsTrackKind.Mantrailing
            && (request.NegativeTrail || !string.IsNullOrWhiteSpace(request.ScentArticle)))
            return Result<GpsTrackDto>.Failure(NurMantrailing);
        if (request.Kind == GpsTrackKind.Mantrailing && request.AgeMinutes is < 0 or > MaxTrailAlterMinuten)
            return Result<GpsTrackDto>.Failure(TrailalterBereich);
        if (Textlaengen.ZuLang(request.ScentArticle, Textlaengen.Geruchsartikel, "Der Geruchsartikel") is { } artikelZuLang)
            return Result<GpsTrackDto>.Failure(artikelZuLang);

        var verfahren = NullWennLeer(request.Verfahren);
        var wildart = NullWennLeer(request.Wildart);
        if (request.Kind != GpsTrackKind.Schweissfaehrte
            && (verfahren is not null || wildart is not null || request.SchweissMl is not null || request.StehzeitStunden is not null))
            return Result<GpsTrackDto>.Failure(NurSchweissfaehrte);
        if (verfahren is not null && !Schweissangaben.Verfahren.Contains(verfahren))
            return Result<GpsTrackDto>.Failure("Unbekanntes Verfahren.");
        if (wildart is not null && !Schweissangaben.Wildarten.Contains(wildart))
            return Result<GpsTrackDto>.Failure("Unbekannte Wildart.");
        if (request.SchweissMl is < 0 or > Schweissangaben.MaxMl)
            return Result<GpsTrackDto>.Failure("Die Schweißmenge muss zwischen 0 und 1000 ml liegen.");
        if (request.StehzeitStunden is < 1 or > Schweissangaben.MaxStehzeitStunden)
            return Result<GpsTrackDto>.Failure("Die Stehzeit muss zwischen 1 und 72 Stunden liegen.");

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
            // Der Bodenzustand gehört zur Fährte und Schweißfährte; ein Trail kennt ihn nicht.
            GroundCondition = request.Kind != GpsTrackKind.Mantrailing ? NullWennLeer(request.GroundCondition) : null,
            Weather = request.Weather,
            Wind = request.Wind,
            Comment = request.Comment,
            Kind = request.Kind,
            DoubleBlind = request.DoubleBlind,
            NegativeTrail = request.NegativeTrail,
            ScentArticle = NullWennLeer(request.ScentArticle),
            Verfahren = verfahren,
            Wildart = wildart,
            SchweissMl = request.SchweissMl,
            StehzeitStunden = request.StehzeitStunden
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
        // in den Punkten) - im Hintergrund, nach dem Speichern: Das Speichern
        // wartet nicht auf den Wetterdienst. Schlägt der Abruf fehl, bleibt es leer.
        await weather.EnqueueTrackAsync(track, ct);

        return Result<GpsTrackDto>.Success(ToDto(track));
    }

    public async Task<Result<GpsWalkRunDto>> AddWalkRunAsync(Guid userId, Guid trackId, CreateGpsWalkRunRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.NotFound("Fährte nicht gefunden.");

        // Wiederholte Anfrage aus der Offline-Warteschlange (Antwort ging verloren): Der Ablauf
        // ist schon da, er wird wie beim ersten Mal beantwortet - sonst scheiterte die Wiederholung
        // gleich an der Regel "nur einmal abgelaufen" und der Eintrag bliebe als abgelehnt liegen.
        if (request.Id is { } vorhandeneId && await VorhandenerAblaufAsync(track, vorhandeneId, ct) is { } vorhanden)
            return vorhanden;

        // Eine Spur wird nur einmal abgelaufen: Danach ist sie vom ersten Ablauf
        // überlaufen, ein zweiter ergäbe keine vergleichbare Auswertung (Entscheidung
        // des Betreibers 2026-11-15). Ältere Spuren mit mehreren Abläufen bleiben.
        if (await db.GpsWalkRuns.AnyAsync(r => r.TrackId == trackId, ct))
            return Result<GpsWalkRunDto>.Failure("Diese Spur wurde schon abgelaufen.");

        if (request.Points.Count == 0)
            return Result<GpsWalkRunDto>.Failure("Ein Ablauf-Versuch benötigt mindestens einen GPS-Punkt.");
        if (request.Points.Count > MaxPunkteJeAufzeichnung)
            return Result<GpsWalkRunDto>.Failure(ZuVielePunkte);
        if (await PunkteDerEinheitAsync(track.TrainingSessionId, ct) + request.Points.Count > punkteJeTrainingstag)
            return Result<GpsWalkRunDto>.Failure(TagZuVoll);
        if (Textlaengen.ZuLang(request.Comment, Textlaengen.FaehrtenKommentar, "Der Kommentar") is { } zuLang)
            return Result<GpsWalkRunDto>.Failure(zuLang);

        // Die Frist (Ablauffristen) gilt für den Beginn des Ablaufs, nicht für den Zeitpunkt der
        // Anfrage: Ein offline aufgezeichneter Ablauf kommt oft erst Stunden später an und
        // darf deshalb nicht scheitern. Ohne automatischen Punkt lässt sich nichts prüfen.
        var legeEnde = await db.GpsPoints
            .Where(p => p.TrackId == trackId && p.PointType == GpsPointType.Automatic)
            .MaxAsync(p => (DateTimeOffset?)p.Timestamp, ct);
        if (legeEnde is not null && !Ablauffristen.NochZuSuchen(legeEnde, track.Kind, request.Points.Min(p => p.Timestamp)))
            return Result<GpsWalkRunDto>.Failure("Diese Spur wurde zu lange vor dem Ablauf gelegt, um sie noch abzulaufen.");

        var walkRun = new GpsWalkRun
        {
            TrackId = trackId,
            LengthMeters = request.LengthMeters,
            Comment = request.Comment
        };
        if (request.Id is { } neueId)
            walkRun.Id = neueId;

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
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (request.Id is not null
            && e.InnerException is DbException { SqlState: EindeutigerIndexVerletzt })
        {
            // Dieselbe Kennung zweimal gleichzeitig (Wiederholung, während die erste Antwort noch läuft):
            // Die andere war schneller, ihr Ablauf ist der eine gültige.
            if (await VorhandenerAblaufAsync(track, request.Id.Value, ct) is not { } schnellerer) throw;
            return schnellerer;
        }

        // Auswertung direkt nach dem Speichern - die gelegten Punkte liegen
        // dafür ohnehin schon in der Datenbank.
        await EvaluateAndPersistAsync(walkRun, track.Kind, ct);
        await db.SaveChangesAsync(ct);

        // Erst mit dem ersten Ablauf gibt es einen Suchzeitpunkt - jetzt lässt
        // sich die Temperaturänderung Legen -> Suchen bestimmen (im Hintergrund).
        await weather.EnqueueTrackAsync(track, ct);

        return Result<GpsWalkRunDto>.Success(await ToWalkRunDtoAsync(track, walkRun, ct));
    }

    /// <summary>Der schon unter dieser Kennung angelegte Ablauf, null wenn es keinen gibt; gehört er zu einer anderen Fährte, ein 404.</summary>
    private async Task<Result<GpsWalkRunDto>?> VorhandenerAblaufAsync(GpsTrack track, Guid walkRunId, CancellationToken ct)
    {
        var vorhanden = await db.GpsWalkRuns
            .Include(r => r.Points)
            .Include(r => r.Stops)
            .Include(r => r.Legs)
            .Include(r => r.Corners)
            .Include(r => r.Articles)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == walkRunId, ct);
        if (vorhanden is null) return null;
        return vorhanden.TrackId == track.Id
            ? Result<GpsWalkRunDto>.Success(await ToWalkRunDtoAsync(track, vorhanden, ct))
            : Result<GpsWalkRunDto>.NotFound("Fährte nicht gefunden.");
    }

    public async Task<Result<GpsWalkRunDto>> EvaluateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.NotFound("Fährte nicht gefunden.");

        var walkRun = await db.GpsWalkRuns
            .Include(r => r.Points)
            .Include(r => r.Stops)
            .Include(r => r.Legs)
            .Include(r => r.Corners)
            .Include(r => r.Articles)
            // AsSplitQuery: mehrere Listen in einer Abfrage vervielfachen die Zeilen (siehe GetByTrainingSessionAsync).
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunDto>.NotFound("Ablauf-Versuch nicht gefunden.");

        var ausgewertet = await EvaluateAndPersistAsync(walkRun, track.Kind, ct);
        await db.SaveChangesAsync(ct);
        if (!ausgewertet)
            return Result<GpsWalkRunDto>.Failure("Dieser Ablauf ist zu groß für die Auswertung.");

        return Result<GpsWalkRunDto>.Success(await ToWalkRunDtoAsync(track, walkRun, ct));
    }

    /// <summary>
    /// Wertet einen Ablauf gegen seine gelegte Fährte aus und schreibt das
    /// Ergebnis auf die Entitäten (ohne SaveChanges - der Aufrufer entscheidet,
    /// wann gespeichert wird). Bestehende Halte, Schenkel und Winkel werden ersetzt.
    ///
    /// <c>false</c>, wenn die Auswertung zu aufwendig wäre (siehe
    /// <see cref="GpsTrackEvaluator.MaxAuswertungsAufwand"/>). Der Ablauf gilt
    /// dann trotzdem als erledigt (EvaluatedAt gesetzt, ohne Kennzahlen) -
    /// sonst lüde ihn die Nachauswertung bei jedem Serverstart erneut.
    /// </summary>
    private async Task<bool> EvaluateAndPersistAsync(GpsWalkRun walkRun, GpsTrackKind kind, CancellationToken ct)
    {
        var laidPoints = await db.GpsPoints
            .Where(p => p.TrackId == walkRun.TrackId)
            .ToListAsync(ct);

        var walkPoints = walkRun.Points.Count > 0
            ? walkRun.Points.ToList()
            : await db.GpsWalkPoints.Where(p => p.WalkRunId == walkRun.Id).ToListAsync(ct);

        if (GpsTrackEvaluator.TryEvaluate(laidPoints, walkPoints, kind) is not { } evaluation)
        {
            walkRun.EvaluatedAt = DateTimeOffset.UtcNow;
            walkRun.EvaluationVersion = GpsTrackEvaluator.AktuelleVersion;
            return false;
        }

        await WalkRunEvaluationWriter.WriteAsync(db, walkRun, walkPoints, evaluation, ct);

        return true;
    }

    public async Task<Result<GpsWalkRunAngabenDto>> UpdateWalkRunAsync(Guid userId, Guid trackId, Guid walkRunId, UpdateGpsWalkRunRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunAngabenDto>.NotFound("Fährte nicht gefunden.");

        // Ohne die Punkte: Geändert wird nur der Kommentar, und die Antwort trägt nur ihn.
        var walkRun = await db.GpsWalkRuns
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunAngabenDto>.NotFound("Ablauf-Versuch nicht gefunden.");

        // Nur der Kommentar ist editierbar - die GPS-Punkte einer Aufzeichnung
        // sind Messdaten und werden bewusst nicht nachträglich verändert.
        if (Textlaengen.ZuLang(request.Comment, Textlaengen.FaehrtenKommentar, "Der Kommentar") is { } zuLang)
            return Result<GpsWalkRunAngabenDto>.Failure(zuLang);
        var comment = request.Comment?.Trim();
        walkRun.Comment = string.IsNullOrEmpty(comment) ? null : comment;
        await db.SaveChangesAsync(ct);

        return Result<GpsWalkRunAngabenDto>.Success(ToAngaben(walkRun));
    }

    // Rechte der beiden Trail-Routen: Die Angaben zum Trail (Alter, Doppelblind, Negativ, Geruchsartikel)
    // gehören zum Eintrag und ändert nur, wer das Training bearbeiten darf (TrainingsRechte); betreuende
    // Trainer:innen sehen Einträge der Familie nur. Das Ergebnis eines Laufs (Fund, Anzeige) darf, wer den
    // Hund sehen darf - wie der Kommentar eines Ablaufs (UpdateWalkRunAsync).
    public async Task<Result<GpsTrackAngabenDto>> UpdateTrailAsync(Guid userId, Guid trackId, UpdateGpsTrailRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await DarfEinheitBearbeitenAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsTrackAngabenDto>.NotFound("Fährte nicht gefunden.");
        if (track.Kind != GpsTrackKind.Mantrailing)
            return Result<GpsTrackAngabenDto>.Failure(NurMantrailing);
        if (request.AgeMinutes is < 0 or > MaxTrailAlterMinuten)
            return Result<GpsTrackAngabenDto>.Failure(TrailalterBereich);
        if (Textlaengen.ZuLang(request.ScentArticle, Textlaengen.Geruchsartikel, "Der Geruchsartikel") is { } zuLang)
            return Result<GpsTrackAngabenDto>.Failure(zuLang);

        track.AgeMinutes = request.AgeMinutes;
        track.DoubleBlind = request.DoubleBlind;
        track.NegativeTrail = request.NegativeTrail;
        track.ScentArticle = NullWennLeer(request.ScentArticle);
        await db.SaveChangesAsync(ct);

        return Result<GpsTrackAngabenDto>.Success(ToAngaben(track));
    }

    public async Task<Result<GpsTrackAngabenDto>> UpdateVerdecktAsync(Guid userId, Guid trackId, UpdateGpsVerdecktRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await DarfEinheitBearbeitenAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsTrackAngabenDto>.NotFound("Fährte nicht gefunden.");
        // Der Trail hat seinen eigenen Weg (UpdateTrailAsync, "Doppelblind" mit den übrigen Trail-Angaben).
        if (track.Kind != GpsTrackKind.Faehrte)
            return Result<GpsTrackAngabenDto>.Failure(NurFaehrte);
        // Nach einem Ablauf wäre die Angabe rückwirkend: Der Ablauf hätte die Fährte gesehen, die jetzt
        // als verdeckt gälte. Ablaufen darf man sie danach erst wieder in einem neuen Versuch.
        if (await db.GpsWalkRuns.AnyAsync(r => r.TrackId == trackId, ct))
            return Result<GpsTrackAngabenDto>.Failure("Verdeckt absuchen lässt sich nur einstellen, solange die Fährte noch nicht abgelaufen wurde.");

        track.DoubleBlind = request.Verdeckt;
        await db.SaveChangesAsync(ct);

        return Result<GpsTrackAngabenDto>.Success(ToAngaben(track));
    }

    public async Task<Result<GpsWalkRunAngabenDto>> UpdateTrailRunAsync(Guid userId, Guid trackId, Guid walkRunId, UpdateGpsTrailRunRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunAngabenDto>.NotFound("Fährte nicht gefunden.");
        if (track.Kind != GpsTrackKind.Mantrailing)
            return Result<GpsWalkRunAngabenDto>.Failure(NurMantrailing);
        if (request.Indication is { } anzeige && !Enum.IsDefined(anzeige))
            return Result<GpsWalkRunAngabenDto>.Failure("Unbekanntes Anzeigeverhalten.");

        var walkRun = await db.GpsWalkRuns
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunAngabenDto>.NotFound("Ablauf-Versuch nicht gefunden.");

        walkRun.PersonFound = request.PersonFound;
        // Ein Anzeigeverhalten gibt es nur, wo eine Person gefunden wurde - nicht beim
        // Negativ-Trail und nicht ohne Fund. Sonst bliebe es nach einer Korrektur stehen.
        walkRun.Indication = request.PersonFound == true && !track.NegativeTrail ? request.Indication : null;
        await db.SaveChangesAsync(ct);

        return Result<GpsWalkRunAngabenDto>.Success(ToAngaben(walkRun));
    }

    // Rechte wie beim Bearbeiten der Fährte (UpdateTrailAsync): Material und Herausnehmen aus der Entwicklung
    // gehören zum Eintrag. Die Angaben je Gegenstand im Ablauf darf, wer den Hund sehen darf - wie das Ergebnis
    // eines Trail-Laufs und der Kommentar eines Ablaufs.
    public async Task<Result<GpsPointDto>> UpdateMarkerAsync(Guid userId, Guid trackId, Guid pointId, UpdateGpsMarkerRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await DarfEinheitBearbeitenAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsPointDto>.NotFound("Fährte nicht gefunden.");
        if (track.Kind != GpsTrackKind.Faehrte)
            return Result<GpsPointDto>.Failure(NurFaehrte);

        var punkt = await db.GpsPoints.FirstOrDefaultAsync(p => p.Id == pointId && p.TrackId == trackId, ct);
        if (punkt is null)
            return Result<GpsPointDto>.NotFound("Marker nicht gefunden.");
        if (punkt.PointType != GpsPointType.Manual || punkt.MarkerType != GpsMarkerType.Article)
            return Result<GpsPointDto>.Failure("Das Material lässt sich nur bei einem Gegenstand eintragen.");

        var material = NullWennLeer(request.Material);
        if (material is not null && !GegenstandsMaterial.Alle.Contains(material))
            return Result<GpsPointDto>.Failure("Unbekanntes Material.");

        punkt.Material = material;
        await db.SaveChangesAsync(ct);

        return Result<GpsPointDto>.Success(ToPointDto(punkt));
    }

    public async Task<Result> DeleteMarkerAsync(Guid userId, Guid trackId, Guid pointId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await DarfEinheitBearbeitenAsync(userId, track.TrainingSessionId, ct))
            return Result.NotFound("Fährte nicht gefunden.");

        var punkt = await db.GpsPoints.FirstOrDefaultAsync(p => p.Id == pointId && p.TrackId == trackId, ct);
        if (punkt is null || punkt.PointType != GpsPointType.Manual)
            return Result.NotFound("Marker nicht gefunden.");

        var laeufe = await db.GpsWalkRuns
            .Where(r => r.TrackId == trackId)
            .Include(r => r.Points)
            .Include(r => r.Stops)
            .Include(r => r.Legs)
            .Include(r => r.Corners)
            .Include(r => r.Articles)
            .AsSplitQuery()
            .ToListAsync(ct);

        try
        {
            // Eine Transaktion, damit nie ein Marker fehlt, dessen Abläufe noch mit ihm ausgewertet sind.
            await db.InTransaktionAsync(async () =>
            {
                // Die Angaben je Gegenstand haben keinen FK auf den Punkt und blieben sonst verwaist.
                db.GpsWalkRunArticles.RemoveRange(laeufe.SelectMany(r => r.Articles).Where(a => a.PointId == pointId));
                db.GpsPoints.Remove(punkt);
                await db.SaveChangesAsync(ct);

                // Erst nach dem Speichern: Die Auswertung liest die gelegten Punkte aus der Datenbank.
                foreach (var lauf in laeufe)
                    await EvaluateAndPersistAsync(lauf, track.Kind, ct);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Zweiter gleichzeitiger Aufruf: Der Marker ist schon weg.
            return Result.NotFound("Marker nicht gefunden.");
        }
        return Result.Success();
    }

    public async Task<Result<GpsWalkRunDto>> UpdateWalkRunArticlesAsync(Guid userId, Guid trackId, Guid walkRunId, IReadOnlyList<UpdateGpsWalkRunArticleRequest> request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await HasSessionAccessAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsWalkRunDto>.NotFound("Fährte nicht gefunden.");
        if (track.Kind != GpsTrackKind.Faehrte)
            return Result<GpsWalkRunDto>.Failure(NurFaehrte);

        var walkRun = await db.GpsWalkRuns
            .Include(r => r.Points)
            .Include(r => r.Stops)
            .Include(r => r.Legs)
            .Include(r => r.Corners)
            .Include(r => r.Articles)
            // AsSplitQuery: mehrere Listen in einer Abfrage vervielfachen die Zeilen (siehe GetByTrainingSessionAsync).
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == walkRunId && r.TrackId == trackId, ct);
        if (walkRun is null)
            return Result<GpsWalkRunDto>.NotFound("Ablauf-Versuch nicht gefunden.");

        if (request.Any(a => a is null))
            return Result<GpsWalkRunDto>.Failure("Eine Angabe fehlt.");

        // Alles oder nichts: Eine fremde Id oder ein Marker, der kein Gegenstand ist, lehnt die ganze Anfrage ab.
        var gegenstandIds = await db.GpsPoints
            .Where(p => p.TrackId == trackId && p.PointType == GpsPointType.Manual && p.MarkerType == GpsMarkerType.Article)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (request.Any(a => !gegenstandIds.Contains(a.PointId)))
            return Result<GpsWalkRunDto>.Failure("Die Angabe gehört zu keinem Gegenstand dieser Fährte.");
        if (request.Any(a => a.Result is { } ergebnis && !Enum.IsDefined(ergebnis)))
            return Result<GpsWalkRunDto>.Failure("Unbekanntes Ergebnis.");

        // Je Gegenstand zählt die letzte Angabe der Anfrage.
        foreach (var angabe in request.GroupBy(a => a.PointId).Select(g => g.Last()))
        {
            var vorhanden = walkRun.Articles.FirstOrDefault(a => a.PointId == angabe.PointId);
            if (angabe.Result is not { } neu)
            {
                // Keine Angabe heißt: keine Zeile - die Eingabe wird entfernt, nicht aufbewahrt.
                if (vorhanden is not null)
                    db.GpsWalkRunArticles.Remove(vorhanden);
            }
            else if (vorhanden is not null)
            {
                vorhanden.Result = neu;
            }
            else
            {
                // Über das DbSet mit explizitem FK, nicht über die Navigation (siehe WalkRunEvaluationWriter).
                db.GpsWalkRunArticles.Add(new GpsWalkRunArticle { WalkRunId = walkRun.Id, PointId = angabe.PointId, Result = neu });
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IstGleichzeitigerKonflikt(ex))
        {
            // Zwischendurch hat eine andere Anfrage dieselbe Angabe erstmals angelegt (Unique-Index
            // Ablauf + Gegenstand); ein neuer Versuch sieht sie. Wie im ClubWorkService eine
            // Eingabemeldung statt eines 500.
            return Result<GpsWalkRunDto>.Failure("Das Speichern hat sich mit einer anderen Änderung überschnitten. Bitte versuche es noch einmal.");
        }
        return Result<GpsWalkRunDto>.Success(await ToWalkRunDtoAsync(track, walkRun, ct));
    }

    public async Task<Result<GpsTrackAngabenDto>> UpdateStatsAsync(Guid userId, Guid trackId, UpdateGpsTrackStatsRequest request, CancellationToken ct = default)
    {
        var track = await db.GpsTracks.FirstOrDefaultAsync(t => t.Id == trackId, ct);
        if (track is null || !await DarfEinheitBearbeitenAsync(userId, track.TrainingSessionId, ct))
            return Result<GpsTrackAngabenDto>.NotFound("Fährte nicht gefunden.");
        if (track.Kind != GpsTrackKind.Faehrte)
            return Result<GpsTrackAngabenDto>.Failure(NurFaehrte);

        track.ExcludeFromStats = request.ExcludeFromStats;
        await db.SaveChangesAsync(ct);

        return Result<GpsTrackAngabenDto>.Success(ToAngaben(track));
    }

    /// <summary>Ob ein Speichern an einer gleichzeitigen Anfrage gescheitert ist (eindeutiger Index oder Verklemmung, auch verpackt).</summary>
    private static bool IstGleichzeitigerKonflikt(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is DbException { SqlState: EindeutigerIndexVerletzt or Verklemmung })
                return true;
        return false;
    }

    private static string? NullWennLeer(string? text)
    {
        var getrimmt = text?.Trim();
        return string.IsNullOrEmpty(getrimmt) ? null : getrimmt;
    }

    public async Task<Result<GpsTrackDto>> RefreshWeatherAsync(Guid userId, Guid trackId, CancellationToken ct = default)
    {
        var track = await db.GpsTracks
            .Include(t => t.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Points)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Stops)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Legs)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Corners)
            .Include(t => t.WalkRuns).ThenInclude(r => r.Articles)
            // AsSplitQuery: mehrere Listen in einer Abfrage vervielfachen die Zeilen (siehe GetByTrainingSessionAsync).
            .AsSplitQuery()
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
        if (track is null || !await DarfEinheitBearbeitenAsync(userId, track.TrainingSessionId, ct))
            return Result.NotFound("Fährte nicht gefunden.");

        // Mit Punkten, Abläufen und Stockungen - sonst blieben sie als Rückstand
        // in der Datenbank (siehe GpsTrackRemoval).
        await db.InTransaktionAsync(async () =>
        {
            await GpsTrackRemoval.WeichLoeschenAsync(db, [track.Id], DateTimeOffset.UtcNow, ct);
            await db.SaveChangesAsync(ct);
        }, ct);
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

    private Task<bool> DarfEinheitBearbeitenAsync(Guid userId, Guid trainingSessionId, CancellationToken ct) =>
        db.TrainingSessions.Where(s => s.Id == trainingSessionId).DarfBearbeiten(db, userId).AnyAsync(ct);

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
            points.Select(ToPointDto).ToList(),
            t.WalkRuns.OrderBy(r => r.CreatedAt)
                .Select(r => ToWalkRunDto(r, simplify, t.Kind == GpsTrackKind.Faehrte ? t.Points : null)).ToList(),
            t.LaidTemperatureC,
            t.LaidRelativeHumidity,
            t.LaidWindSpeedKmh,
            t.LaidWeatherCode,
            t.SearchTemperatureC,
            t.SearchRelativeHumidity,
            t.SearchWindSpeedKmh,
            t.SearchWeatherCode,
            t.TemperatureDeltaC,
            t.WeatherFetchedAt,
            t.Kind,
            t.DoubleBlind,
            t.NegativeTrail,
            t.ScentArticle,
            t.ExcludeFromStats,
            t.GroundCondition,
            t.LaidWindDirectionDeg,
            t.SearchWindDirectionDeg,
            t.Verfahren,
            t.Wildart,
            t.SchweissMl,
            t.StehzeitStunden);
    }

    private static GpsWalkRunAngabenDto ToAngaben(GpsWalkRun r) => new(r.Id, r.Comment, r.PersonFound, r.Indication);

    private static GpsTrackAngabenDto ToAngaben(GpsTrack t) =>
        new(t.Id, t.AgeMinutes, t.DoubleBlind, t.NegativeTrail, t.ScentArticle, t.ExcludeFromStats);

    private static GpsPointDto ToPointDto(GpsPoint p) =>
        new(p.Id, p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, p.PointType, p.Label, p.MarkerType, p.Material);

    /// <summary>Wie <see cref="ToWalkRunDto"/> für einen einzelnen Ablauf, samt GPS-Hinweis zu den Gegenständen der Fährte.</summary>
    private async Task<GpsWalkRunDto> ToWalkRunDtoAsync(GpsTrack track, GpsWalkRun run, CancellationToken ct) =>
        ToWalkRunDto(run, laidPoints: track.Kind == GpsTrackKind.Faehrte
            ? await db.GpsPoints.Where(p => p.TrackId == track.Id).AsNoTracking().ToListAsync(ct)
            : null);

    private static GpsWalkRunDto ToWalkRunDto(GpsWalkRun r, bool simplify = false, IEnumerable<GpsPoint>? laidPoints = null)
    {
        var ordered = r.Points.OrderBy(p => p.Timestamp).ToList();
        var reached = laidPoints is null || r.EvaluatedAt is null ? null : GpsTrackEvaluator.ErreichteGegenstaende(laidPoints, r.Points);

        return new(
            r.Id,
            r.TrackId,
            r.CreatedAt,
            r.LengthMeters,
            r.Comment,
            ToWalkPointDtos(ordered, simplify),
            r.AvgDeviationMeters,
            r.MaxDeviationMeters,
            r.OnTrackPercent,
            r.ArticlesFound,
            r.ArticlesTotal,
            r.EvaluatedAt,
            r.Stops.OrderByDescending(s => s.DurationSeconds)
                .Select(s => new GpsWalkStopDto(s.Latitude, s.Longitude, s.DurationSeconds, s.Kind, s.MarkerLabel))
                .ToList(),
            r.PersonFound,
            r.Indication,
            Legs: r.Legs.OrderBy(l => l.Number)
                .Select(l => new GpsWalkRunLegDto(l.Number, l.LengthMeters, l.DurationSeconds, l.SpeedMetersPerMinute, l.AvgDeviationMeters))
                .ToList(),
            Corners: r.Corners.OrderBy(c => c.Number)
                .Select(c => new GpsWalkRunCornerDto(
                    c.Number, c.Direction, c.Sharp, c.TurnDegrees, c.Latitude, c.Longitude, c.Reached,
                    c.OvershootMeters, c.Overshot, c.HoldSeconds, c.TimeAtCornerSeconds))
                .ToList(),
            Articles: r.Articles.Select(a => new GpsWalkRunArticleDto(a.PointId, a.Result)).ToList(),
            ArticlesConfirmedFound: r.Articles.Count == 0
                ? null
                : r.Articles.Count(a => a.Result is ArticleResult.Indicated or ArticleResult.PickedUp),
            // Nur für ausgewertete Abläufe (und nicht über der Aufwandsgrenze): sonst gibt es keinen
            // GPS-Hinweis, und die Oberfläche blendet ihn aus.
            ReachedArticleIds: reached,
            UnrecordedArticleIds: reached is null ? null : GpsTrackEvaluator.NichtAufgezeichneteGegenstaende(laidPoints!, r.Points, reached));
    }

    /// <summary>
    /// Die Ablaufpunkte fürs Ausliefern. Lücken werden auf den vollständigen Punkten erkannt und je
    /// Stück getrennt vereinfacht: So bleibt jeder Lücken-Endpunkt erhalten, und die Lücke steht als
    /// AfterGap am Punkt, statt aus vereinfachten Strecken erraten zu werden.
    /// </summary>
    private static List<GpsWalkPointDto> ToWalkPointDtos(List<GpsWalkPoint> ordered, bool simplify)
    {
        var result = new List<GpsWalkPointDto>(ordered.Count);
        var start = 0;
        foreach (var ende in GpsTrackEvaluator.LueckenAnfaenge(ordered).Append(ordered.Count))
        {
            var stueck = ordered.GetRange(start, ende - start);
            IReadOnlyList<GpsWalkPoint> punkte = simplify ? GpsTrackSimplifier.Simplify(stueck) : stueck;
            result.AddRange(punkte.Select((p, i) => new GpsWalkPointDto(
                p.Latitude, p.Longitude, p.Timestamp, p.Accuracy, p.DeviationMeters, AfterGap: start > 0 && i == 0)));
            start = ende;
        }
        return result;
    }
}
