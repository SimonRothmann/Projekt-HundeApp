namespace CanisTrack.Application.Community;

public record ClubDto(Guid Id, string Name, string? Description, int TrainerCount, int GroupCount);

public record ClubTrainerDto(Guid UserId, string Email, string FirstName, string LastName, DateTimeOffset AssignedAt);

public record ClubDetailDto(ClubDto Club, IReadOnlyList<ClubTrainerDto> Trainers);

public record CreateClubRequest(string Name, string? Description);

public record AssignClubTrainerRequest(string Email);
