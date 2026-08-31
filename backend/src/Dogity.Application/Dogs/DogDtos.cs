using Dogity.Domain.Dogs;

namespace Dogity.Application.Dogs;

public record DogDto(
    Guid Id,
    string Name,
    string? Breed,
    DateOnly? Birthday,
    DogGender Gender,
    string? ImageUrl,
    string? Notes,
    DateTimeOffset? ArchivedAt,
    /// <summary>
    /// Ob ein Profilbild hinterlegt ist. Das Bild selbst kommt über einen
    /// eigenen Aufruf (siehe DogsController.GetImage) - in einer Hundeliste
    /// hinge sonst an jedem Eintrag das vollständige Bild.
    /// </summary>
    bool HasImage);

/// <summary>
/// Ein von mir betreuter Hund - für die Trainerübersicht. Enthält bewusst den
/// Namen des Hundeführers und ob ein Trainingsplan läuft: das sind die beiden
/// Angaben, an denen eine Trainer:in ihre Hunde auseinanderhält, ohne die
/// Karte erst öffnen zu müssen.
/// </summary>
public record SupervisedDogDto(
    Guid Id,
    string Name,
    string? Breed,
    bool HasImage,
    string HandlerName,
    /// <summary>Anzahl aktiver Trainingsziele - 0 heißt: hier ist noch kein Plan.</summary>
    int ActiveGoalCount);

/// <summary>
/// Ein Bild als Data-URI ("data:image/jpeg;base64,..."), in beide Richtungen.
///
/// Bewusst kein Datei-Upload: Der Browser rechnet das Bild ohnehin auf einer
/// Leinwand auf Profilbildgröße herunter und hat es danach als Data-URI in der
/// Hand. So bleibt es bei gewöhnlichem JSON - kein multipart, und die Antwort
/// lässt sich unverändert in ein img-Element hängen.
/// </summary>
public record DogImageDto(string DataUrl);

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

public record ArchiveDogRequest(bool Archived);
