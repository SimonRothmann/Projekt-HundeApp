namespace Dogity.Application.Reminders;

/// <summary>
/// Versendet die automatischen Erinnerungen (Termin am Vortag, Fährte noch nicht
/// abgelaufen, offene Rückmeldungen der Trainer:innen). Wird vom Hintergrunddienst
/// alle zehn Minuten aufgerufen und ist selbst für Wiederholungen ausgelegt: Was
/// schon versendet wurde, steht in <see cref="Dogity.Domain.Notifications.SentReminder"/>.
/// </summary>
public interface IErinnerungService
{
    /// <summary>Versendet alle jetzt fälligen Erinnerungen und gibt zurück, wie viele es waren.</summary>
    Task<int> VersendeFaelligeAsync(CancellationToken ct = default);
}
