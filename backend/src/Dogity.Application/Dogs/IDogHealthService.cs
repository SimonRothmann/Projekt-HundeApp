using Dogity.Application.Common;

namespace Dogity.Application.Dogs;

/// <summary>
/// Gesundheitseinträge eines Hundes (Impfungen, Entwurmung, Parasitenschutz, Tierarzttermine).
/// Sehen und ändern dürfen NUR Besitzer:innen und Mitbesitzer:innen. Betreuende Trainer:innen
/// und Fremde bekommen wie bei den übrigen Hunde-Unterressourcen "nicht gefunden" - auch
/// beim Lesen, denn Gesundheit ist privat; wer einen Impfnachweis zeigen muss, zeigt den Impfpass.
/// </summary>
public interface IDogHealthService
{
    /// <summary>Alle Einträge des Hundes, jüngste zuerst - mit Kennzeichnung, was aktuell ist.</summary>
    Task<Result<IReadOnlyList<DogHealthRecordDto>>> GetAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    /// <summary>
    /// Legt einen Eintrag an. Eine erneuerte Impfung ist einfach ein neuer Eintrag - der ältere
    /// bleibt im Verlauf.
    /// </summary>
    Task<Result<DogHealthRecordDto>> CreateAsync(Guid userId, Guid dogId, SaveDogHealthRecordRequest request, CancellationToken ct = default);

    Task<Result<DogHealthRecordDto>> UpdateAsync(Guid userId, Guid dogId, Guid recordId, SaveDogHealthRecordRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid dogId, Guid recordId, CancellationToken ct = default);
}
