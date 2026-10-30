export type AuthResponse = {
  token: string;
  // Langlebiger Refresh-Token: der Client holt damit lautlos einen neuen
  // Access-Token (token), wenn dieser abläuft - so bleibt man eingeloggt,
  // ohne sich neu anmelden zu müssen (siehe api.ts, Roadmap 6).
  refreshToken: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
};

export type DogGender = 0 | 1; // 0 = Male, 1 = Female (siehe Domain.Dogs.DogGender)

// Grund einer Trainingspause. Zahlen, keine Namen - die API überträgt Enums
// numerisch (siehe Domain.Dogs.DogPauseReason). Die Beschriftungen stehen in
// lib/pausen.ts.
export const PAUSE_GRUND = {
  Verletzt: 0,
  Krank: 1,
  Laeufig: 2,
} as const;

export type PauseGrund = (typeof PAUSE_GRUND)[keyof typeof PAUSE_GRUND];

// Eine Trainingspause des Hundes (Backend: DogPauseDto). Grund und Notiz
// bekommen nur Besitzer:innen und Mitbesitzer:innen; für betreuende
// Trainer:innen sind beide null - der Server liefert sie gar nicht erst aus.
export type DogPause = {
  id: string;
  /** Erster Pausentag (YYYY-MM-DD). */
  startDate: string;
  /** Letzter Pausentag einschließlich; null = bis auf Weiteres. */
  endDate: string | null;
  reason: PauseGrund | null;
  note: string | null;
};

// Art eines Gesundheitseintrags - NUMERISCH (siehe Domain.Dogs.DogHealthKind). Die
// Beschriftungen stehen in lib/gesundheit.ts.
export const GESUNDHEIT_ART = {
  Impfung: 0,
  Entwurmung: 1,
  Parasitenschutz: 2,
  Tierarzttermin: 3,
  Sonstiges: 4,
} as const;

export type GesundheitsArt = (typeof GESUNDHEIT_ART)[keyof typeof GESUNDHEIT_ART];

// Wie es um eine Frist steht - NUMERISCH (siehe Application.Dogs.HealthState).
export const GESUNDHEIT_ZUSTAND = {
  Ok: 0,
  Bald: 1,
  Ueberfaellig: 2,
} as const;

export type GesundheitsZustand = (typeof GESUNDHEIT_ZUSTAND)[keyof typeof GESUNDHEIT_ZUSTAND];

// Ein Gesundheitseintrag des Hundes (Backend: DogHealthRecordDto). Nur Besitzer:innen
// und Mitbesitzer:innen bekommen ihn - der Server liefert ihn niemandem sonst.
export type DogHealthRecord = {
  id: string;
  kind: GesundheitsArt;
  title: string;
  /** Gegeben am bzw. Tag des Tierarzttermins (YYYY-MM-DD). */
  date: string;
  /** Uhrzeit des Termins ("10:30:00"); nur beim Tierarzttermin. */
  time: string | null;
  /** Gültig bis bzw. nächste fällig (YYYY-MM-DD, einschließlich); beim Tierarzttermin null. */
  dueDate: string | null;
  note: string | null;
  /** Gehört zu "Aktuell" (der jüngste je Art und Titel, bei Terminen: heute oder später); sonst Verlauf. */
  isCurrent: boolean;
};

// Der Fälligkeitsstand am Hund (Backend: DogHealthSummaryDto) - nur für Besitzer:innen,
// null bei Trainer:innen und wenn nichts Datiertes eingetragen ist.
export type DogGesundheitStand = {
  overdue: number;
  dueSoon: number;
  nextDue: { id: string; kind: GesundheitsArt; title: string; dueDate: string; state: GesundheitsZustand } | null;
  nextAppointment: { id: string; title: string; date: string; time: string | null } | null;
};

// Ein Pausenzeitraum, wie ihn die Planrechnung braucht (Backend: PausenZeitraumDto).
export type PauseZeitraum = {
  start: string;
  end: string | null;
};

export type Dog = {
  id: string;
  name: string;
  breed: string | null;
  birthday: string | null;
  gender: DogGender;
  imageUrl: string | null;
  notes: string | null;
  // Gesetzt, wenn der Hund archiviert ist (z.B. verstorben) - dann aus der
  // aktiven Liste ausgeblendet, Daten bleiben erhalten. null = aktiv.
  archivedAt: string | null;
  // Ob ein Profilbild hinterlegt ist. Das Bild selbst kommt über einen eigenen
  // Aufruf (/api/dogs/{id}/image), damit an Listen nicht das Bildmaterial
  // aller Hunde hängt - siehe DogAvatar.
  hasImage: boolean;
  // Die Trainingspause, die heute gilt; null ohne. Optional, weil der
  // Lesecache noch Hunde ohne dieses Feld liefern kann.
  pause?: DogPause | null;
  // Fälligkeitsstand der Gesundheit; nur für Besitzer:innen, sonst fehlt er bzw. ist null.
  gesundheit?: DogGesundheitStand | null;
};

export type Sport = {
  id: string;
  code: string;
  name: string;
  // Englische Bezeichnung (nur FCI-Sportarten), siehe lib/katalog-name.ts.
  nameEn?: string | null;
  description: string | null;
  clubId: string | null;
};

export type ExerciseDifficulty = 0 | 1 | 2; // 0 = Beginner, 1 = Intermediate, 2 = Advanced (siehe Domain.Sports.ExerciseDifficulty)

export type Exercise = {
  id: string;
  sportId: string | null;
  name: string;
  nameEn?: string | null;
  description: string | null;
  // Englische Fassungen (nur FCI-Übungen), siehe lib/katalog-name.ts.
  descriptionEn?: string | null;
  difficulty: ExerciseDifficulty;
  category: string | null;
  scoringCriteria: string | null;
  scoringCriteriaEn?: string | null;
  clubId: string | null;
};

export type ParsedExerciseCandidate = {
  name: string;
  maxPoints: number;
};

export type Regulation = {
  id: string;
  name: string;
  nameEn?: string | null;
  // ISO-Kürzel des Geltungsbereichs; null = gilt überall (siehe Domain.Sports.Regulation).
  countryCode?: string | null;
  sourceUrl: string | null;
  lastSyncedAt: string | null;
  latestKnownVersionLabel: string | null;
  // Mehrzeilige Kurzbeschreibung der Prüfungs-Rahmenbedingungen (Schrittzahl,
  // Winkel, Fährtenalter, Voraussetzungen, Bestehensgrenze, ...).
  description: string | null;
  // Englische Fassung der Rahmenbedingungen (nur FCI-Ordnungen).
  descriptionEn?: string | null;
  // Prüfungsordnungen, die auf diese folgen (Ausbildungsweg im Backend).
  nextStageNames?: string[];
  // Zeitpunkt der letzten Handänderung im Katalog-Editor; null = Katalogfassung.
  editedAt?: string | null;
  // Stabile Katalogkennung (Ausbildungsweg); null bei Vereinsordnungen.
  seedKey?: string | null;
};

export type RegulationVersionInfo = {
  id: string;
  versionLabel: string;
  validFrom: string;
};

