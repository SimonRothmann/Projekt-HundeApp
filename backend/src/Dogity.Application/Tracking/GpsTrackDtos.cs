using Dogity.Domain.Tracking;

namespace Dogity.Application.Tracking;

public record GpsPointDto(
    // Id und Material: Das Material eines Gegenstands und die Angaben je Gegenstand im Ablauf
    // (GpsWalkRunArticleDto) hängen am Marker, nicht an seiner Lage.
    Guid Id,
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    double? Accuracy,
    GpsPointType PointType,
    string? Label,
    GpsMarkerType MarkerType,
    string? Material);

/// <summary>
/// DeviationMeters: senkrechter Abstand zur gelegten Fährte (null = noch nicht
/// ausgewertet). Die Karte färbt die Ablauf-Linie abschnittsweise danach ein.
/// AfterGap: Vor diesem Punkt liegt eine Lücke (siehe GpsTrackEvaluator.LueckeMinSekunden) - auf den
/// vollständigen Punkten ermittelt und ausdrücklich getragen, weil die Vereinfachung beim Ausliefern
/// sonst langsames gerades Gehen wie eine Lücke aussehen ließe.
/// </summary>
public record GpsWalkPointDto(double Latitude, double Longitude, DateTimeOffset Timestamp, double? Accuracy, double? DeviationMeters, bool AfterGap = false);

/// <summary>Ein erkannter Halt während des Ablaufs (siehe GpsWalkStop).</summary>
public record GpsWalkStopDto(
    double Latitude,
    double Longitude,
    int DurationSeconds,
    WalkStopKind Kind,
    string? MarkerLabel);

/// <summary>Ein Schenkel der Fährte im Ablauf (siehe GpsWalkRunLeg).</summary>
public record GpsWalkRunLegDto(
    int Number,
    double LengthMeters,
    int? DurationSeconds,
    double? SpeedMetersPerMinute,
    double? AvgDeviationMeters);

/// <summary>Ein Winkel der Fährte im Ablauf (siehe GpsWalkRunCorner).</summary>
public record GpsWalkRunCornerDto(
    int Number,
    CornerDirection Direction,
    bool Sharp,
    double TurnDegrees,
    double Latitude,
    double Longitude,
    bool Reached,
    double? OvershootMeters,
    bool Overshot,
    int HoldSeconds,
    int? TimeAtCornerSeconds);

/// <summary>Was der Hund mit einem Gegenstand gemacht hat (siehe GpsWalkRunArticle).</summary>
public record GpsWalkRunArticleDto(Guid PointId, ArticleResult Result);

public record GpsWalkRunDto(
    Guid Id,
    Guid TrackId,
    DateTimeOffset CreatedAt,
    double? LengthMeters,
    string? Comment,
    IReadOnlyList<GpsWalkPointDto> Points,
    // Auswertung (null, solange nicht ausgewertet - siehe GpsTrackEvaluator).
    double? AvgDeviationMeters,
    double? MaxDeviationMeters,
    double? OnTrackPercent,
    int? ArticlesFound,
    int? ArticlesTotal,
    DateTimeOffset? EvaluatedAt,
    IReadOnlyList<GpsWalkStopDto> Stops,
    // Nur Mantrailing (siehe GpsWalkRun): null, solange nicht angegeben.
    bool? PersonFound,
    TrailIndication? Indication,
    // Nur Fährte (beim Mantrailing leer): Schenkel und Winkel, in Laufreihenfolge.
    IReadOnlyList<GpsWalkRunLegDto> Legs,
    IReadOnlyList<GpsWalkRunCornerDto> Corners,
    // Nur Fährte: die Angaben der Hundeführer:in je Gegenstand. ArticlesFound bleibt die GPS-Zahl;
    // ArticlesConfirmedFound zählt, was verwiesen oder aufgenommen wurde - null ohne jede Angabe.
    IReadOnlyList<GpsWalkRunArticleDto> Articles,
    int? ArticlesConfirmedFound,
    // Die Gegenstände, denen das Gespann per GPS auf 8 m nahe kam (die Regel hinter ArticlesFound) -
    // als Hinweis neben der Angabe der Hundeführer:in, nicht als Ergebnis.
    IReadOnlyList<Guid>? ReachedArticleIds,
    // Davon die Gegenstände, die in einer Lücke der Aufzeichnung liegen (null wie ReachedArticleIds).
    IReadOnlyList<Guid>? UnrecordedArticleIds = null,
    // Nur Schweißfährte (Paket SF2, sonst null): Zahl der Rückrufe und wo sie fielen, berechnet aus den
    // vollständigen Punkten (kein gespeichertes Feld); EndReached: ein Ablaufpunkt lag am Ende der Legung.
    int? Recalls = null,
    IReadOnlyList<GpsRecallPointDto>? RecallPoints = null,
    bool? EndReached = null);

