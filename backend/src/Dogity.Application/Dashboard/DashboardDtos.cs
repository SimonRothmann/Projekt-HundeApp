using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Tracking;
using Dogity.Application.Training;

namespace Dogity.Application.Dashboard;

// Ein Hund mit allem, was die Startseite von ihm braucht:
// - SportIds: wirksame Sportarten (siehe PreferenceService.GetEffectiveDogSportsAsync),
//   leer heißt "keine Einschränkung". Das Frontend entscheidet damit, ob es
//   "Fährte legen" anbietet (lib/faehrte.ts).
// - ActiveGoals: aktive Ziele mit Trainingsplan - die Quelle für "Diese Woche".
// - TracksToday: heute gelegte Fährten samt Abläufen - die Quelle für "Heute gelegt".
public record DashboardDogDto(
    DogDto Dog,
    IReadOnlyList<Guid> SportIds,
    IReadOnlyList<GoalDto> ActiveGoals,
    IReadOnlyList<GpsTrackDto> TracksToday);

// Eine offene Einladung in eine Trainingsgruppe (als Mitglied), die die Person
// auf der Startseite mit einem Tipp annehmen oder ablehnen kann.
public record DashboardGroupInvitationDto(Guid GroupId, string GroupName, string? ClubName);

// OpenFeedback: Trainer-Feedback zu den eigenen Hunden, auf das noch nicht
// reagiert wurde (neuestes zuerst) - die Quelle der Feedback-Karte.
// GroupInvitations: nur die EIGENEN offenen Gruppeneinladungen - die Quelle der
// Einladungs-Karte direkt unter dem Kopf.
public record DashboardDto(
    IReadOnlyList<DashboardDogDto> Dogs,
    IReadOnlyList<OpenFeedbackDto> OpenFeedback,
    IReadOnlyList<DashboardGroupInvitationDto> GroupInvitations);