export type RegulationExerciseInfo = {
  exerciseId: string;
  exerciseName: string;
  exerciseNameEn?: string | null;
  isMandatory: boolean;
  maxPoints: number;
  scoringNotes: string | null;
  // Englische Fassung des Bewertungshinweises (nur FCI-Ordnungen).
  scoringNotesEn?: string | null;
  // Zeitpunkt der letzten Handänderung im Katalog-Editor; null = Katalogfassung.
  editedAt?: string | null;
};

export type RegulationDetail = {
  regulation: Regulation;
  currentVersion: RegulationVersionInfo;
  exercises: RegulationExerciseInfo[];
};

export type TrainingExercise = {
  id: string;
  // null bei einem Freitext-Eintrag (siehe exerciseName, das dann direkt
  // den eingegebenen Freitext enthält statt eines Katalog-Übungsnamens).
  exerciseId: string | null;
  exerciseName: string;
  // Englischer Katalogname; null bei Freitext und Übungen ohne englischen Namen.
  exerciseNameEn?: string | null;
  rating: number;
  difficulty: ExerciseDifficulty;
  success: boolean;
  notes: string | null;
  trainingPlanItemId: string | null;
  // Bewertung eines zugewiesenen Trainers (1-5), getrennt von der
  // Selbstbewertung (rating). null, solange kein Trainer bewertet hat.
  trainerRating: number | null;
  trainerNote: string | null;
};

export type TrainingSession = {
  id: string;
  dogId: string;
  date: string;
  durationMinutes: number;
  notes: string | null;
  exercises: TrainingExercise[];
  trainerFeedback: string | null;
  feedbackAt: string | null;
  // Uhrzeit + Ort: Grundlage der automatischen Wetter-Ermittlung. Beide
  // optional, weil Trainings auch nachgetragen werden.
  startTime: string | null; // "HH:mm:ss"
  latitude: number | null;
  longitude: number | null;
  locationName: string | null;
  temperatureC: number | null;
  relativeHumidity: number | null;
  windSpeedKmh: number | null;
  weatherCode: number | null;
  // Verfassung des Hundes an diesem Trainingstag; null, wenn nicht angegeben.
  condition: DogCondition | null;
  // Ob mindestens eine Fährte existiert - erspart den GPS-Request pro
  // Trainings-Karte (GpsTrackSection wird bei abgeschlossenen Trainings
  // ohne Fährte gar nicht erst gemountet, siehe SessionHistory).
  hasGpsTrack: boolean;
  // Länge und Untergrund der ersten Fährte des Tages - für die Tageszeile im
  // Tagebuch. Nur in der Liste gefüllt, fehlt bei älteren Lesecache-Ständen.
  trackLengthMeters?: number | null;
  trackSurface?: string | null;
  // Antwort des Besitzers auf das Trainer-Feedback (siehe FEEDBACK_REACTION).
  // Fehlt bei Zwischenständen aus dem Lesecache, die vor dieser Fassung
  // gespeichert wurden - deshalb optional.
  ownerReaction?: FeedbackReaction | null;
  ownerReply?: string | null;
  ownerReplyAt?: string | null;
  // Wann die Trainer:in das Training als erledigt abgehakt hat ("Fertig" /
  // "Passt so"). Optional aus demselben Grund wie ownerReaction.
  trainerReviewedAt?: string | null;
  // Ob die angemeldete Person die Einheit ändern oder löschen darf: Besitzer:in
  // immer, betreuende Trainer:innen nur bei selbst eingetragenen Einheiten.
  // Fehlt bei älteren Lesecache-Ständen - gilt dann als ja (der Server prüft
  // ohnehin).
  darfBearbeiten?: boolean;
};

// Schnelle Reaktion auf Trainer-Feedback. Numerisch wie alle Enums der API.
export const FEEDBACK_REACTION = {
  Thanks: 0,
  Understood: 1,
} as const;

export type FeedbackReaction = (typeof FEEDBACK_REACTION)[keyof typeof FEEDBACK_REACTION];

export type GoalStatus = 0 | 1 | 2; // 0 = Active, 1 = Achieved, 2 = Cancelled

export type TrainingPlanItemLog = {
  trainingSessionId: string;
  // Id der durchgeführten Übung - nötig, um die Notiz auch aus dem Plan-Log
  // heraus bearbeiten zu können (siehe ExerciseNotes / Wunsch 2).
  trainingExerciseId: string;
  date: string;
  rating: number;
  success: boolean;
  notes: string | null;
  // Ob die angemeldete Person die Notiz ändern darf (siehe
  // TrainingSession.darfBearbeiten). Fehlt im älteren Lesecache - gilt als ja.
  darfBearbeiten?: boolean;
};

// Grund, warum der adaptive Generator eine Übung geplant hat (siehe
// Domain.Planning.PlanItemReason). null bei manuellen Einträgen/Pausenwochen.
export type PlanItemReason = 0 | 1 | 2; // 0 = Schwäche, 1 = Wiederholung, 2 = Neu

// Eine gewichtbare Übung eines Ziels ("mehr/weniger üben"). manualPriority
// −2..+2 (0 = normal) fließt ins Ranking des adaptiven Generators ein.
export type WeightableExercise = {
  exerciseId: string;
  exerciseName: string;
  exerciseNameEn?: string | null;
  difficulty: ExerciseDifficulty;
  manualPriority: number;
  // 0 = noch nie trainiert, 1 = hängt, 2 = mittel, 3 = sitzt.
  masteryStatus: 0 | 1 | 2 | 3;
  plannedThisWeek: boolean;
};

export type TrainingPlanItem = {
  id: string;
  weekNumber: number;
  exerciseId: string | null;
  exerciseName: string | null;
  exerciseNameEn?: string | null;
  freeTextLabel: string | null;
  repetitionsTarget: number;
  isRestWeek: boolean;
  completedCount: number;
  isComplete: boolean;
  logs: TrainingPlanItemLog[];
  reason: PlanItemReason | null;
  dayIndex: number;
};

export type TrainingPlan = {
  id: string;
  generatedAt: string;
  items: TrainingPlanItem[];
};

// Pro-Woche-Überschreibung der Trainingstage (siehe
// Domain.Planning.TrainingPlanWeekConfig). Nur Wochen mit abweichendem Wert
// sind enthalten; alle übrigen nutzen Goal.trainingDaysPerWeek.
export type WeekConfig = {
  weekNumber: number;
  trainingDaysPerWeek: number;
};

// Eine Prüfungsordnung, die nach einem erreichten Ziel als nächstes in Frage kommt.
export type NextStage = {
  regulationId: string;
  sportId: string;
  name: string;
  nameEn?: string | null;
};

