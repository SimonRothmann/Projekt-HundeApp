using Dogity.Application.Abstractions;
using Dogity.Application.Common;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Dogs;

/// <summary>
/// Use Cases für die Hundeverwaltung (siehe FEATURE_MODULE.md "Core").
/// Zugriff ist immer auf Hunde beschränkt, die dem aufrufenden Benutzer
/// über <see cref="DogOwner"/> zugeordnet sind.
/// </summary>
public class DogService(IApplicationDbContext db, IUserLookupService userLookup) : IDogService
{
    public async Task<Result<IReadOnlyList<DogDto>>> GetMyDogsAsync(Guid userId, CancellationToken ct = default)
    {
        var dogs = await db.DogOwners
            .Where(o => o.UserId == userId)
            .Select(o => o.Dog!)
            .ToListAsync(ct);

        // Nur die Ids der Hunde MIT Bild holen - die Bilddaten selbst bleiben
        // in der Datenbank, die Liste zeigt ohnehin nur, ob eines existiert.
        var dogIds = dogs.Select(d => d.Id).ToList();
        var withImage = await db.DogImages
            .Where(i => dogIds.Contains(i.DogId))
            .Select(i => i.DogId)
            .ToListAsync(ct);

        return Result<IReadOnlyList<DogDto>>.Success(
            dogs.Select(d => ToDto(d, withImage.Contains(d.Id))).ToList());
    }

    public async Task<Result<DogDto>> GetByIdAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<DogDto>.Failure("Hund nicht gefunden.");

