using CanisTrack.Application.Abstractions;
using CanisTrack.Application.Common;
using CanisTrack.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace CanisTrack.Application.Dogs;

/// <summary>
/// Use Cases für die Hundeverwaltung (siehe FEATURE_MODULE.md "Core").
/// Zugriff ist immer auf Hunde beschränkt, die dem aufrufenden Benutzer
/// über <see cref="DogOwner"/> zugeordnet sind.
/// </summary>
public class DogService(IApplicationDbContext db) : IDogService
{
    public async Task<Result<IReadOnlyList<DogDto>>> GetMyDogsAsync(Guid userId, CancellationToken ct = default)
    {
        var dogs = await db.DogOwners
            .Where(o => o.UserId == userId)
            .Select(o => o.Dog!)
            .Select(d => ToDto(d))
            .ToListAsync(ct);

        return Result<IReadOnlyList<DogDto>>.Success(dogs);
    }

    public async Task<Result<DogDto>> GetByIdAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<DogDto>.Failure("Hund nicht gefunden.");

        var dog = await db.Dogs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dogId, ct);
        return dog is null
            ? Result<DogDto>.Failure("Hund nicht gefunden.")
            : Result<DogDto>.Success(ToDto(dog));
    }

    public async Task<Result<DogDto>> CreateAsync(Guid userId, CreateDogRequest request, CancellationToken ct = default)
    {
        var validationError = Validate(request.Name);
        if (validationError is not null)
            return Result<DogDto>.Failure(validationError);

        var dog = new Dog
        {
            Name = request.Name.Trim(),
            Breed = request.Breed,
            Birthday = request.Birthday,
            Gender = request.Gender,
            ImageUrl = request.ImageUrl,
            Notes = request.Notes
        };
        dog.Owners.Add(new DogOwner { DogId = dog.Id, UserId = userId, Role = DogOwnerRole.Owner });

        db.Dogs.Add(dog);
        await db.SaveChangesAsync(ct);

        return Result<DogDto>.Success(ToDto(dog));
    }

    public async Task<Result<DogDto>> UpdateAsync(Guid userId, Guid dogId, UpdateDogRequest request, CancellationToken ct = default)
    {
        var validationError = Validate(request.Name);
        if (validationError is not null)
            return Result<DogDto>.Failure(validationError);

        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null)
            return Result<DogDto>.Failure("Hund nicht gefunden.");

        dog.Name = request.Name.Trim();
        dog.Breed = request.Breed;
        dog.Birthday = request.Birthday;
        dog.Gender = request.Gender;
        dog.ImageUrl = request.ImageUrl;
        dog.Notes = request.Notes;
        dog.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<DogDto>.Success(ToDto(dog));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null)
            return Result.Failure("Hund nicht gefunden.");

        dog.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Dog?> GetOwnedDogAsync(Guid userId, Guid dogId, CancellationToken ct) =>
        await db.Dogs
            .Where(d => d.Id == dogId)
            .Where(d => d.Owners.Any(o => o.UserId == userId))
            .FirstOrDefaultAsync(ct);

    private static string? Validate(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Name ist erforderlich." : null;

    private static DogDto ToDto(Dog d) =>
        new(d.Id, d.Name, d.Breed, d.Birthday, d.Gender, d.ImageUrl, d.Notes);
}