export type Goal = {
  id: string;
  dogId: string;
  sportId: string;
  sportName: string;
  sportNameEn?: string | null;
  regulationId: string | null;
  regulationName: string | null;
  regulationNameEn?: string | null;
  targetDate: string;
  status: GoalStatus;
  notes: string | null;
  isCustom: boolean;
  weeklyExerciseCount: number;
  trainingDaysPerWeek: number;
  weekConfigs: WeekConfig[];
  trainingPlan: TrainingPlan | null;
  // Eine betreuende Trainer:in hat den Plan bearbeitet - dann wird er nicht
  // mehr automatisch wöchentlich neu aufgebaut.
  planManagedByTrainer: boolean;
  // Prüfungsergebnis eines erreichten Ziels (leer bei offenen und bei früher
  // ohne Ergebnis als erreicht markierten Zielen). Optional, weil der
  // Stale-While-Revalidate-Cache noch Ziele ohne diese Felder liefern kann.
  examDate?: string | null;
  examScore?: number | null;
  examNote?: string | null;
  // Höchstpunktzahl der Prüfungsordnung; null, wenn keine bekannt ist.
  maxPoints?: number | null;
  // Folgestufen - nur bei erreichten Zielen gefüllt.
  nextStages?: NextStage[] | null;
  // Die Trainingspausen des Hundes, die diesen Plan betreffen (nur Zeiträume).
  // Mit ihnen rechnet computeCurrentWeek dieselbe Planwoche wie der Server.
  planPausen?: PauseZeitraum[];
  // Wegen der Pausen reicht die Zeit bis zum Zieldatum nicht mehr für den Plan:
  // Die Oberfläche fragt, ob das Zieldatum verschoben werden soll.
  zieldatumZuKnapp?: boolean;
  // Prüfungsreife in Kurzform (Ampel und Zahlen) - nur bei laufenden Zielen mit
  // Prüfungsordnung. Optional, weil der Lesecache noch Ziele ohne das Feld liefern kann.
  pruefungsreife?: PruefungsreifeKurz | null;
};

// ---- Prüfungsreife ----

// Zahlen, keine Namen - die API überträgt Enums numerisch (siehe
// Application.Planning.Reifeampel). Die Beschriftungen stehen in lib/pruefungsreife.ts.
export const REIFE_AMPEL = { Gruen: 0, Gelb: 1, Rot: 2 } as const;
export type ReifeAmpel = (typeof REIFE_AMPEL)[keyof typeof REIFE_AMPEL];

export const REIFE_ZUSTAND = { Offen: 0, Wackelig: 1, Sicher: 2 } as const;
export type ReifeZustand = (typeof REIFE_ZUSTAND)[keyof typeof REIFE_ZUSTAND];

export const REIFE_GRUND = { SeltenTrainiert: 0, SchwacheBewertung: 1, LangeHer: 2 } as const;
export type ReifeGrund = (typeof REIFE_GRUND)[keyof typeof REIFE_GRUND];

// Backend: PruefungsreifeKurzDto.
export type PruefungsreifeKurz = {
  ampel: ReifeAmpel;
  offen: number;
  wackelig: number;
  sicher: number;
  zieldatumVerschieben: boolean;
};

// Eine Übung in der Auswertung (Backend: PruefungsreifeUebungDto).
export type PruefungsreifeUebung = {
  exerciseId: string;
  name: string;
  nameEn?: string | null;
  isMandatory: boolean;
  maxPoints: number;
  zustand: ReifeZustand;
  /** Nur bei "wackelig". */
  grund: ReifeGrund | null;
  /** Kalendertage seit dem letzten Training; null, wenn nie trainiert. */
  tageSeit: number | null;
  /** Davon Pausentage (zählen für "sicher" nicht mit). */
  pausenTage: number;
  /** Durchschnittsbewertung 1-5; null, wenn nie trainiert. */
  bewertung: number | null;
};

// Die Theoriezeile (Sachkunde) - nur für Besitzer:innen, nie für Trainer:innen
// (Backend: TheorieZeileDto; der Server liefert sie dann gar nicht aus).
export type PruefungsreifeTheorie = {
  zustand: ReifeZustand;
  /** Katalog, zu dem der Link führt. */
  catalogCode: string;
  gewertet: number;
  bestanden: number;
  inFehlerspeicher: number;
};

// Die ausführliche Auswertung (Backend: PruefungsreifeDto). Die Übungen stehen
// sortiert: erst alles, was noch fehlt, nach Gewicht absteigend, dann das Sichere.
export type Pruefungsreife = {
  ampel: ReifeAmpel;
  sicher: number;
  wackelig: number;
  offen: number;
  /** Trainingswochen bis zum Zieldatum (aufgerundet, ohne Pausentage). */
  wochen: number;
  /** Kalendertage bis zum Zieldatum. */
  tage: number;
  /** Pausentage zwischen heute und dem Zieldatum. */
  pausenTage: number;
  zieldatumVerschieben: boolean;
  /** Der Hund pausiert heute. */
  pausiert: boolean;
  uebungen: PruefungsreifeUebung[];
  theorie: PruefungsreifeTheorie | null;
};

// Ein von mir betreuter Hund (TrainerAssignment) - für die Trainerübersicht.
export type SupervisedDog = {
  id: string;
  name: string;
  breed: string | null;
  hasImage: boolean;
  handlerName: string;
  activeGoalCount: number;
  // Pause, die heute gilt - für Trainer:innen nur der Zeitraum.
  pause?: DogPause | null;
};

export type GroupMemberRole = 0 | 1; // 0 = Member, 1 = Trainer

// Verhältnis der angemeldeten Person zu einer Gruppe (Backend: GroupRelation).
// 0 = keins, 1 = Anfrage läuft, 2 = Mitglied, 3 = Trainer:in,
// 4 = eingeladen (noch nicht angenommen).
export type GroupRelation = 0 | 1 | 2 | 3 | 4;

export type Group = {
  id: string;
  name: string;
  description: string | null;
  trainerId: string;
  clubId: string | null;
  memberCount: number;
  trainerName: string | null;
  myRelation: GroupRelation;
};

// Offenes bei einer Trainer:in (Backend: TrainerOpenCountsDto) - nur Zahlen,
// die Listen laden erst auf den Seiten, auf denen man sie bearbeitet.
export type TrainerOpenCounts = {
  groupJoinRequests: number;
  clubJoinRequests: number;
  sessionsToRate: number;
  /** Offene Beitrittsanfragen je Gruppe; Gruppen ohne Anfragen fehlen. */
  groups: { groupId: string; joinRequests: number }[];
};

// Möglicher Gruppen-Trainer (alle Trainer:innen des Vereins der Gruppe).
export type GroupTrainerOption = {
  userId: string;
  firstName: string;
  lastName: string;
  email: string;
};

export type Club = {
  id: string;
  name: string;
  description: string | null;
  trainerCount: number;
  groupCount: number;
  // Ob die Person den Verein verwalten darf (Rolle Verwaltung) - nur in "meine Vereine" gesetzt.
  canManage?: boolean;
  /** Die eigenen Rollen als Bitmenge (siehe lib/vereinsrollen.ts); wie die folgenden Rechte nur in "meine Vereine". */
  roles?: number;
  /** Beitrittsanfragen, Einladungen, Einladungslink und Mitgliederliste (Mitgliederverwaltung, Verwaltung). */
  canManageMembers?: boolean;
  /** Trainer:innen berufen und abberufen (Ausbildungswart:in, Verwaltung). */
  canAppointTrainers?: boolean;
  /** Hat eine Trainer-Rolle (Training, Ausbildungswart:in, Verwaltung): Katalog, Gruppen. */
  isTrainer?: boolean;
};

