/**
 * Höchstlängen der Freitexte - dieselben Zahlen wie die Spalten im Backend
 * (EF-Konfigurationen in Dogity.Infrastructure/Persistence/Configurations).
 *
 * Als maxLength am Eingabefeld: Mehr lässt sich gar nicht erst eintippen.
 * Vorher antwortete der Server auf einen zu langen Text mit einem 500er -
 * und ein offline erfasster Eintrag mit zu langer Notiz hielt die
 * Warteschlange dauerhaft auf, samt allen Fährten dahinter.
 */
export const TEXTLAENGE = {
  /** TrainingSession.Notes */
  trainingsNotiz: 4000,
  /** TrainingExercise.Notes */
  uebungsNotiz: 2000,
  /** TrainingSession.TrainerFeedback */
  trainerRueckmeldung: 2000,
  /** TrainingExercise.FreeTextLabel */
  eigeneUebung: 150,
  /** GpsWalkRun.Comment */
  ablaufKommentar: 2000,
  /** QuizQuestion.Text */
  sachkundeFrage: 2000,
  /** QuizQuestion.SampleSolution */
  musterloesung: 2000,
  /** Goal.ExamNote */
  pruefungsNotiz: 500,
  /** TrainingSession.OwnerReply (Rückfrage zum Trainer-Feedback) */
  feedbackRueckfrage: 500,
} as const;
