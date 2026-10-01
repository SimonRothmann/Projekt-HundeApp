using Dogity.Domain.Common;

namespace Dogity.Domain.Community;

/// <summary>
/// "War an diesem Tag da": Je (<see cref="RegistrationId"/>, <see cref="Date"/>)
/// gibt es höchstens eine Zeile. Wer abhakt, legt sie an, wer den Haken
/// zurücknimmt, entfernt sie (DeletedAt) - ein erneutes Abhaken belebt die
/// entfernte Zeile wieder (SoftDeleteRevival), weil der eindeutige Index kein
/// DeletedAt kennt.
///
/// Der Tag ist ein Kalendertag in der Vereinszeit, kein Zeitpunkt. Gibt es zu
/// dem Tag einen Termin der Gruppe, steht er in
/// <see cref="GroupTrainingSessionId"/>; zwingend ist das nicht, Welpengruppen
/// treffen sich oft ohne eingetragenen Termin.
/// </summary>
public class GroupRegistrationAttendance : Entity
{
    public Guid RegistrationId { get; set; }
    public GroupRegistration? Registration { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>
    /// Der Termin der Gruppe an diesem Tag, falls es einen gab. Ohne
    /// Fremdschlüssel: Termine werden nur weich gelöscht, und der Verweis soll
    /// eine Terminänderung nicht blockieren.
    /// </summary>
    public Guid? GroupTrainingSessionId { get; set; }

    /// <summary>Wer abgehakt hat; wird bei Löschung dieses Kontos auf null gesetzt.</summary>
    public Guid? MarkedByUserId { get; set; }
    public DateTimeOffset MarkedAt { get; set; } = DateTimeOffset.UtcNow;
}
