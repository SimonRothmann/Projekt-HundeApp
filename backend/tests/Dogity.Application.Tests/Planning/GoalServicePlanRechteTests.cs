using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Plan-Einstellungen eines Ziels: Übungen und Trainingstage pro Woche, eine Plan-Übung
/// streichen, eine Übung gewichten. Wer sie ändert, ist wer das Ziel sehen darf
/// (Besitzer:in, Mitbesitzer:in, betreuende Trainer:in - sie durfte Pläne schon immer
/// bearbeiten, siehe GoalServiceRegenerateTests); Fremde bekommen "nicht gefunden" und
/// ändern nichts.
/// </summary>
public class GoalServicePlanRechteTests
{
    private sealed record Welt(
        ApplicationDbContext Db, GoalService Dienst, Guid Besitzer, Guid Goal, Guid Plan, Guid Uebung, Guid PlanItem);

    private static async Task<Welt> AufbauenAsync(bool individuell = false)
    {
        var db = InMemoryDbContext.Create();
        var besitzer = Guid.NewGuid();
        var sport = new Sport { Code = "BH", Name = "BH" };
        var uebung = new Exercise { SportId = sport.Id, Name = "Sitz", Difficulty = ExerciseDifficulty.Beginner };
        var hund = new Dog { Name = "Bello" };
        db.Sports.Add(sport);
        db.Exercises.Add(uebung);
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = besitzer });
        var ziel = new Goal
        {
            DogId = hund.Id, SportId = sport.Id, TargetDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(2),
            Status = GoalStatus.Active, IsCustom = individuell, WeeklyExerciseCount = 4, TrainingDaysPerWeek = 2,
        };
        var plan = new TrainingPlan { GoalId = ziel.Id, Goal = ziel };
        ziel.TrainingPlan = plan;
        var eintrag = new TrainingPlanItem { TrainingPlanId = plan.Id, WeekNumber = 1, ExerciseId = uebung.Id, RepetitionsTarget = 2, DayIndex = 1, Source = PlanItemSource.Auto };
        db.Goals.Add(ziel);
        db.TrainingPlanItems.Add(eintrag);
        await db.SaveChangesAsync();
        return new Welt(db, new GoalService(db, TimeProvider.System, new FakeNotificationService(), new ExerciseMasteryService(db)),
            besitzer, ziel.Id, plan.Id, uebung.Id, eintrag.Id);
    }

    private static async Task<Guid> BetreuerAsync(Welt w)
    {
        var trainer = Guid.NewGuid();
        var hund = await w.Db.Dogs.SingleAsync();
        w.Db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainer, MemberId = w.Besitzer, DogId = hund.Id });
        await w.Db.SaveChangesAsync();
        return trainer;
    }

    // ---------------------------------------------------------- Übungen und Tage

    [Fact]
    public async Task Einstellungen_DieBesitzerinAendertSie()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.UpdateConfigAsync(w.Besitzer, w.Goal, 6, 3);

        Assert.True(ergebnis.Succeeded);
        var ziel = await w.Db.Goals.SingleAsync();
        Assert.Equal(6, ziel.WeeklyExerciseCount);
        Assert.Equal(3, ziel.TrainingDaysPerWeek);
    }

    [Fact]
    public async Task Einstellungen_DieBetreuendeTrainerinDarfSieAendern_EineFremdePersonNicht()
    {
        var w = await AufbauenAsync();
        var trainer = await BetreuerAsync(w);

        Assert.True((await w.Dienst.UpdateConfigAsync(trainer, w.Goal, 5, 2)).Succeeded);

        var fremde = await w.Dienst.UpdateConfigAsync(Guid.NewGuid(), w.Goal, 9, 7);
        Assert.True(fremde.IsNotFound);
        Assert.Equal(5, (await w.Db.Goals.SingleAsync()).WeeklyExerciseCount);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(13, 3)]
    [InlineData(4, 0)]
    [InlineData(4, 8)]
    public async Task Einstellungen_AusserhalbDerGrenzen_WerdenAbgewiesen(int uebungen, int tage)
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.UpdateConfigAsync(w.Besitzer, w.Goal, uebungen, tage);

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Equal(4, (await w.Db.Goals.SingleAsync()).WeeklyExerciseCount);
    }

    // ------------------------------------------------------- Plan-Übung streichen

    [Fact]
    public async Task PlanUebungStreichen_DieBesitzerinStreicht()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.RemovePlanItemAsync(w.Besitzer, w.Goal, w.PlanItem);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(ergebnis.Value!.TrainingPlan!.Items);
        Assert.Empty(w.Db.TrainingPlanItems);
    }

    [Fact]
    public async Task PlanUebungStreichen_EineFremdePersonStreichtNichts()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.RemovePlanItemAsync(Guid.NewGuid(), w.Goal, w.PlanItem);

        Assert.True(ergebnis.IsNotFound);
        Assert.Single(w.Db.TrainingPlanItems);
    }

    [Fact]
    public async Task PlanUebungStreichen_EinePlanUebungEinesAnderenZielsIstNichtGefunden()
    {
        var w = await AufbauenAsync();
        var anderes = new Goal { DogId = (await w.Db.Dogs.SingleAsync()).Id, SportId = Guid.NewGuid(), TargetDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(1), Status = GoalStatus.Active };
        var andererPlan = new TrainingPlan { GoalId = anderes.Id, Goal = anderes };
        anderes.TrainingPlan = andererPlan;
        var fremdesItem = new TrainingPlanItem { TrainingPlanId = andererPlan.Id, WeekNumber = 1, FreeTextLabel = "Anderes", RepetitionsTarget = 1, DayIndex = 1 };
        w.Db.Goals.Add(anderes);
        w.Db.TrainingPlanItems.Add(fremdesItem);
        await w.Db.SaveChangesAsync();

        var ergebnis = await w.Dienst.RemovePlanItemAsync(w.Besitzer, w.Goal, fremdesItem.Id);

        Assert.True(ergebnis.IsNotFound);
        Assert.Equal(2, w.Db.TrainingPlanItems.Count());
    }

    // ----------------------------------------------------------- Gewichtung

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Gewichtung_DieBesitzerinSetztSie(int wert)
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.SetExercisePriorityAsync(w.Besitzer, w.Goal, w.Uebung, wert);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(wert, (await w.Db.ExerciseMasteries.SingleAsync()).ManualPriority);
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(3)]
    public async Task Gewichtung_AusserhalbVonMinusZweiBisZwei_WirdAbgewiesen(int wert)
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.SetExercisePriorityAsync(w.Besitzer, w.Goal, w.Uebung, wert);

        Assert.False(ergebnis.Succeeded);
        Assert.Empty(w.Db.ExerciseMasteries);
    }

    [Fact]
    public async Task Gewichtung_EineFremdePersonSetztNichts()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Dienst.SetExercisePriorityAsync(Guid.NewGuid(), w.Goal, w.Uebung, 1);

        Assert.True(ergebnis.IsNotFound);
        Assert.Empty(w.Db.ExerciseMasteries);
    }

    [Fact]
    public async Task Gewichtung_EineUebungAusEinerAnderenSportartGehoertNichtZumZiel()
    {
        var w = await AufbauenAsync();
        var fremde = new Exercise { SportId = Guid.NewGuid(), Name = "Fremd" };
        w.Db.Exercises.Add(fremde);
        await w.Db.SaveChangesAsync();

        var ergebnis = await w.Dienst.SetExercisePriorityAsync(w.Besitzer, w.Goal, fremde.Id, 1);

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Empty(w.Db.ExerciseMasteries);
    }

    [Fact]
    public async Task Gewichtung_BeimIndividuellenPlanOhneWirkung_WirdAbgewiesen()
    {
        var w = await AufbauenAsync(individuell: true);

        Assert.False((await w.Dienst.SetExercisePriorityAsync(w.Besitzer, w.Goal, w.Uebung, 1)).Succeeded);
    }
}