// Vereins-Trainingsbibliothek (siehe docs/GROUP_TRAINING_LIBRARY.md).
export type GroupTrainingCategory = 0 | 1 | 2; // 0 = Welpen, 1 = Junghunde, 2 = Basis

// Prüfungs-Tags als Bitmaske (siehe Domain.Community.GroupExamTarget [Flags]).
export const GROUP_EXAM = { BH: 1, IBGH1: 2, IBGH2: 4, IBGH3: 8 } as const;

// Wiederverwendbarer Übungs-Baustein eines Vereins.
export type GroupTrainingExercise = {
  id: string;
  clubId: string;
  category: GroupTrainingCategory;
  title: string;
  focus: string | null;
  durationMinutes: number | null;
  description: string | null;
  examTargets: number; // Bitmaske aus GROUP_EXAM
};

// Ein Baustein an einer Position innerhalb einer Einheit.
export type GroupTrainingUnitItem = {
  id: string;
  exerciseId: string;
  sortOrder: number;
  exercise: GroupTrainingExercise;
};

// Geordnete Zusammenstellung von Bausteinen (verein-weit geteilte Vorlage).
export type GroupTrainingUnit = {
  id: string;
  clubId: string;
  category: GroupTrainingCategory;
  title: string;
  description: string | null;
  totalMinutes: number;
  items: GroupTrainingUnitItem[];
};

export type GroupTrainingLibrary = {
  clubId: string;
  clubName: string;
  exercises: GroupTrainingExercise[];
  units: GroupTrainingUnit[];
};

// Terminplanung (siehe docs/GROUP_TRAINING_SCHEDULE.md).
export type GroupTrainingSessionStatus = 0 | 1; // 0 = Geplant, 1 = Abgesagt

export type SessionItem = {
  id: string;
  exerciseId: string | null;
  freeText: string | null;
  sortOrder: number;
  exercise: GroupTrainingExercise | null;
};

export type SessionTrainer = { userId: string; firstName: string; lastName: string };

// Zu- oder Absage einer Person - nur für Trainer:innen des Vereins/Termins gefüllt.
export type SessionResponse = { userId: string; firstName: string; lastName: string; isAttending: boolean };

export type GroupTrainingSession = {
  id: string;
  clubId: string;
  groupId: string;
  groupName: string;
  category: GroupTrainingCategory;
  startsAt: string;
  durationMinutes: number;
  location: string | null;
  notes: string | null;
  status: GroupTrainingSessionStatus;
  plannedMinutes: number;
  items: SessionItem[];
  trainers: SessionTrainer[];
  // Eigene Antwort: true = kommt, false = kann nicht, null = noch offen.
  myResponse: boolean | null;
  // Zählung über aktive Gruppenmitglieder (für alle gleich).
  attendingCount: number;
  decliningCount: number;
  openCount: number;
  // Namen nur für Trainer:innen - für Mitglieder leer.
  responses: SessionResponse[];
  // Anmeldungen der Gruppe - nur für die, die die Gruppe verwalten dürfen, sonst 0.
  registrationCount: number;
};

export type ClubTrainerInfo = {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  assignedAt: string;
  /** Die Rollen der Person im Verein als Bitmenge (siehe lib/vereinsrollen.ts). */
  roles: number;
};

export type ClubMemberInfo = {
  membershipId: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  requestedAt: string;
  decidedAt: string | null;
  /** Ob die Person zugleich Trainer:in dieses Vereins ist (siehe ClubMemberRequest). */
  isTrainer: boolean;
  /** Ob die Person irgendeine Rolle im Verein hat; dann lässt sie sich nicht entfernen. */
  hatRolle: boolean;
  source: ClubMembershipSource;
};

export type ClubDetail = {
  club: Club;
  trainers: ClubTrainerInfo[];
  members: ClubMemberInfo[];
};

export type GroupMember = {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: GroupMemberRole;
  joinedAt: string;
};

// Eine:r der Trainer:innen einer Gruppe. isLead markiert die/den
// Hauptverantwortliche:n, alle anderen betreuen gleichberechtigt mit.
export type GroupTrainer = {
  userId: string;
  email: string;
  // Bei offenen Einladungen leer - den Namen erfährt die Gruppe erst mit der Zusage.
  firstName: string;
  lastName: string;
  isLead: boolean;
  isInvited: boolean;
};

export type GroupDetail = {
  group: Group;
  members: GroupMember[];
  trainers: GroupTrainer[];
  // Offene Einladungen - nur für Personen gefüllt, die die Gruppe verwalten.
  invitations: GroupMember[];
};

// Eine Gruppe aus Sicht des Mitglieds: aktive Mitgliedschaft oder offene
// Einladung (Backend: MyGroupMembershipDto).
export type MyGroupMembership = {
  groupId: string;
  groupName: string;
  clubName: string | null;
  trainerName: string | null;
  isInvitation: boolean;
  since: string;
  /** Einladung als Trainer:in statt als Mitglied. */
  asTrainer: boolean;
};

export type MemberDog = {
  id: string;
  name: string;
  breed: string | null;
  isTrainerAssigned: boolean;
  // Pause, die heute gilt - nur bei betreuten Hunden, nur der Zeitraum.
  pause?: DogPause | null;
};

export type AdminStats = {
  userCount: number;
  dogCount: number;
  groupCount: number;
  trainingSessionCount: number;
  gpsTrackCount: number;
  last30Days: AdminRecentStats;
};

/** Zählungen der letzten 30 Tage - keine Personen (siehe AdminRecentStatsDto). */
export type AdminRecentStats = {
  newAccounts: number;
  /** Die folgenden vier sind Teilmengen der neuen Konten. */
  withDog: number;
  inClub: number;
  withGoal: number;
  viaClubLink: number;
  /** Alle Konten (nicht nur neue) mit einem in den 30 Tagen angelegten Training oder einer Fährte. */
  activeAccounts: number;
};

/**
 * Wie und was genutzt wird - nur Zählungen aus vorhandenen Daten, keine
 * Personen (siehe AdminUsageDto). Tage sind ISO-Daten ("2026-10-26").
 */
export type AdminUsage = {
  activity: AdminUsageActivity;
  features: AdminFeatureUsage[];
  disabledModules: AdminDisabledModule[];
  retention: AdminRetention;
  timing: AdminUsageTiming;
  sports: AdminSportUsage[];
};

export type AdminUsageActivity = {
  totalAccounts: number;
  /** Richtwert aus den Anmelde-Tokens - untertreibt (siehe AdminUsageActivityDto). */
  openedLast7Days: number;
  openedLast30Days: number;
  /** Konten, die im Zeitraum etwas angelegt haben. */
  activeLast7Days: number;
  activeLast30Days: number;
  activeLast90Days: number;
  /** Die letzten 12 Kalenderwochen, die älteste zuerst. */
  weeks: AdminUsageWeek[];
};

export type AdminUsageWeek = {
  /** Der Montag der Woche. */
  weekStart: string;
  activeAccounts: number;
  entries: number;
};

