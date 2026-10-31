using Dogity.Application.Common;
using Dogity.Application.Community;
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
    IGroupService groups,
    IClubService clubs,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var meineHunde = await dogs.GetMyDogsAsync(userId, ct);
        if (!meineHunde.Succeeded)
            return Result<DashboardDto>.Failure([.. meineHunde.Errors]);

        // Der Kalendertag der Vereinszeit, wie ihn auch der Fährtenrecorder für
        // die Einheit nimmt - zwischen 0 und 2 Uhr ist es in UTC noch gestern.
        var jetzt = timeProvider.GetUtcNow();
        // Eine noch suchbare Spur kann an einer Einheit der letzten Tage hängen: Ein Trail
        // liegt 48 Stunden, und das Legen kann über Mitternacht hinaus dauern - daher ein
        // Tag Luft vor dem Tag, an dem die längste Frist beginnt.
        var einheitenAb = Vereinszeit.Tag(jetzt - Ablauffristen.Trail).AddDays(-1);
        var heute = Vereinszeit.Tag(jetzt);
        var eintraege = new List<DashboardDogDto>();

        // Jeder Fehler einer Teilabfrage wird wie bei den Hunden weitergereicht: Eine leere
        // Liste wäre nicht von "nichts da" zu unterscheiden, bei den Sportarten hieße sie
        // sogar "keine Einschränkung" und die Startseite zeigte alle Module.
        foreach (var hund in meineHunde.Value!.Where(h => h.ArchivedAt is null))
        {
            var sportIds = await preferences.GetEffectiveDogSportsAsync(userId, hund.Id, ct);
            if (!sportIds.Succeeded) return Fehler(sportIds);

            var ziele = await goals.GetByDogAsync(userId, hund.Id, ct);
            if (!ziele.Succeeded) return Fehler(ziele);
            var aktiveZiele = ziele.Value!
                .Where(ziel => ziel.Status == GoalStatus.Active && ziel.TrainingPlan is not null)
                .ToList();

            // Die noch suchbaren Spuren (Fährte 24, Trail 48 Stunden nach dem Legen), nicht nur
            // die von heute: Was abends gelegt wurde, soll nach Mitternacht nicht von der
            // Startseite verschwinden, solange man es noch ablaufen kann.
            var faehrten = new List<GpsTrackDto>();
            var einheiten = await trainings.GetByDogAsync(userId, hund.Id, einheitenAb, heute, ct);
            if (!einheiten.Succeeded) return Fehler(einheiten);
            foreach (var einheit in einheiten.Value!.Where(e => e.HasGpsTrack))
            {
                var spuren = await tracks.GetByTrainingSessionAsync(userId, einheit.Id, ct);
                if (!spuren.Succeeded) return Fehler(spuren);
                faehrten.AddRange(spuren.Value!.Where(spur =>
                    Ablauffristen.NochZuSuchen(Ablauffristen.LegeEnde(spur.Points), spur.Kind, jetzt)));
            }

            eintraege.Add(new DashboardDogDto(hund, sportIds.Value!, aktiveZiele, faehrten));
        }

        var offenesFeedback = await trainings.GetOpenFeedbackAsync(userId, eintraege.Select(e => e.Dog.Id).ToList(), ct);
        if (!offenesFeedback.Succeeded) return Fehler(offenesFeedback);

        // Einladungen als Trainer:in bleiben außen vor: Sie haben eigene Endpunkte
        // und gehören auf die Gruppenseite, nicht in eine Ein-Tipp-Karte.
        var einladungen = (await groups.GetMyOpenInvitationsAsync(userId, ct))
            .Select(e => new DashboardGroupInvitationDto(e.GroupId, e.GroupName, e.ClubName))
            .ToList();

        var vereinsEinladungen = (await clubs.GetMyOpenInvitationsAsync(userId, ct))
            .Select(e => new DashboardClubInvitationDto(e.ClubId, e.ClubName))
            .ToList();

        return Result<DashboardDto>.Success(new DashboardDto(eintraege, offenesFeedback.Value!, einladungen, vereinsEinladungen));
    }

    private static Result<DashboardDto> Fehler(Result teil) => Result<DashboardDto>.Failure([.. teil.Errors]);
}
