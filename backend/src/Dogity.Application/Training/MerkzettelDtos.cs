using Dogity.Domain.Training;

namespace Dogity.Application.Training;

/// <summary>
/// Ein Eintrag auf dem Merkzettel eines Hundes, wie ihn Besitzer:innen und Mitbesitzer:innen sehen -
/// und niemand sonst (siehe <see cref="IMerkzettelService"/>).
/// </summary>
/// <param name="TargetDate">
/// Der Tag, auf den der Eintrag zielt, so wie er jetzt gilt: bei "Datum" der gewählte Tag, bei "Nächste Woche"
/// das erste erkannte Training dieser Woche (sonst der Montag), bei "Nächstes Training" das nächste erkannte
/// (rutscht nach dem Trainingstag von selbst weiter); null, wenn es sich nicht bestimmen lässt.
/// </param>
/// <param name="TargetTime">Uhrzeit des erkannten Trainings an diesem Tag; null, wenn unbekannt.</param>
/// <param name="ExerciseName">Name der Katalog-Übung, falls verknüpft.</param>
public record TrainingNoteDto(
    Guid Id,
    Guid DogId,
    string Text,
    Guid? ExerciseId,
    string? ExerciseName,
    string? ExerciseNameEn,
    TrainingNoteGoal Goal,
    DateOnly? TargetDate,
    TimeOnly? TargetTime,
    DateTimeOffset? DoneAt,
    Guid? DoneInTrainingId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Anlegen und Ändern haben dieselben Felder. <paramref name="TargetDate"/> gilt nur bei
/// <see cref="TrainingNoteGoal.Date"/>. <paramref name="Id"/> vergibt das Gerät beim Anlegen, damit ein
/// Eintrag aus der Offline-Warteschlange bei einer Wiederholung nicht doppelt entsteht.
/// </summary>
public record SaveTrainingNoteRequest(
    string Text,
    Guid? ExerciseId,
    TrainingNoteGoal Goal,
    DateOnly? TargetDate,
    Guid? Id = null);

public record SetTrainingNoteDoneRequest(bool Done);

/// <summary>
/// Das nächste Training eines Hundes für die Beschriftung "Nächstes Training · Di 18:00".
/// <paramref name="Date"/> ist null bei <see cref="TrainingsQuelle.Unbekannt"/>.
/// </summary>
public record NextTrainingDto(DateOnly? Date, TimeOnly? Time, TrainingsQuelle Source);

/// <summary>Wann ein Hund meistens trainiert: Wochentage als Bitmaske (Mo = 1 ... So = 64), Stunde wahlweise (0 bis 23).</summary>
public record SaveTrainingRhythmRequest(int Days, int? Hour);