export type AdminFeatureUsage = {
  /** Stabiler Schlüssel (kebab, englisch); die Namen stehen in lib/nutzung.ts. */
  key: string;
  accountsLast30Days: number;
  /** null, wo ein Eintrag nichts aussagt (Push). */
  entriesLast30Days: number | null;
  accountsEver: number;
  /** Tag des letzten Eintrags, null = nie genutzt. */
  lastUsed: string | null;
};

export type AdminDisabledModule = { key: string; accounts: number };

export type AdminRetention = {
  accountsOlderThan30Days: number;
  olderActiveLast30Days: number;
  neverCreatedWithoutDog: number;
  neverCreatedWithDog: number;
};

export type AdminUsageTiming = {
  /** Montag bis Sonntag. */
  byWeekday: number[];
  /** Stunde 0 bis 23. */
  byHour: number[];
};

export type AdminSportUsage = {
  name: string;
  nameEn: string | null;
  trainings: number;
  dogs: number;
};

/** Daten von Konten, die es nicht mehr gibt - nur Zählungen (siehe OrphanedDataDto). */
export type AdminOrphanedData = {
  konten: number;
  trainings: number;
  hunde: number;
  ziele: number;
  benachrichtigungen: number;
  sonstige: number;
};

export type AdminOrphanedDataPurge = { konten: number };

export type AdminUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  isLockedOut: boolean;
};

export type AdminUserPage = {
  users: AdminUser[];
  totalCount: number;
  totalPages: number;
  page: number;
  pageSize: number;
};

export type ClubSummary = {
  id: string;
  name: string;
  description: string | null;
  /** Die angemeldete Person hat eine Rolle in diesem Verein, gleich welche (hat dann keine Mitgliedschaftszeile). */
  isTrainer?: boolean;
  /** Die Rollen der Person als Bitmenge (siehe lib/vereinsrollen.ts). */
  roles?: number;
};

/**
 * 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Invited. Eingeladen ist noch
 * KEIN Mitglied: Der Verein hat die Person eingeladen, sie hat nicht
 * zugesagt (Backend: ClubMembershipStatus.Invited).
 */
export type ClubMembershipStatus = 0 | 1 | 2 | 3;

/** Auf welchem Weg eine Beitrittsanfrage kam. Numerisch, wie alle Aufzählungen der API. */
export type ClubMembershipSource = 0 | 1 | 2; // 0 = Directory (Vereinsliste), 1 = InviteLink, 2 = Invitation (eingeladen)

/** Einladungscode eines Vereins (GET/POST /api/clubs/{id}/invite-link). */
export type ClubInviteLink = { code: string };

/** Was die öffentliche Einladungsseite ohne Anmeldung erfährt: nur den Namen. */
export type ClubInvitePreview = { clubName: string };

/** Anmeldelink einer Gruppe (GET/POST /api/groups/{id}/registration-link). */
export type GroupRegistrationLink = { code: string };

/**
 * Was die öffentliche Anmeldeseite ohne Konto erfährt: Vereins- und Gruppenname.
 * Gehört die Gruppe keinem Verein, fehlt der Vereinsname.
 */
export type GroupRegistrationForm = { clubName: string | null; groupName: string };

/** Herkunft einer Anmeldung (numerisch wie alle Aufzählungen der API). */
export type GroupRegistrationSource = 0 | 1 | 2; // 0 = Formular, 1 = Import, 2 = von Hand

/**
 * Eine Anmeldung zu einer Gruppe (z. B. Welpengruppe). Die Angemeldeten haben
 * kein Konto - das sind Kursteilnehmende, keine Gruppenmitglieder.
 */
export type GroupRegistration = {
  id: string;
  firstName: string;
  lastName: string;
  dogName: string;
  dogBreed: string;
  /** Wurftag, "JJJJ-MM-TT". */
  dogBirthDate: string;
  phone: string;
  source: GroupRegistrationSource;
  registeredAt: string;
  /** Wann "bezahlt" gesetzt wurde; null = offen. */
  paidAt: string | null;
  notes: string | null;
  attendanceCount: number;
};

/** Ein Tag mit Termin der Gruppe, für die Tagesauswahl der Anwesenheit. */
export type AttendanceDay = { date: string; sessionId: string; startsAt: string };

export type ImportRegistrationsResult = {
  angelegt: number;
  uebersprungen: number;
  fehler: { zeile: number; meldung: string }[];
};

export type ClubMembership = {
  id: string;
  clubId: string;
  clubName: string;
  status: ClubMembershipStatus;
  requestedAt: string;
  decidedAt: string | null;
};

/**
 * Eine offene Einladung, wie der Verein sie sieht (GET /api/clubs/{id}/invitations):
 * nur die Adresse, die er selbst eingegeben hat - KEIN Name. Der gehört der
 * eingeladenen Person, bis sie zusagt.
 */
export type ClubInvitation = {
  membershipId: string;
  invitedEmail: string;
  requestedAt: string;
};

export type ClubMemberRequest = {
  membershipId: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  requestedAt: string;
  decidedAt: string | null;
  /**
   * Ob die Person zugleich Trainer:in dieses Vereins ist. Kommt vom Server -
   * Trainer:innen stehen in einer eigenen Tabelle, nicht in den
   * Mitgliedschaften, das lässt sich hier nicht herleiten.
   */
  isTrainer: boolean;
  /** Ob die Person irgendeine Rolle im Verein hat; dann lässt sie sich nicht entfernen. */
  hatRolle: boolean;
  /** Wie die Anfrage entstanden ist - der Einladungslink bekommt eine Kennzeichnung. */
  source: ClubMembershipSource;
};

export type GpsPointType = 0 | 1; // 0 = Automatic, 1 = Manual (siehe Domain.Tracking.GpsPointType)

// Fachliche Bedeutung eines manuellen Markers (siehe Domain.Tracking.GpsMarkerType).
// Entscheidet, wie ein Halt an dieser Stelle gewertet wird: am Gegenstand ist er
// ein erwünschtes Verweisen, am Leckerlipot/an einer Verleitung erklärt+neutral.
export type GpsMarkerType = 0 | 1 | 2 | 3; // Gegenstand | Leckerlipot | Verleitung | Sonstiges

export type GpsPoint = {
  latitude: number;
  longitude: number;
  timestamp: string;
  accuracy: number | null;
  pointType: GpsPointType;
  label: string | null;
  markerType: GpsMarkerType;
};

export type GpsWalkPoint = {
  latitude: number;
  longitude: number;
  timestamp: string;
  accuracy: number | null;
  // Senkrechter Abstand zur gelegten Fährte (null = nicht ausgewertet).
  deviationMeters: number | null;
};

// 0 = unerklärt (Warnsignal), 1 = Verweisen am Gegenstand (gut), 2 = erklärt/neutral.
export type WalkStopKind = 0 | 1 | 2;

export type GpsWalkStop = {
  latitude: number;
  longitude: number;
  durationSeconds: number;
  kind: WalkStopKind;
  markerLabel: string | null;
};

