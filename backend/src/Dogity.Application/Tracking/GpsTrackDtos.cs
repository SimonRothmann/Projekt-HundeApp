using Dogity.Domain.Tracking;

namespace Dogity.Application.Tracking;

public record GpsPointDto(
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    double? Accuracy,
    GpsPointType PointType,
    string? Label,
    GpsMarkerType MarkerType);

/// <summary>
/// DeviationMeters: senkrechter Abstand zur gelegten Fährte (null = noch nicht
/// ausgewertet). Die Karte färbt die Ablauf-Linie abschnittsweise danach ein.
/// </summary>
public record GpsWalkPointDto(double Latitude, double Longitude, DateTimeOffset Timestamp, double? Accuracy, double? DeviationMeters);

/// <summary>Ein erkannter Halt während des Ablaufs (siehe GpsWalkStop).</summary>
public record GpsWalkStopDto(
    double Latitude,
    double Longitude,
    int DurationSeconds,
    WalkStopKind Kind,
    string? MarkerLabel);

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
    TrailIndication? Indication);

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
    // Nur Mantrailing (Kind = Mantrailing); bei der Fährte false bzw. null.
    GpsTrackKind Kind,
    bool DoubleBlind,
    bool NegativeTrail,
    string? ScentArticle);

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
    string? ScentArticle = null);

public record CreateGpsWalkPointRequest(double Latitude, double Longitude, DateTimeOffset Timestamp, double? Accuracy);

public record CreateGpsWalkRunRequest(
    double? LengthMeters,
    string? Comment,
    IReadOnlyList<CreateGpsWalkPointRequest> Points);

public record UpdateGpsWalkRunRequest(string? Comment);

/// <summary>
/// Angaben zum Trail (Mantrailing), die sich nachträglich ändern lassen - etwa das
/// Trailalter, das erst beim Suchen feststeht. Ersetzt alle vier Angaben.
/// </summary>
public record UpdateGpsTrailRequest(int? AgeMinutes, bool DoubleBlind, bool NegativeTrail, string? ScentArticle);

/// <summary>Ergebnis eines Mantrailing-Laufs. Ersetzt beide Angaben (null = nicht angegeben).</summary>
public record UpdateGpsTrailRunRequest(bool? PersonFound, TrailIndication? Indication);
