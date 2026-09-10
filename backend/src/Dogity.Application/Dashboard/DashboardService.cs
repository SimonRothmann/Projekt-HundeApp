using Dogity.Application.Common;
using Dogity.Application.Dogs;
using Dogity.Application.Planning;
using Dogity.Application.Preferences;
using Dogity.Application.Tracking;
using Dogity.Application.Training;
using Dogity.Domain.Planning;

namespace Dogity.Application.Dashboard;

/// <summary>
/// Alles, was die Startseite je Hund braucht, in einem Aufruf.
///
/// Vorher holte das Frontend erst die Hunde und danach je Hund Sportarten,
/// Ziele, Trainings und Fährten - bei zwei Hunden 13 Anfragen in zwei bis drei
/// Wellen hintereinander. Auf dem Handy kostet jede Welle eine Rundreise samt
/// Vorab-Anfrage (CORS), und die Abschnitte der Startseite tauchten
/// nacheinander auf.
///
/// Bewusst aus den bestehenden Diensten zusammengesetzt statt mit eigenen
/// Abfragen: Zugriffsregeln, die Vererbung der Sportarten und die
/// Zusammenstellung der Pläne gibt es so weiterhin nur an einer Stelle. Die
/// Aufrufe laufen nacheinander, weil sich alle Dienste einen DbContext teilen.
/// </summary>
public class DashboardService(
    IDogService dogs,
    IPreferenceService preferences,
    IGoalService goals,
    ITrainingService trainings,
    IGpsTrackService tracks,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var meineHunde = await dogs.GetMyDogsAsync(userId, ct);
        if (!meineHunde.Succeeded)
            return Result<DashboardDto>.Failure([.. meineHunde.Errors]);

        // UTC-Datum wie beim Fährtenrecorder, der die Einheit so datiert
        // (siehe TODO.md: Fährte über Mitternacht).
        var heute = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var eintraege = new List<DashboardDogDto>();

        foreach (var hund in meineHunde.Value!.Where(h => h.ArchivedAt is null))
        {
            var sportIds = (await preferences.GetEffectiveDogSportsAsync(userId, hund.Id, ct)).Value ?? [];

            var aktiveZiele = ((await goals.GetByDogAsync(userId, hund.Id, ct)).Value ?? [])
                .Where(ziel => ziel.Status == GoalStatus.Active && ziel.TrainingPlan is not null)
                .ToList();

            var faehrten = new List<GpsTrackDto>();
            var einheiten = (await trainings.GetByDogAsync(userId, hund.Id, heute, heute, ct)).Value ?? [];
            foreach (var einheit in einheiten.Where(e => e.HasGpsTrack))
                faehrten.AddRange((await tracks.GetByTrainingSessionAsync(userId, einheit.Id, ct)).Value ?? []);

            eintraege.Add(new DashboardDogDto(hund, sportIds, aktiveZiele, faehrten));
        }

        return Result<DashboardDto>.Success(new DashboardDto(eintraege));
    }
}