export type GpsWalkRun = {
  id: string;
  trackId: string;
  createdAt: string;
  lengthMeters: number | null;
  comment: string | null;
  points: GpsWalkPoint[];
  // Auswertung (null, solange nicht ausgewertet). Gemessen wird die Linie des
  // HUNDEFÜHRERS - der Hund kann im Leinenradius abweichen, ohne dass es hier
  // sichtbar wird; dafür gibt es die Stockungen (stops).
  avgDeviationMeters: number | null;
  maxDeviationMeters: number | null;
  onTrackPercent: number | null;
  articlesFound: number | null;
  articlesTotal: number | null;
  evaluatedAt: string | null;
  stops: GpsWalkStop[];
};

export type GpsTrack = {
  id: string;
  trainingSessionId: string;
  lengthMeters: number | null;
  ageMinutes: number | null;
  surface: string | null;
  weather: string | null;
  wind: string | null;
  comment: string | null;
  points: GpsPoint[];
  walkRuns: GpsWalkRun[];
  // Automatisch ermitteltes Wetter beim Legen und beim Suchen. Fachlich am
  // wichtigsten ist temperatureDeltaC - die Änderung dazwischen bestimmt
  // maßgeblich, wie sich die Geruchsspur hält.
  laidTemperatureC: number | null;
  laidRelativeHumidity: number | null;
  laidWindSpeedKmh: number | null;
  laidWeatherCode: number | null;
  searchTemperatureC: number | null;
  searchRelativeHumidity: number | null;
  searchWindSpeedKmh: number | null;
  searchWeatherCode: number | null;
  temperatureDeltaC: number | null;
  weatherFetchedAt: string | null;
};

// Treffer der Ortssuche (Open-Meteo Geocoding, siehe WeatherController).
export type GeocodeResult = {
  /** Zeile, die man wiedererkennt - z.B. "Hundesportverein". */
  name: string;
  /** Einordnung darunter, z.B. "Pforzheimer Straße 78 · 76275 Ettlingen". */
  detail: string | null;
  latitude: number;
  longitude: number;
};

/** Ein Ort, an dem schon trainiert wurde (Schnellauswahl statt Suche). */
export type RecentLocation = {
  name: string;
  latitude: number;
  longitude: number;
  lastUsed: string;
};

export type Notification = {
  id: string;
  message: string;
  linkPath: string | null;
  isRead: boolean;
  createdAt: string;
};

export type Profile = {
  firstName: string;
  lastName: string;
  email: string;
  avatarUrl: string | null;
};

// Ein vom Trainer zu bewertendes Training eines betreuten Hundes: Gesamt-
// Feedback + alle Übungen in einer Ansicht. rating je Übung = Selbstbewertung
// des Hundeführers, trainerRating = Bewertung des Trainers (null = offen).
export type TrainerSessionExercise = {
  exerciseId: string;
  exerciseName: string;
  exerciseNameEn?: string | null;
  rating: number;
  success: boolean;
  trainerRating: number | null;
  trainerNote: string | null;
};

export type TrainerSessionToRate = {
  sessionId: string;
  dogId: string;
  dogName: string;
  handlerName: string;
  date: string;
  durationMinutes: number;
  trainerFeedback: string | null;
  exercises: TrainerSessionExercise[];
};

export type DogOwnerRole = 0 | 1; // 0 = Owner, 1 = Trainer

export type DogOwner = {
  userId: string;
  email: string;
  // Bei offenen Einladungen leer - den Namen erfährt man erst mit der Zusage.
  firstName: string;
  lastName: string;
  role: DogOwnerRole;
  addedAt: string;
  isInvited: boolean;
};

/** Eine Trainer:in, die den Hund betreut und Tagebuch, Ziele und Fährten sieht - die Sicht der Besitzer:innen. */
export type DogBetreuung = {
  trainerId: string;
  trainerName: string;
  /** Beginn der Betreuung als ISO-Datum (YYYY-MM-DD). */
  startDate: string;
  /** Die Gruppe, über die sie läuft; null, wenn sich keine ermitteln lässt. */
  groupName: string | null;
};

/** Offene Einladung, einen Hund mitzuverwalten - die Sicht der eingeladenen Person. */
export type DogInvitation = {
  dogId: string;
  dogName: string;
  invitedByName: string | null;
  invitedAt: string;
};

export type WeeklyActivity = {
  week: string;
  count: number;
};

export type DogStats = {
  dogId: string;
  dogName: string;
  sessionCount: number;
  sessionsLast30d: number;
  activeGoals: number;
  avgRating30d: number | null;
  planItemsCompleted: number;
  planItemsTotal: number;
};

export type DashboardStats = {
  weeklyActivity: WeeklyActivity[];
  perDog: DogStats[];
};

// Kennzahlen pro Übung eines Hundes - schwächste zuerst (aufsteigend nach
// avgRating). Grundlage der lokalen, regelbasierten Fokus-Empfehlung.
export type DogExerciseStat = {
  exerciseName: string;
  exerciseNameEn?: string | null;
  count: number;
  avgRating: number;
  successRate: number; // 0..1
  ratingTrend: number | null; // Ø jüngere Hälfte − Ø ältere Hälfte, null bei <4 Durchgängen
  lastTrained: string;
};

// Fährten-Entwicklung eines Hundes (siehe StatsService.GetDogTrackStatsAsync).
export type DogTrackRun = {
  date: string;
  avgDeviationMeters: number;
  onTrackPercent: number;
  articlesFound: number;
  articlesTotal: number;
  unexplainedStops: number;
};

export type DogTrackStats = {
  runs: DogTrackRun[];
  // Negativ = Abweichung sinkt = Verbesserung.
  deviationTrend: number | null;
  // Positiv = mehr Zeit auf der Fährte = Verbesserung.
  onTrackTrend: number | null;
};

// Verfassung des Hundes an einem Trainingstag. Optional - wer nichts angibt,
// taucht in der Auswertung gar nicht auf, statt als "ausgeglichen" zu zählen.
//
// Zahlen, keine Namen: Die API überträgt Enums durchgehend numerisch (siehe
// ExerciseDifficulty, GoalStatus). Die Zuordnung zu Beschriftungen steht an
// genau einer Stelle - components/dogs/condition-picker.tsx.
export const DOG_CONDITION = {
  Motivated: 0,
  Settled: 1,
  Distracted: 2,
  Tired: 3,
  Stressed: 4,
} as const;

export type DogCondition = (typeof DOG_CONDITION)[keyof typeof DOG_CONDITION];

export type ConditionRating = {
  condition: DogCondition;
  sessionCount: number;
  avgRating: number | null;
  successRate: number | null;
};

// Bewertungen danach gruppiert, wie viele Tage unmittelbar davor schon
// trainiert wurde: 0 = am Vortag Pause, 1, 2 = zwei oder mehr am Stück.
export type TrainingDensity = {
  precedingTrainingDays: number;
  sessionCount: number;
  avgRating: number | null;
  tiredOrStressedShare: number | null;
};

export type DogConditionStats = {
  byCondition: ConditionRating[];
  byPrecedingDays: TrainingDensity[];
  sessionsWithCondition: number;
  sessionsTotal: number;
};

export type GroupJoinRequest = {
  memberId: string;
  email: string;
  firstName: string;
  lastName: string;
  requestedAt: string;
};

