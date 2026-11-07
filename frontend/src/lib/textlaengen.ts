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
  /** GpsTrack.ScentArticle: Geruchsartikel eines Mantrailing-Trails */
  geruchsartikel: 100,
  /** GpsPoint.Material: Material eines Gegenstands, nur aus der Auswahl in lib/gegenstaende.ts */
  gegenstandsMaterial: 40,
  /** QuizQuestion.Text */
  sachkundeFrage: 2000,
  /** QuizQuestion.SampleSolution */
  musterloesung: 2000,
  /** Goal.ExamNote */
  pruefungsNotiz: 500,
  /** TrainingSession.OwnerReply (Rückfrage zum Trainer-Feedback) */
  feedbackRueckfrage: 500,
  /** DogHealthRecord.Title: Titel eines Gesundheitseintrags */
  gesundheitTitel: 100,
  /** DogHealthRecord.Note: Notiz zu einem Gesundheitseintrag */
  gesundheitNotiz: 500,
  /** GroupRegistration: Vor-/Nachname und Rufname des Hundes */
  anmeldeName: 100,
  /** Dog.Name: Rufname des Hundes */
  hundename: 100,
  /** Dog.Breed (Rasse des Hundes) */
  hundeRasse: 100,
  /** Dog.Breed und GroupRegistration.DogBreed */
  anmeldeRasse: 100,
  /** GroupRegistration.Phone */
  anmeldeTelefon: 30,
  /** GroupRegistration.Notes */
  anmeldeNotiz: 500,
  /** Sport.NameEn (Katalog-Editor, Englischer Name) */
  sportNameEn: 100,
  /** Regulation.NameEn (Katalog-Editor, Englischer Name) */
  ordnungNameEn: 100,
  /** Exercise.NameEn (Katalog-Editor, Englischer Name) */
  uebungNameEn: 150,
  /** Exercise.DescriptionEn (Katalog-Editor, Englische Beschreibung) */
  uebungBeschreibungEn: 2000,
  /** Exercise.ScoringCriteriaEn (Katalog-Editor, Englische Bewertungskriterien) */
  uebungBewertungEn: 1000,
  /** Regulation.DescriptionEn (Katalog-Editor, Englische Rahmenbedingungen) */
  ordnungBeschreibungEn: 2000,
  /** RegulationExercise.ScoringNotesEn (Katalog-Editor, Englische Bewertungshinweise) */
  ordnungsuebungHinweisEn: 1000,
  /** DogPause.Note: Notiz zur Trainingspause eines Hundes */
  pausenNotiz: 500,
  /** Club.Name: Name des Vereins */
  vereinsName: 150,
  /** Club.Description: Beschreibung des Vereins */
  vereinsBeschreibung: 500,
  /** ClubExam.Location: Ort eines Prüfungstags */
  pruefungsOrt: 100,
  /** ClubExam.JudgeName: Name der Richter:in */
  pruefungsRichter: 100,
  /** ClubExam.Note: Bemerkung zum Prüfungstag */
  pruefungsBemerkung: 500,
  /** ClubExamEntry.GuestHandlerName und GuestDogName: Name von Hundeführer:in und Hund eines Gast-Teams */
  gastName: 100,
  /** ClubExamEntry.GuestInfo: Verein und Rasse eines Gast-Teams */
  gastInfo: 200,
  /** ClubWorkEvent.Title: Titel eines Arbeitseinsatzes */
  arbeitseinsatzTitel: 100,
  /** ClubWorkEvent.Note: Notiz zu einem Arbeitseinsatz */
  arbeitseinsatzNotiz: 500,
  /** ClubWorkGuest.FirstName und LastName: Vor- und Nachname einer Helfer:in ohne Konto */
  arbeitseinsatzGastName: 100,
  /** Suchbegriff der Admin-Nutzerliste (Textlaengen.NutzerSuche, wird nicht gespeichert) */
  nutzerSuche: 100,
} as const;
