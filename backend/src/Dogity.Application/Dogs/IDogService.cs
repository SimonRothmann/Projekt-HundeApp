using Dogity.Application.Common;

namespace Dogity.Application.Dogs;

public interface IDogService
{
    Task<Result<IReadOnlyList<DogDto>>> GetMyDogsAsync(Guid userId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SupervisedDogDto>>> GetSupervisedDogsAsync(Guid trainerId, CancellationToken ct = default);
    Task<Result<DogDto>> GetByIdAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result<DogDto>> CreateAsync(Guid userId, CreateDogRequest request, CancellationToken ct = default);
    Task<Result<DogDto>> UpdateAsync(Guid userId, Guid dogId, UpdateDogRequest request, CancellationToken ct = default);
    Task<Result> SetArchivedAsync(Guid userId, Guid dogId, bool archived, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<DogOwnerDto>>> GetOwnersAsync(Guid userId, Guid dogId, CancellationToken ct = default);
    Task<Result> AddOwnerAsync(Guid userId, Guid dogId, AddDogOwnerRequest request, CancellationToken ct = default);
    Task<Result> RemoveOwnerAsync(Guid userId, Guid dogId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>
    /// Profilbild als Data-URI. Lesen darf jeder mit Zugriff auf den Hund
    /// (auch ein zugewiesener Trainer); ohne hinterlegtes Bild ein Fehlschlag.
    /// </summary>
    Task<Result<DogImageDto>> GetImageAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    /// <summary>
    /// Kennzeichen des Bildes für den bedingten Abruf, ohne die Bilddaten zu
    /// lesen. Siehe <see cref="DogService.GetImageETagAsync"/>.
    /// </summary>
    Task<Result<string>> GetImageETagAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    /// <summary>
    /// Setzt oder ersetzt das Profilbild. Nur für Besitzer des Hundes.
    /// Erwartet eine Data-URI; Typ und Größe werden geprüft.
    /// </summary>
    Task<Result> SetImageAsync(Guid userId, Guid dogId, string dataUrl, CancellationToken ct = default);

    /// <summary>Entfernt das Profilbild. Ohne vorhandenes Bild ein Erfolg, kein Fehler.</summary>
    Task<Result> DeleteImageAsync(Guid userId, Guid dogId, CancellationToken ct = default);
}
