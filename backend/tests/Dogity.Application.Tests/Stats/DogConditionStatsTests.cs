using Dogity.Application.Stats;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Dogity.Domain.Training;

namespace Dogity.Application.Tests.Stats;

/// <summary>
/// Testet die Auswertung der Verfassung - vor allem die Trainingsdichte, also
/// den Zusammenhang, den man im Alltag nicht sieht, weil niemand seine
/// Trainingstage zusammenzählt.
/// </summary>
public class DogConditionStatsTests
{
    private static readonly DateOnly Start = new(2026, 3, 2); // ein Montag

    private static (StatsService Dienst, Guid Nutzer, Guid Hund, Action<int, DogCondition?, int[]> Trainiere,
        Func<Task> Speichern) Aufbau()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = Guid.NewGuid();
        var hund = new Dog { Name = "Test" };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = nutzer });

        void Trainiere(int tagVersatz, DogCondition? verfassung, int[] bewertungen)
        {
            var einheit = new TrainingSession
            {
                UserId = nutzer,
                DogId = hund.Id,
                Date = Start.AddDays(tagVersatz),
                DurationMinutes = 30,
                Condition = verfassung,
            };
            db.TrainingSessions.Add(einheit);
            foreach (var b in bewertungen)
                db.TrainingExercises.Add(new TrainingExercise
                {
                    TrainingSessionId = einheit.Id, TrainingSession = einheit,
                    FreeTextLabel = "Übung", Rating = b, Success = b >= 3,
                });
        }

        return (new StatsService(db), nutzer, hund.Id, Trainiere, () => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Verfassung_WirdMitDurchschnittsbewertungAusgewiesen()
    {
        var (dienst, nutzer, hund, trainiere, speichern) = Aufbau();
        trainiere(0, DogCondition.Motivated, [5, 5]);
        trainiere(7, DogCondition.Distracted, [2, 2]);
        trainiere(14, DogCondition.Distracted, [3, 3]);
        await speichern();

        var stand = (await dienst.GetDogConditionStatsAsync(nutzer, hund)).Value!;

        var motiviert = stand.ByCondition.Single(c => c.Condition == DogCondition.Motivated);
        var abgelenkt = stand.ByCondition.Single(c => c.Condition == DogCondition.Distracted);
        Assert.Equal(5, motiviert.AvgRating);
        Assert.Equal(2.5, abgelenkt.AvgRating);
        Assert.Equal(2, abgelenkt.SessionCount);
    }

    [Fact]
    public async Task Trainingsdichte_ZaehltZusammenhaengendeVortage()
    {
        var (dienst, nutzer, hund, trainiere, speichern) = Aufbau();
        trainiere(0, null, [5]);   // nach Pause
        trainiere(1, null, [4]);   // zweiter Tag in Folge
        trainiere(2, null, [2]);   // dritter Tag
        trainiere(10, null, [5]);  // wieder nach Pause
        await speichern();

        var stand = (await dienst.GetDogConditionStatsAsync(nutzer, hund)).Value!;

        var nachPause = stand.ByPrecedingDays.Single(d => d.PrecedingTrainingDays == 0);
        var zweiter = stand.ByPrecedingDays.Single(d => d.PrecedingTrainingDays == 1);
        var dritter = stand.ByPrecedingDays.Single(d => d.PrecedingTrainingDays == 2);

        Assert.Equal(2, nachPause.SessionCount);
        Assert.Equal(5, nachPause.AvgRating);
        Assert.Equal(4, zweiter.AvgRating);
        Assert.Equal(2, dritter.AvgRating);
    }

    [Fact]
    public async Task Trainingsdichte_ZaehltHoechstensBisZwei()
    {
        var (dienst, nutzer, hund, trainiere, speichern) = Aufbau();
        for (var tag = 0; tag < 5; tag++) trainiere(tag, null, [3]);
        await speichern();

        var stand = (await dienst.GetDogConditionStatsAsync(nutzer, hund)).Value!;

        // Fünf Tage am Stück, aber nur drei Gruppen: mehr Stufen würden die
        // Gruppen so klein machen, dass der Schnitt nichts mehr aussagt.
        Assert.Equal([0, 1, 2], stand.ByPrecedingDays.Select(d => d.PrecedingTrainingDays));
        Assert.Equal(3, stand.ByPrecedingDays.Single(d => d.PrecedingTrainingDays == 2).SessionCount);
    }

    [Fact]
    public async Task MuedeUndGestresst_WerdenAlsAnteilAusgewiesen()
    {
        var (dienst, nutzer, hund, trainiere, speichern) = Aufbau();
        trainiere(0, DogCondition.Motivated, [5]);
        trainiere(1, DogCondition.Tired, [3]);
        trainiere(7, DogCondition.Stressed, [2]);
        await speichern();

        var stand = (await dienst.GetDogConditionStatsAsync(nutzer, hund)).Value!;

        // Tag 1 ist der zweite Tag in Folge und war müde -> 100 %.
        Assert.Equal(1.0, stand.ByPrecedingDays.Single(d => d.PrecedingTrainingDays == 1).TiredOrStressedShare);
        // Tag 0 (motiviert) und Tag 7 (gestresst) folgen beide auf eine Pause.
        Assert.Equal(0.5, stand.ByPrecedingDays.Single(d => d.PrecedingTrainingDays == 0).TiredOrStressedShare);
    }

    [Fact]
    public async Task OhneAngabe_VerfaelschtDenAnteilNicht()
    {
        var (dienst, nutzer, hund, trainiere, speichern) = Aufbau();
        trainiere(0, DogCondition.Tired, [3]);
        trainiere(7, null, [4]);
        trainiere(14, null, [4]);
        await speichern();

        var stand = (await dienst.GetDogConditionStatsAsync(nutzer, hund)).Value!;

        // Ein Hund darf nicht umso ausgeglichener aussehen, je seltener jemand
        // etwas eingetragen hat: gezählt wird nur, wo eine Angabe da ist.
        Assert.Equal(1.0, stand.ByPrecedingDays.Single(d => d.PrecedingTrainingDays == 0).TiredOrStressedShare);
        Assert.Equal(1, stand.SessionsWithCondition);
        Assert.Equal(3, stand.SessionsTotal);
    }

    [Fact]
    public async Task OhneJedeAngabe_BleibtDerAnteilLeer()
    {
        var (dienst, nutzer, hund, trainiere, speichern) = Aufbau();
        trainiere(0, null, [4]);
        await speichern();

        var stand = (await dienst.GetDogConditionStatsAsync(nutzer, hund)).Value!;

        Assert.Empty(stand.ByCondition);
        Assert.Null(stand.ByPrecedingDays.Single().TiredOrStressedShare);
        Assert.Equal(4, stand.ByPrecedingDays.Single().AvgRating);
    }

    [Fact]
    public async Task FremderHund_IstNichtEinsehbar()
    {
        var (dienst, _, hund, _, speichern) = Aufbau();
        await speichern();

        var ergebnis = await dienst.GetDogConditionStatsAsync(Guid.NewGuid(), hund);

        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
    }
}
