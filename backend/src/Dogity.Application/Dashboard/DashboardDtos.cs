using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Domain.Tracking;

namespace Dogity.Application.Dashboard;

// Ein Hund mit allem, was die Startseite von ihm braucht:
// - SportIds: wirksame Sportarten (siehe PreferenceService.GetEffectiveDogSportsAsync),
//   leer heißt "keine Einschränkung". Das Frontend entscheidet damit, ob es
//   "Fährte legen" anbietet (lib/faehrte.ts).
// - ActiveGoals: aktive Ziele mit Trainingsplan - die Quelle für "Diese Woche".
// - TracksToday: die noch suchbaren und noch nicht abgelaufenen Spuren (Fährte bis 24, Trail bis 48
//   Stunden nach dem Ende des Legens, siehe Ablauffristen) - die Quelle für "Heute gelegt". Der Name
//   stammt aus der Zeit, als es nur die von heute waren.
public record DashboardDogDto(
    DogDto Dog,
    IReadOnlyList<Guid> SportIds,
    IReadOnlyList<GoalDto> ActiveGoals,
    IReadOnlyList<DashboardSpurDto> TracksToday);

// Eine gelegte Spur, wie die Startseite sie zum Ablaufen braucht: die Legung mit den Angaben, die der
// Recorder und das Ergebnis-Fenster lesen. Ohne Abläufe (die Spur hat noch keinen, sonst stünde sie
// nicht hier), Wetter, Kommentar und Entwicklungs-Schalter - das alles lädt die Hundeseite selbst.
public record DashboardSpurDto(
    Guid Id,
    Guid TrainingSessionId,
    double? LengthMeters,
    int? AgeMinutes,
    string? Surface,
    GpsTrackKind Kind,
    bool DoubleBlind,
    bool NegativeTrail,
    string? ScentArticle,
    IReadOnlyList<GpsPointDto> Points);

// Eine offene Einladung in eine Trainingsgruppe (als Mitglied), die die Person
// auf der Startseite mit einem Tipp annehmen oder ablehnen kann.
public record DashboardGroupInvitationDto(Guid GroupId, string GroupName, string? ClubName);

// Dasselbe für eine offene Einladung in einen Verein.
public record DashboardClubInvitationDto(Guid ClubId, string ClubName);

// OpenFeedback: Trainer-Feedback zu den eigenen Hunden, auf das noch nicht
// reagiert wurde (neuestes zuerst) - die Quelle der Feedback-Karte.
// GroupInvitations: nur die EIGENEN offenen Gruppeneinladungen - die Quelle der
// Einladungs-Karte direkt unter dem Kopf.
// ClubInvitations: ebenso die eigenen offenen Vereinseinladungen.
public record DashboardDto(
    IReadOnlyList<DashboardDogDto> Dogs,
    IReadOnlyList<OpenFeedbackDto> OpenFeedback,
    IReadOnlyList<DashboardGroupInvitationDto> GroupInvitations,
    IReadOnlyList<DashboardClubInvitationDto> ClubInvitations);
