using CanisTrack.Domain.Tracking;

namespace CanisTrack.Application.Tracking;

public record GpsPointDto(
    double Latitude,
    double Longitude,
    DateTimeOffset Timestamp,
    double? Accuracy,
    GpsPointType PointType,
    string? Label);

public record GpsWalkPointDto(double Latitude, double Longitude, DateTimeOffset Timestamp, double? Accuracy);

public record GpsWalkRunDto(
    Guid Id,
    Guid TrackId,
    DateTimeOffset CreatedAt,
    double? LengthMeters,
    string? Comment,
    IReadOnlyList<GpsWalkPointDto> Points);

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
    string? Label = null);

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
