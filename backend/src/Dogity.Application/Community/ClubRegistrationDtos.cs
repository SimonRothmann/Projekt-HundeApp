using Dogity.Domain.Community;

namespace Dogity.Application.Community;

public record CreateClubRegistrationRequest(string? Name, string? Description);

/// <param name="Status">
/// ACHTUNG: numerisch übertragen (0 = Pending, 1 = Approved, 2 = Rejected) -
/// die API hat keinen JsonStringEnumConverter, siehe AssignClubTrainerRequest.
/// </param>
public record ClubRegistrationDto(
    Guid Id,
    string Name,
    string? Description,
    Guid RequestedByUserId,
    string RequestedByEmail,
    string RequestedByName,
    ClubRegistrationStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionNote,
    Guid? ClubId);

public record DecideClubRegistrationRequest(string? Note);
