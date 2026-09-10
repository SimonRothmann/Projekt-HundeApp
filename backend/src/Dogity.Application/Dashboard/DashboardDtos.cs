using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Tracking;

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

public record DashboardDto(IReadOnlyList<DashboardDogDto> Dogs);
