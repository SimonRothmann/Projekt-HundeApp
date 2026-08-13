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
    IReadOnlyList<GpsWalkStopDto> Stops);

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
    IReadOnlyList<GpsWalkRunDto> WalkRuns);

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

public record CreateGpsTrackRequest(
    Guid TrainingSessionId,
    double? LengthMeters,
    int? AgeMinutes,
    string? Surface,
    string? Weather,
    string? Wind,
    string? Comment,
    IReadOnlyList<CreateGpsPointRequest> Points);

public record CreateGpsWalkPointRequest(double Latitude, double Longitude, DateTimeOffset Timestamp, double? Accuracy);

public record CreateGpsWalkRunRequest(
    double? LengthMeters,
    string? Comment,
    IReadOnlyList<CreateGpsWalkPointRequest> Points);

public record UpdateGpsWalkRunRequest(string? Comment);
