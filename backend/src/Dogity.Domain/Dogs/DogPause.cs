using Dogity.Domain.Common;

namespace Dogity.Domain.Dogs;

public enum DogPauseReason
{
    Injured,
    Sick,

    /// <summary>Läufigkeit - gibt es nur bei Hündinnen (<see cref="DogGender.Female"/>).</summary>
    InHeat
}

/// <summary>
/// Eine Trainingspause eines Hundes: verletzt, krank oder läufig.
///
/// Die Pause hält den Trainingsplan an (siehe Dogity.Application.Planning.Trainingspausen):
/// Wochen in der Pause zählen nicht, danach läuft der Plan dort weiter, wo er
/// stand. Training eintragen bleibt trotzdem möglich - leichtes Training oder
/// Physio sind während einer Pause üblich, eine Sperre wäre bevormundend.
///
/// Beide Datumsgrenzen gelten einschließlich: Eine Pause mit
/// <see cref="StartDate"/> = <see cref="EndDate"/> dauert genau einen Tag. Ohne
/// <see cref="EndDate"/> gilt sie "bis auf Weiteres".
///
/// Grund und Notiz sind Gesundheitsangaben zum Tier und gehören der Familie:
/// Betreuende Trainer:innen erfahren nur, DASS und BIS WANN pausiert wird
/// (siehe DogPauseDto).
/// </summary>
public class DogPause : Entity
{
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    public DogPauseReason Reason { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Wer die Pause eingetragen hat (Besitzer:in oder Mitbesitzer:in). Kein
    /// Fremdschlüssel, wie bei den anderen Verweisen auf Konten; bei der
    /// Kontolöschung der Person bleibt die Pause eines geteilten Hundes bei den
    /// übrigen Besitzer:innen, der Verweis wird dann geleert.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }
}