// ---- Sachkunde-Fragentrainer ----

export type QuizSection = {
  key: string;
  name: string;
  questionCount: number;
};

export type QuizCatalog = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  publisher: string;
  sourceUrl: string | null;
  edition: string | null;
  audience: "Adults" | "Youth";
  questionCount: number;
  sections: QuizSection[];
  // Kurzinfo zur Übungsprüfung; null = dieser Katalog hat keine. Fehlt das Feld
  // (ältere, noch zwischengespeicherte Antwort), gilt es wie null.
  exam?: QuizExamInfo | null;
};

export type QuizExamInfo = {
  questionCount: number;
  maxPoints: number;
  passPoints: number;
  passPercent: number;
};

export type QuizOption = {
  id: string;
  text: string;
  isCorrect: boolean;
  // Gesetzt, wenn die Antwort selbst ein Bild ist ("Welcher Hund zeigt eine
  // Spielhaltung?" - drei Zeichnungen, je eine je Antwort).
  imageName: string | null;
};

// Ein zuzuordnender Begriff ("Boxer", "Angst") samt richtigem Schlüssel.
export type QuizTerm = {
  id: string;
  text: string;
  solutionKey: string;
};

// Ein wählbarer Schlüssel. label ist leer, wenn die Schlüssel aus einer
// Abbildung stammen - dann sind es die Ziffern im Bild.
export type QuizKey = {
  key: string;
  label: string | null;
};

// Zuordnungen werden zugeordnet (Begriff -> Schlüssel) und vom Server geprüft.
// Nur die offenen Freitextfragen tragen statt Antworten eine Musterlösung, die
// man sich selbst abnimmt - die kann niemand automatisch prüfen.
export type QuizQuestionKind = "SingleChoice" | "MultipleChoice" | "Assignment" | "FreeText";

export type QuizQuestionState = {
  box: number;
  lastWasCorrect: boolean;
  correctCount: number;
  wrongCount: number;
  dueAt: string | null;
};

export type QuizQuestion = {
  id: string;
  number: string;
  section: string;
  sectionName: string;
  kind: QuizQuestionKind;
  text: string;
  imageName: string | null;
  sampleSolution: string | null;
  options: QuizOption[];
  terms: QuizTerm[];
  keys: QuizKey[];
  state: QuizQuestionState | null;
};

export type QuizSectionProgress = {
  key: string;
  name: string;
  total: number;
  answered: number;
  correct: number;
  mastered: number;
  inMistakes: number;
};

export type QuizProgress = {
  catalogCode: string;
  total: number;
  answered: number;
  // Fragen, deren letzte Antwort richtig war - bewegt sich mit jeder Antwort.
  correct: number;
  // Fragen ab Leitner-Fach 4, also mehrfach richtig an verschiedenen Tagen.
  mastered: number;
  inMistakes: number;
  dueNow: number;
  neverSeen: number;
  percentCorrect: number;
  percentMastered: number;
  sections: QuizSectionProgress[];
};

export type QuizSession = {
  catalogCode: string;
  mode: QuizMode;
  questions: QuizQuestion[];
  progress: QuizProgress;
  // Nichts mehr fällig und nichts mehr offen - der Moment für "von vorne".
  roundComplete: boolean;
};

export type QuizMode = "learn" | "mistakes" | "all";

export type QuizAnswerResult = {
  correct: boolean;
  box: number;
  dueAt: string | null;
  correctOptionIds: string[];
  // Bei Zuordnungen: je Begriffs-Id, ob sie richtig zugeordnet war.
  termResults: Record<string, boolean>;
  // Der Lernstand NACH dieser Antwort - damit der Balken sich sofort bewegt.
  progress: QuizProgress | null;
};

// ---- Sachkunde-Übungsprüfung ----

// Die gezogene Prüfung: Fragen OHNE Lösungen.
export type QuizExam = {
  catalogCode: string;
  // Zufällige Abgabe-Kennung vom Server: schließt die Doppelabgabe. Optional,
  // weil eine vor dem Ausrollen gezogene Prüfung (sessionStorage) sie noch nicht trägt.
  submissionId?: string;
  questionCount: number;
  maxPoints: number;
  passPoints: number;
  passPercent: number;
  questions: QuizExamQuestion[];
};

export type QuizExamQuestion = {
  id: string;
  number: string;
  section: string;
  sectionName: string;
  kind: QuizQuestionKind;
  text: string;
  imageName: string | null;
  options: { id: string; text: string; imageName: string | null }[];
  terms: { id: string; text: string }[];
  keys: QuizKey[];
};

// Antwort auf eine Frage der Prüfung; leer = unbeantwortet.
export type QuizExamAnswer = {
  questionId: string;
  selectedOptionIds: string[];
  assignments: Record<string, string>;
};

export type QuizExamSectionResult = {
  key: string;
  name: string;
  points: number;
  maxPoints: number;
};

export type QuizExamQuestionResult = {
  questionId: string;
  correct: boolean;
  points: number;
  maxPoints: number;
  correctOptionIds: string[];
  // Bei Zuordnungen: Begriffs-Id -> richtiger Schlüssel.
  correctAssignments: Record<string, string>;
};

export type QuizExamResult = {
  points: number;
  maxPoints: number;
  passPoints: number;
  passPercent: number;
  percent: number;
  passed: boolean;
  questionCount: number;
  correctCount: number;
  saved: boolean;
  sections: QuizExamSectionResult[];
  questions: QuizExamQuestionResult[];
};

export type QuizExamAttempt = {
  id: string;
  submittedAt: string;
  points: number;
  maxPoints: number;
  percent: number;
  passed: boolean;
  questionCount: number;
  correctCount: number;
  sections: QuizExamSectionResult[];
};

export type QuizExamHistory = {
  catalogCode: string;
  attempts: QuizExamAttempt[];
  passedOfLastFive: number;
  lastFiveCount: number;
};

// ---- Sachkunde-Verwaltung (nur Admin) ----

export type AdminQuizOption = {
  id: string;
  kind: "Answer" | "Term" | "Label";
  text: string;
  isCorrect: boolean;
  matchKey: string | null;
  imageName: string | null;
  sortOrder: number;
  // Hinweise auf auffällige Textstellen - kein Urteil, nur ein Wink.
  flags: string[];
};

export type AdminQuizQuestion = {
  id: string;
  catalogCode: string;
  catalogName: string;
  number: string;
  section: string;
  sectionName: string;
  kind: QuizQuestionKind;
  text: string;
  sampleSolution: string | null;
  imageName: string | null;
  // Gesetzt, sobald jemand die Frage von Hand überarbeitet hat - der Seeder
  // lässt sie dann in Ruhe.
  editedAt: string | null;
  options: AdminQuizOption[];
  flags: string[];
};

// ---- Geführter Erststart ----