/// <summary>Die Stelle, an der ein Rückruf fiel (siehe GpsTrackEvaluator.Rueckrufe).</summary>
public record GpsRecallPointDto(double Latitude, double Longitude);

public record GpsTrackDto(
    Guid Id,
    Guid TrainingSessionId,
    double? LengthMeters,
    int? AgeMinutes,
    string? Surface,
    string? Weather,
    string? Wind,
    string? Comment,
    IReadOnlyList<GpsPointDto> Points,
    IReadOnlyList<GpsWalkRunDto> WalkRuns,
    // Automatisch ermitteltes Wetter beim LEGEN und beim SUCHEN. Die Differenz
    // (TemperatureDeltaC) ist der fachlich interessante Wert: sie bestimmt
    // maßgeblich, wie sich die Geruchsspur hält.
    double? LaidTemperatureC,
    int? LaidRelativeHumidity,
    double? LaidWindSpeedKmh,
    int? LaidWeatherCode,
    double? SearchTemperatureC,
    int? SearchRelativeHumidity,
    double? SearchWindSpeedKmh,
    int? SearchWeatherCode,
    double? TemperatureDeltaC,
    DateTimeOffset? WeatherFetchedAt,
    // Mantrailing-Angaben (Kind = Mantrailing); bei der Fährte false bzw. null - außer DoubleBlind: Das ist dort
    // "Verdeckt absuchen" und wird erst nachträglich gesetzt.
    GpsTrackKind Kind,
    bool DoubleBlind,
    bool NegativeTrail,
    string? ScentArticle,
    // Bewusst schwere Übungsfährte: zählt nicht in die Entwicklung des Hundes.
    bool ExcludeFromStats,
    // Bodenzustand beim Legen (Mehrfachauswahl als Text, nur Fährte).
    string? GroundCondition = null,
    // Windrichtung in Grad, AUS der der Wind kommt (null bei Fährten, deren Wetter vor Paket F4 geholt wurde).
    int? LaidWindDirectionDeg = null,
    int? SearchWindDirectionDeg = null,
    // Nur Schweißfährte: Verfahren, Wildart, Schweißmenge (ml) und Mindest-Stehzeit (Stunden).
    string? Verfahren = null,
    string? Wildart = null,
    int? SchweissMl = null,
    int? StehzeitStunden = null,
    // Nur Schweißfährte und Trail: Wetter über die Stehzeit/Liegezeit (Regensumme, niedrigste und höchste Temperatur).
    double? StandRegenMm = null,
    double? StandTempMinC = null,
    double? StandTempMaxC = null);

/// <summary>
/// Was ein Ablauf nach dem Ändern von Kommentar oder Trail-Ergebnis zurückgibt: nur die änderbaren Angaben,
/// ohne Punkte, Halte, Schenkel und Winkel (die Oberfläche lädt die Spuren danach selbst neu oder braucht sie nicht).
/// </summary>
public record GpsWalkRunAngabenDto(Guid Id, string? Comment, bool? PersonFound, TrailIndication? Indication);

/// <summary>
/// Was eine Fährte nach dem Ändern einer einzelnen Angabe (Trail-Angaben, Verdeckt absuchen, Entwicklung)
/// zurückgibt: die Angaben, die diese Aufrufe ändern können - ohne Punkte und Abläufe.
/// </summary>
public record GpsTrackAngabenDto(Guid Id, int? AgeMinutes, bool DoubleBlind, bool NegativeTrail, string? ScentArticle, bool ExcludeFromStats);

public record CreateGpsPointRequest(
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    double? Accuracy,
    GpsPointType PointType = GpsPointType.Automatic,
    string? Label = null,
    // Default Article: ältere Clients kennen das Feld nicht und haben bisher
    // ausschließlich Gegenstände markiert.
    GpsMarkerType MarkerType = GpsMarkerType.Article);

