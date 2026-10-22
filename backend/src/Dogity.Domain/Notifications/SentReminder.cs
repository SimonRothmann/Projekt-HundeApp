using Dogity.Domain.Common;

namespace Dogity.Domain.Notifications;

/// <summary>Die Arten automatischer Erinnerungen (siehe Dogity.Application.Reminders).</summary>
public enum ReminderType
{
    /// <summary>Termin einer Gruppe am Folgetag, an Mitglieder.</summary>
    SessionDayBefore,

    /// <summary>Eine gelegte Fährte wurde noch nicht abgelaufen, die 24-Stunden-Frist läuft noch.</summary>
    TrackNotWalked,

    /// <summary>Zusammenfassung offener Rückmeldungen, an Trainer:innen.</summary>
    TrainerFeedbackSummary
}

/// <summary>
/// Merkt sich, dass eine Erinnerung schon versendet wurde - damit sie bei einem
/// Neustart, bei zwei Läufen kurz hintereinander oder auf zwei Instanzen nicht
/// ein zweites Mal kommt. Der eindeutige Index (UserId, Type, ReferenceId, DayKey)
/// ist die eigentliche Sperre: Wer als Zweiter einfügt, scheitert und sendet nicht.
///
/// Gespeichert wird bewusst nur das Nötige, kein Text: Der Satz steht als
/// Benachrichtigung in der Glocke, hier genügt "wofür, für wen, wann".
/// </summary>
public class SentReminder : Entity
{
    /// <summary>Wert von <see cref="DayKey"/> bei Erinnerungen, die je Bezug nur einmal kommen.</summary>
    public static readonly DateOnly KeinTag = DateOnly.MinValue;

    public Guid UserId { get; set; }
    public ReminderType Type { get; set; }

    /// <summary>
    /// Worauf sich die Erinnerung bezieht: die Id des Termins bzw. der Fährte;
    /// bei der Zusammenfassung (kein einzelner Bezug) <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid ReferenceId { get; set; }

    /// <summary>
    /// Der Kalendertag in Vereinszeit bei Erinnerungen, die täglich neu fällig
    /// werden können (Zusammenfassung), beim Termin der Termintag (ein verschobener
    /// Termin wird erneut erinnert); sonst <see cref="KeinTag"/>. Der Schlüssel
    /// ist der Tag und nicht die Stunde: Ändert jemand die Uhrzeit nach dem
    /// Versand, kommt am selben Tag keine zweite.
    /// </summary>
    public DateOnly DayKey { get; set; } = KeinTag;
}
