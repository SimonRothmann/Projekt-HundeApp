using Dogity.Application.Common;

namespace Dogity.Application.Training;

/// <summary>
/// Der Merkzettel fürs nächste Training. Sehen und ändern dürfen NUR Besitzer:innen und
/// Mitbesitzer:innen des Hundes - gemeinsamer Hund, gemeinsamer Merkzettel. Betreuende Trainer:innen
/// und Fremde bekommen "nicht gefunden", auch beim Lesen: Es sind persönliche Notizen.
/// </summary>
public interface IMerkzettelService
{
    /// <summary>Die offenen Einträge des Hundes, älteste zuerst.</summary>
    Task<Result<IReadOnlyList<TrainingNoteDto>>> GetAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    /// <summary>
    /// Legt einen Eintrag an. Mit <see cref="SaveTrainingNoteRequest.Id"/> idempotent: Gibt es den Eintrag schon,
    /// kommt er unverändert zurück.
    /// </summary>
    Task<Result<TrainingNoteDto>> CreateAsync(Guid userId, Guid dogId, SaveTrainingNoteRequest request, CancellationToken ct = default);

    Task<Result<TrainingNoteDto>> UpdateAsync(Guid userId, Guid dogId, Guid noteId, SaveTrainingNoteRequest request, CancellationToken ct = default);

    /// <summary>Hakt von Hand ab bzw. nimmt das Abhaken zurück.</summary>
    Task<Result<TrainingNoteDto>> SetDoneAsync(Guid userId, Guid dogId, Guid noteId, bool done, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid dogId, Guid noteId, CancellationToken ct = default);

    /// <summary>Das nächste Training für die Beschriftung des Ziels "Nächstes Training".</summary>
    Task<Result<NextTrainingDto>> GetNextTrainingAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    /// <summary>Speichert den angegebenen Rhythmus des Hundes ("Wann trainierst du meistens?") und liefert das daraus folgende nächste Training.</summary>
    Task<Result<NextTrainingDto>> SetRhythmAsync(Guid userId, Guid dogId, SaveTrainingRhythmRequest request, CancellationToken ct = default);
}