// Woran der Erststart gerade steht. Nach dem Hund gabelt sich der Weg: selbst
// loslegen (Ziel, erstes Training) oder über den Verein (beitreten, Gruppe).
// Beides führt ans Ziel - erledigt ist der Erststart, sobald EINER gegangen ist.
export type OnboardingStatus = {
  hasDog: boolean;
  firstDogId: string | null;
  firstDogName: string | null;
  // Bei genau einem Hund führt "Training erfassen" auf dem Dashboard direkt
  // zu ihm statt auf die Liste.
  dogCount: number;
  hasGoal: boolean;
  hasTraining: boolean;
  hasClubMembership: boolean;
  // Anfrage gestellt, Freigabe steht aus - kein offener Schritt mehr.
  hasPendingClubRequest: boolean;
  hasGroupMembership: boolean;
  hasPendingGroupRequest: boolean;
  isDismissed: boolean;
  isComplete: boolean;
  // Wann das erste Training angelegt wurde (nicht: für welchen Tag). Fehlt bei
  // einem Zwischenstand aus der Zeit davor.
  firstTrainingAt?: string | null;
};

// ---- Startseite ----

// Ein Hund mit allem, was die Startseite von ihm braucht (GET /api/dashboard,
// siehe DashboardService). Ein Aufruf statt einer Anfrage je Hund und Abschnitt.
export type DashboardHund = {
  dog: Dog;
  // Wirksame Sportarten des Hundes; leer heißt "keine Einschränkung".
  sportIds: string[];
  // Aktive Ziele mit Trainingsplan - Quelle für "Diese Woche".
  activeGoals: Goal[];
  // Heute gelegte Fährten samt Abläufen - Quelle für "Heute gelegt".
  tracksToday: GpsTrack[];
};

// Trainer-Feedback, auf das noch nicht reagiert wurde (Danke/Verstanden/
// Rückfrage) - Quelle der Feedback-Karte. Spiegel von Application/Training/OpenFeedbackDto.
export type OffenesFeedback = {
  sessionId: string;
  dogId: string;
  dogName: string;
  // null, wenn das Konto der Trainer:in nicht mehr existiert.
  trainerName: string | null;
  feedback: string;
  feedbackAt: string | null;
};

export type DashboardDaten = {
  dogs: DashboardHund[];
  // Optional, weil der Stale-While-Revalidate-Cache noch Antworten ohne dieses Feld liefern kann.
  openFeedback?: OffenesFeedback[];
  /** Eigene offene Gruppeneinladungen (als Mitglied). Optional wie openFeedback: ältere Zwischenstände haben das Feld nicht. */
  groupInvitations?: DashboardGruppeneinladung[];
  /** Eigene offene Vereinseinladungen. Optional aus demselben Grund wie groupInvitations. */
  clubInvitations?: DashboardVereinseinladung[];
};

/** Eine offene Einladung in eine Trainingsgruppe (Backend: DashboardGroupInvitationDto). */
export type DashboardGruppeneinladung = {
  groupId: string;
  groupName: string;
  clubName: string | null;
};

/** Eine offene Einladung in einen Verein (Backend: DashboardClubInvitationDto). */
export type DashboardVereinseinladung = {
  clubId: string;
  clubName: string;
};

/**
 * Persönliche Einstellungen (siehe docs/VERBAENDE_SPRACHEN_MODULE.md).
 *
 * Zwei verschiedene Modelle, absichtlich: `disabledModules` ist eine
 * NEGATIV-Liste - gespeichert wird, was abgewählt wurde, damit ein künftig
 * hinzukommendes Modul bei allen von selbst erscheint. `sportIds` ist eine
 * POSITIV-Liste - die Aussage ist "ich mache genau das", eine neue Sportart
 * soll sich niemandem aufdrängen. Leer heißt dort "keine Einschränkung".
 */
export type UserPreferences = {
  locale: string | null;
  /** Geltungsbereich der Prüfungsordnungen (ISO 3166-1 alpha-2), null = Vorgabe. */
  country: string | null;
  /** Schriftgröße der Oberfläche (siehe lib/schriftgroesse.ts), null = Vorgabe. */
  fontScale: string | null;
  disabledModules: string[];
  sportIds: string[];
  /** Ob wichtige Benachrichtigungen (Einladungen, Entscheidungen) auch per E-Mail kommen. Vorgabe: ja. */
  emailNotifications: boolean;
  /** Erinnerungen (Glocke + Push): Termin am Vortag, noch nicht abgelaufene Fährte, offene Rückmeldungen. Vorgabe: alle an. */
  reminderSessions: boolean;
  reminderTracks: boolean;
  reminderTrainerSummary: boolean;
  /** Wochentage der Zusammenfassung für Trainer:innen als Bitmaske (Mo = 1 ... So = 64, lib/erinnerungen.ts). Vorgabe: Sonntag. */
  trainerSummaryDays: number;
  /** Uhrzeit der Zusammenfassung, volle Stunde 8 bis 21 (Vereinszeit). Vorgabe: 18. */
  trainerSummaryHour: number;
  /** Erinnerung an ablaufende Impfungen, fällige Entwurmung und Tierarzttermine. Vorgabe: ja. */
  reminderHealth: boolean;
};

/** Ein wählbarer Geltungsbereich - Spiegel von Application/Sports/CountryDto. */
export type Country = {
  code: string;
  regulationCount: number;
};

/** Schlüssel der abschaltbaren Module - Spiegel von Application/Preferences/Modules.cs. */
export const MODULE = {
  faehrte: "faehrte",
  sachkunde: "sachkunde",
  gruppentraining: "gruppentraining",
  wetter: "wetter",
  statistik: "statistik",
  gesundheit: "gesundheit",
} as const;

/**
 * Antrag auf einen neuen Verein.
 *
 * `status` kommt NUMERISCH: 0 = offen, 1 = freigegeben, 2 = abgelehnt.
 * Die API hat keinen JsonStringEnumConverter (wie bei DogCondition und
 * ExerciseDifficulty) - ein Textvergleich ginge hier still schief.
 */
export type ClubRegistration = {
  id: string;
  name: string;
  description: string | null;
  requestedByUserId: string;
  requestedByEmail: string;
  requestedByName: string;
  status: 0 | 1 | 2;
  requestedAt: string;
  decidedAt: string | null;
  decisionNote: string | null;
  clubId: string | null;
};

export const CLUB_REGISTRATION_STATUS = { offen: 0, freigegeben: 1, abgelehnt: 2 } as const;

/** Antwort von POST /api/admin/testmail ("Testmail senden" im Adminbereich). */
export type AdminTestmailErgebnis = {
  /** false: kein SMTP konfiguriert - es wurde nichts verschickt. */
  versandEingerichtet: boolean;
  /** Ob der Mailserver die Mail angenommen hat (zugestellt ist sie damit noch nicht). */
  versendet: boolean;
  /** Antwort bzw. Fehler des Mailservers, wenn er die Mail nicht angenommen hat. */
  fehler: string | null;
};

/** Antwort von POST /api/admin/testpush ("Test-Push an mich" im Adminbereich). */
export type AdminTestpushErgebnis = {
  /** false: Server ohne VAPID-Schlüssel oder ohne Frontend-Adresse - nichts verschickt. */
  pushEingerichtet: boolean;
  /** Auf wie vielen eigenen Geräten Push eingeschaltet ist - an die geht die Meldung. */
  geraete: number;
};
