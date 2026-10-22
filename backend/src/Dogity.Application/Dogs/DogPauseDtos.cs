using Dogity.Domain.Dogs;

namespace Dogity.Application.Dogs;

/// <summary>
/// Eine Trainingspause des Hundes.
///
/// <see cref="Reason"/> und <see cref="Note"/> sind für Besitzer:innen und
/// Mitbesitzer:innen gefüllt - und für niemanden sonst: Eine betreuende
/// Trainer:in erfährt nur, DASS und BIS WANN pausiert wird. Das entscheidet der
/// Server beim Zusammenstellen (<see cref="DogPauseQueries.ToDto"/>), nicht
/// erst die Oberfläche; die Felder kommen bei ihr gar nicht an.
/// </summary>
public record DogPauseDto(
    Guid Id,
    DateOnly StartDate,
    /// <summary>Letzter Tag der Pause (einschließlich); null = bis auf Weiteres.</summary>
    DateOnly? EndDate,
    DogPauseReason? Reason,
    string? Note);

/// <summary>Anlegen und Ändern einer Pause haben dieselben Felder.</summary>
public record SaveDogPauseRequest(
    DogPauseReason Reason,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Note);
