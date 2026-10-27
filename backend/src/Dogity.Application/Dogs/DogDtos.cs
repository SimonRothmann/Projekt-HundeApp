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
    bool HasImage,
    /// <summary>
    /// Die Trainingspause, die heute gilt; null ohne. Für Besitzer:innen mit
    /// Grund und Notiz, für betreuende Trainer:innen nur mit Zeitraum.
    /// </summary>
    DogPauseDto? Pause = null,
    /// <summary>
    /// Fälligkeitsstand der Gesundheit (Impfungen, Entwurmung, Tierarzttermin). Nur für Besitzer:innen
    /// und Mitbesitzer:innen gefüllt, für betreuende Trainer:innen immer null; null auch, wenn nichts
    /// Datiertes eingetragen ist.
    /// </summary>
    DogHealthSummaryDto? Gesundheit = null);

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
    int ActiveGoalCount,
    /// <summary>Die Pause, die heute gilt - nur Zeitraum, nie Grund oder Notiz.</summary>
    DogPauseDto? Pause = null);

/// <summary>
/// Ein Bild als Data-URI ("data:image/jpeg;base64,..."), in beide Richtungen.
///
/// Bewusst kein Datei-Upload: Der Browser rechnet das Bild ohnehin auf einer
/// Leinwand auf Profilbildgröße herunter und hat es danach als Data-URI in der
/// Hand. So bleibt es bei gewöhnlichem JSON - kein multipart, und die Antwort
/// lässt sich unverändert in ein img-Element hängen.
/// </summary>
public record DogImageDto(string DataUrl);

/// <summary>
/// Eine Person am Hund. Bei offenen Einladungen (<see cref="IsInvited"/>)
/// bleiben die Namen leer: Wer einlädt, kennt die E-Mail-Adresse ohnehin -
/// den Namen dahinter erfährt er erst, wenn die Person annimmt.
/// </summary>
public record DogOwnerDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    DogOwnerRole Role,
    DateTimeOffset AddedAt,
    bool IsInvited = false);

/// <summary>
/// Eine Trainer:in, die den Hund betreut (TrainerAssignment) - die Sicht der
/// Besitzer:innen. Bewusst ohne E-Mail-Adresse: Es geht darum, WER Einblick
/// hat und seit wann, nicht darum, die Person zu erreichen.
/// </summary>
/// <param name="GroupName">Die Gruppe, über die die Betreuung läuft; null, wenn sich keine ermitteln lässt (etwa nach einem Gruppenwechsel).</param>
public record DogBetreuungDto(
    Guid TrainerId,
    string TrainerName,
    DateOnly StartDate,
    string? GroupName);

/// <summary>Eine offene Einladung, Mitbesitzer:in eines Hundes zu werden - die Sicht der eingeladenen Person.</summary>
public record DogInvitationDto(
    Guid DogId,
    string DogName,
    string? InvitedByName,
    DateTimeOffset InvitedAt);

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
