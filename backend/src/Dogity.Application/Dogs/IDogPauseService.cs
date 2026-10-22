using Dogity.Application.Common;

namespace Dogity.Application.Dogs;

/// <summary>
/// Trainingspausen eines Hundes (verletzt, krank, läufig). Schreiben dürfen
/// nur Besitzer:innen und Mitbesitzer:innen. Lesen dürfen sie mit Grund und
/// Notiz, betreuende Trainer:innen nur mit Zeitraum (siehe DogPauseDto); alle
/// anderen bekommen wie bei den übrigen Hunde-Endpunkten "nicht gefunden".
/// </summary>
public interface IDogPauseService
{
    /// <summary>Alle Pausen des Hundes, neueste zuerst - der Verlauf.</summary>
    Task<Result<IReadOnlyList<DogPauseDto>>> GetAsync(Guid userId, Guid dogId, CancellationToken ct = default);

    /// <summary>
    /// Trägt eine Pause ein. Eine Pause, die sich mit einer bestehenden
    /// überschneidet, wird abgelehnt - eine neue beendet keine alte
    /// stillschweigend.
    /// </summary>
    Task<Result<DogPauseDto>> CreateAsync(Guid userId, Guid dogId, SaveDogPauseRequest request, CancellationToken ct = default);

    Task<Result<DogPauseDto>> UpdateAsync(Guid userId, Guid dogId, Guid pauseId, SaveDogPauseRequest request, CancellationToken ct = default);

    /// <summary>
    /// Beendet eine laufende Pause vorzeitig: Der Hund trainiert ab heute
    /// wieder, letzter Pausentag ist gestern. Eine Pause, die heute erst
    /// beginnen würde oder noch bevorsteht, wird stattdessen zurückgenommen.
    /// </summary>
    Task<Result> EndAsync(Guid userId, Guid dogId, Guid pauseId, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid userId, Guid dogId, Guid pauseId, CancellationToken ct = default);
}
