using Dogity.Domain.Dogs;

namespace Dogity.Application.Dogs;

public record DogDto(
    Guid Id,
    string Name,
    string? Breed,
    DateOnly? Birthday,
    DogGender Gender,
    string? ImageUrl,
    string? Notes);

public record DogOwnerDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DogOwnerRole Role,
    DateTimeOffset AddedAt);

public record CreateDogRequest(
    string Name,
    string? Breed,
    DateOnly? Birthday,
    DogGender Gender,
    string? ImageUrl,
    string? Notes);

public record UpdateDogRequest(
    string Name,
    string? Breed,
    DateOnly? Birthday,
    DogGender Gender,
    string? ImageUrl,
    string? Notes);

public record AddDogOwnerRequest(string Email);
