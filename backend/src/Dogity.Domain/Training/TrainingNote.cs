using Dogity.Domain.Common;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;

namespace Dogity.Domain.Training;

/// <summary>Wann ein Merkzettel-Eintrag drankommen soll.</summary>
public enum TrainingNoteGoal
{
    /// <summary>Beim nächsten Training - welcher Tag das ist, ergibt sich erst beim Lesen (Dogity.Application.Training.Trainingsrhythmus).</summary>
    NextTraining,

    /// <summary>In der nächsten Kalenderwoche; <see cref="TrainingNote.TargetDate"/> ist deren Montag.</summary>
    NextWeek,

    /// <summary>An einem gewählten Tag (<see cref="TrainingNote.TargetDate"/>).</summary>
    Date
}

/// <summary>
/// Ein Eintrag auf dem Merkzettel fürs nächste Training: Die Trainerin sagt "das machen wir
/// nächstes Mal", die Person tippt es in Sekunden auf und wird vor dem Training per Push erinnert
/// (<see cref="Notifications.ReminderType.TrainingNote"/>). Beim Eintragen des Trainings wird er abgehakt.
///
/// Der Merkzettel gehört dem Hund: Alle Besitzer:innen und Mitbesitzer:innen sehen und ändern ihn,
/// betreuende Trainer:innen nicht - es sind persönliche Notizen.
/// </summary>
public class TrainingNote : Entity
{
    public Guid DogId { get; set; }
    public Dog? Dog { get; set; }

    /// <summary>
    /// Wer den Eintrag gemerkt hat. Kein Fremdschlüssel, wie bei den anderen Verweisen auf Konten;
    /// bei der Kontolöschung der Person bleibt der Eintrag eines geteilten Hundes bei den übrigen
    /// Besitzer:innen, der Verweis wird dann geleert.
    /// </summary>
    public Guid? UserId { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Eine Übung aus dem Katalog, um die es geht; optional.</summary>
    public Guid? ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    public TrainingNoteGoal Goal { get; set; }

    /// <summary>Der Tag bei <see cref="TrainingNoteGoal.Date"/>, bei <see cref="TrainingNoteGoal.NextWeek"/> der Montag der Folgewoche; sonst leer.</summary>
    public DateOnly? TargetDate { get; set; }

    /// <summary>Wann der Eintrag abgehakt wurde; null = offen.</summary>
    public DateTimeOffset? DoneAt { get; set; }

    /// <summary>
    /// Das Training, bei dessen Eintragen er abgehakt wurde; leer, wenn er von Hand abgehakt wurde.
    /// Kein Fremdschlüssel: Wird das Training gelöscht, bleibt der Eintrag erledigt.
    /// </summary>
    public Guid? DoneInTrainingId { get; set; }
}
