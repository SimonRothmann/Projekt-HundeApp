namespace Dogity.Application.Onboarding;

/// <summary>
/// Woran der geführte Erststart gerade steht.
///
/// Nach dem Hund gabelt sich der Weg: entweder auf eigene Faust (Ziel setzen,
/// erstes Training) oder über den Verein (beitreten, Trainingsgruppe). Beides
/// führt ans Ziel, deshalb gilt der Erststart als erledigt, sobald EINER der
/// beiden Wege gegangen ist.
/// </summary>
/// <param name="FirstDogId">
/// Für die Verweise "Ziel setzen" und "Erstes Training" - beide führen auf die
/// Hundeseite. Null, solange es keinen Hund gibt.
/// </param>
/// <param name="IsComplete">
/// Die Festlegung, wann der Erststart erledigt ist: ein Hund UND (ein Training ODER
/// eine Gruppe). Maßgeblich ist diese Regel (siehe docs/ERSTSTART.md); die Karte im
/// Frontend entscheidet bisher nach eigener Zeitregel (lib/erststart.ts).
/// </param>
/// <param name="HasPendingClubRequest">
/// Beitrittsanfrage gestellt, aber noch nicht freigegeben. Wichtig für die
/// Anzeige: "warte auf Freigabe" ist etwas anderes als "noch nichts getan" -
/// ohne diesen Unterschied stünde der Schritt offen da, obwohl der Nutzer
/// getan hat, was er tun konnte.
/// </param>
/// <param name="FirstTrainingAt">
/// Wann das älteste Training der eigenen Hunde angelegt wurde (nicht: für
/// welchen Tag es eingetragen ist). Daran hängt die einmalige Karte "Gut
/// gemacht! Wie geht es weiter?": Sie gilt nur kurz nach dem ersten Training -
/// wer es vor Monaten eingetragen hat, soll sie nicht plötzlich sehen. Null,
/// solange es kein Training gibt.
/// </param>
public record OnboardingStatusDto(
    bool HasDog,
    Guid? FirstDogId,
    bool HasGoal,
    bool HasTraining,
    bool HasClubMembership,
    bool HasPendingClubRequest,
    bool HasGroupMembership,
    bool HasPendingGroupRequest,
    bool IsDismissed,
    bool IsComplete,
    DateTimeOffset? FirstTrainingAt);
