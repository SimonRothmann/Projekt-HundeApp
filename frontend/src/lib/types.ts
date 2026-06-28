export type AuthResponse = {
  token: string;
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
};

export type Sport = {
  id: string;
  code: string;
  name: string;
  description: string | null;
};

export type ExerciseDifficulty = "Beginner" | "Intermediate" | "Advanced";

export type Exercise = {
  id: string;
  sportId: string;
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
  exerciseId: string;
  exerciseName: string;
  rating: number;
  difficulty: ExerciseDifficulty;
  success: boolean;
  notes: string | null;
};

export type TrainingSession = {
  id: string;
  dogId: string;
  date: string;
  durationMinutes: number;
  notes: string | null;
  exercises: TrainingExercise[];
};

export type GoalStatus = 0 | 1 | 2; // 0 = Active, 1 = Achieved, 2 = Cancelled

export type TrainingPlanItem = {
  id: string;
  weekNumber: number;
  exerciseId: string | null;
  exerciseName: string | null;
  repetitionsTarget: number;
  isRestWeek: boolean;
};

export type TrainingPlan = {
  id: string;
  generatedAt: string;
  items: TrainingPlanItem[];
};

export type Goal = {
  id: string;
  dogId: string;
  sportId: string;
  sportName: string;
  targetDate: string;
  status: GoalStatus;
  notes: string | null;
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
};

export type Club = {
  id: string;
  name: string;
  description: string | null;
  trainerCount: number;
  groupCount: number;
};

export type ClubTrainerInfo = {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  assignedAt: string;
};

export type ClubDetail = {
  club: Club;
  trainers: ClubTrainerInfo[];
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
};

export type GpsPoint = {
  latitude: number;
  longitude: number;
  timestamp: string;
  accuracy: number | null;
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
};
