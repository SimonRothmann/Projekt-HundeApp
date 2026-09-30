using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// Die Zu- oder Absage eines Gruppenmitglieds zu einem
/// <see cref="GroupTrainingSession"/>. Je (Termin, Person) gibt es genau eine
/// Zeile - wer es sich anders überlegt, ändert sie, statt eine zweite
/// anzulegen. Keine Antwort heißt "offen", nicht "kommt nicht".
///
/// Ob die Person noch Mitglied der Gruppe ist, steht hier bewusst nicht: Die
/// Zeile bleibt, wenn jemand die Gruppe verlässt, und wird nur nicht mehr
/// mitgezählt (siehe GroupTrainingScheduleService.MapAsync).
/// </summary>
public class GroupTrainingSessionResponse : Entity
{
    public Guid GroupTrainingSessionId { get; set; }
    public GroupTrainingSession? Session { get; set; }

    public Guid UserId { get; set; }

    /// <summary>true = "Ich komme", false = "Kann nicht".</summary>
    public bool IsAttending { get; set; }

    /// <summary>Zeitpunkt der letzten Antwort (ändert sich beim Umschalten).</summary>
    public DateTimeOffset RespondedAt { get; set; } = DateTimeOffset.UtcNow;
}
