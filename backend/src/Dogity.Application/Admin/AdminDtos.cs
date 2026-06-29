namespace Dogity.Application.Admin;

public record AdminStatsDto(int UserCount, int DogCount, int GroupCount, int TrainingSessionCount, int GpsTrackCount);

public record AdminUserDto(Guid Id, string Email, string FirstName, string LastName, string[] Roles);

public record UpdateRegulationSourceRequest(string? SourceUrl, string? LatestKnownVersionLabel);
