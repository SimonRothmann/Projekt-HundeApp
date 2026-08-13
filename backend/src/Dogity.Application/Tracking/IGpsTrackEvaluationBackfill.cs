namespace Dogity.Application.Tracking;

/// <summary>
/// Einmalige Nachauswertung bestehender Ablauf-Versuche (siehe
/// <see cref="GpsTrackEvaluator"/>). Läuft beim Anwendungsstart nach den
/// Migrationen und wertet nur Abläufe ohne Auswertung aus - dadurch
/// idempotent und nach dem ersten Durchlauf praktisch kostenlos.
/// </summary>
public interface IGpsTrackEvaluationBackfill
{
    Task<int> BackfillAsync(CancellationToken ct = default);
}
