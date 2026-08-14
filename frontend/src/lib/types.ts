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
};

export type Sport = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  clubId: string | null;
};

export type ExerciseDifficulty = 0 | 1 | 2; // 0 = Beginner, 1 = Intermediate, 2 = Advanced (siehe Domain.Sports.ExerciseDifficulty)

export type Exercise = {
  id: string;
  sportId: string | null;
  name: string;
  description: string | null;
  difficulty: ExerciseDifficulty;
  category: string | null;
  scoringCriteria: string | null;
  clubId: string | null;
};

export type ParsedExerciseCandidate = {
  name: string;
  maxPoints: number;
};

export type Regulation = {
  id: string;
  name: string;
  sourceUrl: string | null;
  lastSyncedAt: string | null;
  latestKnownVersionLabel: string | null;
  // Mehrzeilige Kurzbeschreibung der Prüfungs-Rahmenbedingungen (Schrittzahl,
  // Winkel, Fährtenalter, Voraussetzungen, Bestehensgrenze, ...).
  description: string | null;
};

export type RegulationVersionInfo = {
  id: string;
  versionLabel: string;
  validFrom: string;
};

export type RegulationExerciseInfo = {
  exerciseId: string;
  exerciseName: string;
  isMandatory: boolean;
  maxPoints: number;
  scoringNotes: string | null;
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
  // Ob mindestens eine Fährte existiert - erspart den GPS-Request pro
  // Trainings-Karte (GpsTrackSection wird bei abgeschlossenen Trainings
  // ohne Fährte gar nicht erst gemountet, siehe SessionHistory).
  hasGpsTrack: boolean;
};

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
};

// Grund, warum der adaptive Generator eine Übung geplant hat (siehe
// Domain.Planning.PlanItemReason). null bei manuellen Einträgen/Pausenwochen.
export type PlanItemReason = 0 | 1 | 2; // 0 = Schwäche, 1 = Wiederholung, 2 = Neu

// Eine gewichtbare Übung eines Ziels ("mehr/weniger üben"). manualPriority
// −2..+2 (0 = normal) fließt ins Ranking des adaptiven Generators ein.
export type WeightableExercise = {
  exerciseId: string;
  exerciseName: string;
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

export type Goal = {
  id: string;
  dogId: string;
  sportId: string;
  sportName: string;
  regulationId: string | null;
  regulationName: string | null;
  targetDate: string;
  status: GoalStatus;
  notes: string | null;
  isCustom: boolean;
  weeklyExerciseCount: number;
  trainingDaysPerWeek: number;
  weekConfigs: WeekConfig[];
  trainingPlan: TrainingPlan | null;
};

export type GroupMemberRole = 0 | 1; // 0 = Member, 1 = Trainer

export type Group = {
  id: string;
  name: string;
  description: string | null;
  trainerId: string;
  clubId: string | null;
  memberCount: number;
  trainerName: string | null;
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
};

export type ClubTrainerInfo = {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  assignedAt: string;
};

export type ClubMemberInfo = {
  membershipId: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  requestedAt: string;
  decidedAt: string | null;
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

export type GroupDetail = {
  group: Group;
  members: GroupMember[];
};

export type MemberDog = {
  id: string;
  name: string;
  breed: string | null;
  isTrainerAssigned: boolean;
};

export type AdminStats = {
  userCount: number;
  dogCount: number;
  groupCount: number;
  trainingSessionCount: number;
  gpsTrackCount: number;
};

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
};

export type ClubMembershipStatus = 0 | 1 | 2; // 0 = Pending, 1 = Approved, 2 = Rejected

export type ClubMembership = {
  id: string;
  clubId: string;
  clubName: string;
  status: ClubMembershipStatus;
  requestedAt: string;
  decidedAt: string | null;
};

export type ClubMemberRequest = {
  membershipId: string;
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  requestedAt: string;
  decidedAt: string | null;
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
  firstName: string;
  lastName: string;
  role: DogOwnerRole;
  addedAt: string;
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

export type GroupJoinRequest = {
  memberId: string;
  email: string;
  firstName: string;
  lastName: string;
  requestedAt: string;
};