        var dog = await db.Dogs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dogId, ct);
        if (dog is null) return Result<DogDto>.Failure("Hund nicht gefunden.");

        return Result<DogDto>.Success(ToDto(dog, await HasImageAsync(dogId, ct)));
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

        return Result<DogDto>.Success(ToDto(dog, await HasImageAsync(dog.Id, ct)));
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
        return Result<DogDto>.Success(ToDto(dog, await HasImageAsync(dog.Id, ct)));
    }

    public async Task<Result> SetArchivedAsync(Guid userId, Guid dogId, bool archived, CancellationToken ct = default)
    {
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null)
            return Result.Failure("Hund nicht gefunden.");

        // Archivieren blendet den Hund nur aus (kein Soft-Delete) - die Historie
        // bleibt vollständig erhalten und die Aktion ist jederzeit reversibel.
        dog.ArchivedAt = archived ? DateTimeOffset.UtcNow : null;
        dog.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
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

    public async Task<Result<IReadOnlyList<DogOwnerDto>>> GetOwnersAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<IReadOnlyList<DogOwnerDto>>.Failure("Hund nicht gefunden.");

        var ownerRows = await db.DogOwners
            .Where(o => o.DogId == dogId)
            .Select(o => new { o.UserId, o.Role, o.CreatedAt })
            .AsNoTracking()
            .ToListAsync(ct);

        var lookup = await userLookup.FindByIdsAsync(ownerRows.Select(o => o.UserId).ToList(), ct);
        var dtos = ownerRows
            .Select(o => lookup.TryGetValue(o.UserId, out var info)
                ? new DogOwnerDto(o.UserId, info.Email, info.FirstName, info.LastName, o.Role, o.CreatedAt)
                : new DogOwnerDto(o.UserId, "(unbekannt)", "", "", o.Role, o.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<DogOwnerDto>>.Success(dtos);
    }

    public async Task<Result> AddOwnerAsync(Guid userId, Guid dogId, AddDogOwnerRequest request, CancellationToken ct = default)
    {
        if (!await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId && o.Role == DogOwnerRole.Owner, ct))
            return Result.Failure("Hund nicht gefunden oder keine Berechtigung.");

        var target = await userLookup.FindByEmailAsync(request.Email, ct);
        if (target is null)
            return Result.Failure("Kein Benutzer mit dieser E-Mail-Adresse gefunden.");

        if (target.UserId == userId)
            return Result.Failure("Du bist bereits Besitzer dieses Hundes.");

        var alreadyOwner = await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == target.UserId, ct);
        if (alreadyOwner)
            return Result.Failure("Dieser Benutzer ist bereits Mitbesitzer dieses Hundes.");

        db.DogOwners.Add(new DogOwner { DogId = dogId, UserId = target.UserId, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RemoveOwnerAsync(Guid userId, Guid dogId, Guid targetUserId, CancellationToken ct = default)
    {
        if (!await db.DogOwners.AnyAsync(o => o.DogId == dogId && o.UserId == userId && o.Role == DogOwnerRole.Owner, ct))
            return Result.Failure("Hund nicht gefunden oder keine Berechtigung.");

        var totalOwners = await db.DogOwners.CountAsync(o => o.DogId == dogId && o.Role == DogOwnerRole.Owner, ct);
        if (totalOwners <= 1)
            return Result.Failure("Der letzte Besitzer kann nicht entfernt werden.");

        var ownerRow = await db.DogOwners.FirstOrDefaultAsync(o => o.DogId == dogId && o.UserId == targetUserId, ct);
        if (ownerRow is null)
            return Result.Failure("Besitzer nicht gefunden.");

        ownerRow.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<DogImageDto>> GetImageAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        if (!await db.HasDogAccessAsync(userId, dogId, ct))
            return Result<DogImageDto>.Failure("Hund nicht gefunden.");

        var image = await db.DogImages.AsNoTracking().FirstOrDefaultAsync(i => i.DogId == dogId, ct);
        return image is null
            ? Result<DogImageDto>.Failure("Kein Bild hinterlegt.")
            : Result<DogImageDto>.Success(new DogImageDto(
                $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Data)}"));
    }

    public async Task<Result> SetImageAsync(Guid userId, Guid dogId, string dataUrl, CancellationToken ct = default)
    {
        // Bild ändern darf nur, wer den Hund besitzt - ein zugewiesener Trainer
        // sieht ihn zwar, verwaltet ihn aber nicht (wie bei UpdateAsync).
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null) return Result.Failure("Hund nicht gefunden.");

        if (!TryParseDataUrl(dataUrl, out var contentType, out var bytes, out var error))
            return Result.Failure(error!);

        var existing = await db.DogImages.FirstOrDefaultAsync(i => i.DogId == dogId, ct);
        if (existing is null)
        {
            db.DogImages.Add(new DogImage { DogId = dogId, Data = bytes!, ContentType = contentType! });
        }
        else
        {
            existing.Data = bytes!;
            existing.ContentType = contentType!;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteImageAsync(Guid userId, Guid dogId, CancellationToken ct = default)
    {
        var dog = await GetOwnedDogAsync(userId, dogId, ct);
        if (dog is null) return Result.Failure("Hund nicht gefunden.");

        var image = await db.DogImages.FirstOrDefaultAsync(i => i.DogId == dogId, ct);
        // Kein Bild da? Dann ist das Ziel bereits erreicht - kein Fehler.
        if (image is null) return Result.Success();

        // Echtes Löschen statt Soft-Delete: ein entferntes Bild soll auch
        // wirklich verschwinden, und es hängt keine Historie daran.
        db.DogImages.Remove(image);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Obergrenze der entpackten Bilddaten. Der Client rechnet vorher herunter - das hier ist die Notbremse.</summary>
    private const int MaxImageBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>
    /// Zerlegt "data:image/jpeg;base64,...." in MIME-Typ und Daten.
    ///
    /// Der MIME-Typ wird gegen eine feste Liste geprüft und nicht einfach
    /// übernommen: er landet später unverändert im Content-Type der Antwort,
    /// und ein frei wählbarer Typ (etwa text/html) machte aus dem Bildabruf
    /// eine Seite, die der Browser ausführt.
    /// </summary>
    private static bool TryParseDataUrl(string? dataUrl, out string? contentType, out byte[]? bytes, out string? error)
    {
        contentType = null;
        bytes = null;

        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            error = "Kein Bild übermittelt.";
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            dataUrl, @"^data:(?<type>[a-z]+/[a-z0-9.+-]+);base64,(?<data>.+)$",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            error = "Bildformat nicht erkannt.";
            return false;
        }

        var type = match.Groups["type"].Value.ToLowerInvariant();
        if (!AllowedImageTypes.Contains(type))
        {
            error = "Nur JPEG, PNG und WebP sind möglich.";
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException)
        {
            error = "Bilddaten sind beschädigt.";
            return false;
        }

        if (bytes.Length == 0)
        {
            error = "Bilddaten sind leer.";
            return false;
        }

        if (bytes.Length > MaxImageBytes)
        {
            error = $"Bild ist zu groß (max. {MaxImageBytes / 1024 / 1024} MB).";
            return false;
        }

        contentType = type;
        error = null;
        return true;
    }

    private async Task<Dog?> GetOwnedDogAsync(Guid userId, Guid dogId, CancellationToken ct) =>
        await db.Dogs
            .Where(d => d.Id == dogId)
            .Where(d => d.Owners.Any(o => o.UserId == userId))
            .FirstOrDefaultAsync(ct);

    private static string? Validate(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Name ist erforderlich." : null;

    private Task<bool> HasImageAsync(Guid dogId, CancellationToken ct) =>
        db.DogImages.AnyAsync(i => i.DogId == dogId, ct);

    private static DogDto ToDto(Dog d, bool hasImage) =>
        new(d.Id, d.Name, d.Breed, d.Birthday, d.Gender, d.ImageUrl, d.Notes, d.ArchivedAt, hasImage);
}