/// <summary>
/// Neue Fährte. Zwei Wege, die Trainingseinheit anzugeben:
///
/// - DogId + Date (Fährtenrecorder seit 2026-09-10): Der Server hängt die
///   Fährte an die Einheit dieses Hundes an diesem Tag an oder legt eine an.
///   Mehrere Fährten pro Übungsstunde sind im Fährtensport üblich - vorher
///   bekam jede Aufnahme eine eigene Einheit, und der Tag stand doppelt da.
/// - TrainingSessionId: ältere Clients und Anfragen, die schon in der
///   Offline-Warteschlange liegen (erst Einheit, dann Fährte).
///
/// Id macht die Anfrage wiederholbar: Die Warteschlange darf sie mehrfach
/// senden, ohne dass eine zweite Fährte oder doppelte Dauer entsteht.
/// </summary>
public record CreateGpsTrackRequest(
    Guid? TrainingSessionId,
    double? LengthMeters,
    int? AgeMinutes,
    string? Surface,
    string? Weather,
    string? Wind,
    string? Comment,
    IReadOnlyList<CreateGpsPointRequest> Points,
    Guid? Id = null,
    Guid? DogId = null,
    DateOnly? Date = null,
    int? DurationMinutes = null,
    // Ältere Clients kennen die Felder nicht und legen ausschließlich Fährten an.
    GpsTrackKind Kind = GpsTrackKind.Faehrte,
    bool DoubleBlind = false,
    bool NegativeTrail = false,
    string? ScentArticle = null,
    // Bodenzustand beim Legen (nur Fährte); ältere Clients kennen das Feld nicht.
    string? GroundCondition = null,
    // Nur Schweißfährte (alles freiwillig); ältere Clients kennen die Felder nicht.
    string? Verfahren = null,
    string? Wildart = null,
    int? SchweissMl = null,
    int? StehzeitStunden = null);

public record CreateGpsWalkPointRequest(double Latitude, double Longitude, DateTimeOffset Timestamp, double? Accuracy);

public record CreateGpsWalkRunRequest(
    double? LengthMeters,
    string? Comment,
    IReadOnlyList<CreateGpsWalkPointRequest> Points,
    // Vom Gerät vergebene Kennung (wie bei der Fährte): Kam die Antwort nicht an, legt das erneute Senden
    // aus der Offline-Warteschlange keinen zweiten Ablauf an. Ältere Clients kennen das Feld nicht.
    Guid? Id = null);

public record UpdateGpsWalkRunRequest(string? Comment);

/// <summary>
/// Angaben zum Trail (Mantrailing), die sich nachträglich ändern lassen - etwa das
/// Trailalter, das erst beim Suchen feststeht. Ersetzt alle vier Angaben.
/// </summary>
public record UpdateGpsTrailRequest(int? AgeMinutes, bool DoubleBlind, bool NegativeTrail, string? ScentArticle);

/// <summary>
/// "Verdeckt absuchen" bei der Fährte und der Schweißfährte (DoubleBlind): Beim Absuchen sind Verlauf,
/// Marker und Haptik ausgeblendet. Lässt sich nur setzen, solange noch kein Ablauf existiert.
/// </summary>
public record UpdateGpsVerdecktRequest(bool Verdeckt);

/// <summary>Ergebnis eines Mantrailing-Laufs. Ersetzt beide Angaben (null = nicht angegeben).</summary>
public record UpdateGpsTrailRunRequest(bool? PersonFound, TrailIndication? Indication);

/// <summary>Material eines Gegenstands (aus <see cref="GegenstandsMaterial.Alle"/>); null oder leer entfernt die Angabe.</summary>
public record UpdateGpsMarkerRequest(string? Material);

/// <summary>Ergebnis zu einem Gegenstand im Ablauf; null entfernt die Angabe.</summary>
public record UpdateGpsWalkRunArticleRequest(Guid PointId, ArticleResult? Result);

/// <summary>Ob die Fährte aus der Entwicklung des Hundes herausgenommen ist.</summary>
public record UpdateGpsTrackStatsRequest(bool ExcludeFromStats);

/// <summary>Eine noch wartende Spur mit ihrer Legung (siehe <see cref="IGpsTrackService.GetWartendeAsync"/>), samt Hund.</summary>
public record WartendeSpurDto(
    Guid DogId,
    Guid Id,
    Guid TrainingSessionId,
    double? LengthMeters,
    int? AgeMinutes,
    string? Surface,
    GpsTrackKind Kind,
    bool DoubleBlind,
    bool NegativeTrail,
    string? ScentArticle,
    IReadOnlyList<GpsPointDto> Points,
    // Nur Schweißfährte: die Angaben, die das Ergebnis-Fenster und die Stehzeit-Anzeige brauchen.
    string? Verfahren = null,
    string? Wildart = null,
    int? SchweissMl = null,
    int? StehzeitStunden = null);
