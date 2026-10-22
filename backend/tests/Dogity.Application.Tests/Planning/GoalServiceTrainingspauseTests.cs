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
/// Der Trainingsplan pausiert mit dem Hund (docs/SMART_TRAINING_PLAN.md,
/// Abschnitt "Pausen"): Die automatische Anpassung ruht während einer Pause,
/// die Planwoche zählt die Pausentage nicht, der Hinweis "Zieldatum
/// verschieben?" kommt nur, wenn die Zeit wirklich nicht mehr reicht, und das
/// Zieldatum lässt sich ohne Prüfung verschieben.
/// </summary>
public class GoalServiceTrainingspauseTests
{
    // Mittwoch, 4. November 2026, 10:00 Uhr deutscher Zeit.
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = new(2026, 11, 4);

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    private sealed record Aufbau(
        ApplicationDbContext Db,
        GoalService Ziele,
        FakeNotificationService Meldungen,
        Guid OwnerId,
        Guid TrainerId,
        Guid DogId,
        Guid GoalId,
        Guid Woche4ItemId,
        Guid Woche6ItemId);

    /// <summary>
    /// Ein Ziel, das vor 28 Tagen angelegt wurde (heute Planwoche 5), mit acht
    /// Planwochen und dem Zieldatum 55 Tage nach dem Anlegen - ohne Pause genau
    /// passend.
    /// </summary>
    private static async Task<Aufbau> AufbauAsync()
    {
        var db = InMemoryDbContext.Create();
        var meldungen = new FakeNotificationService();
        var ziele = new GoalService(db, new FesteUhr(Jetzt), meldungen, new ExerciseMasteryService(db));

        var ownerId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var sportId = Guid.NewGuid();
        var dog = new Dog { Name = "Flitzer", Gender = DogGender.Female };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerId, MemberId = ownerId, DogId = dog.Id, StartDate = Heute });

        var uebungen = new[] { "A", "B", "C", "D", "E" }
            .Select(n => new Exercise { Name = n, SportId = sportId, Difficulty = ExerciseDifficulty.Beginner })
            .ToArray();
        db.Exercises.AddRange(uebungen);

        var angelegt = Jetzt.AddDays(-28);
        var goal = new Goal
        {
            DogId = dog.Id,
            SportId = sportId,
            TargetDate = Heute.AddDays(27),
            Status = GoalStatus.Active,
            WeeklyExerciseCount = 2,
            TrainingDaysPerWeek = 1,
            CreatedAt = angelegt,
        };
        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal, GeneratedAt = angelegt };
        for (var woche = 1; woche <= 8; woche++)
            plan.Items.Add(new TrainingPlanItem
            {
                TrainingPlanId = plan.Id,
                WeekNumber = woche,
                ExerciseId = uebungen[woche % uebungen.Length].Id,
                RepetitionsTarget = 2,
                Source = PlanItemSource.Auto,
            });
        goal.TrainingPlan = plan;
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        return new Aufbau(
            db, ziele, meldungen, ownerId, trainerId, dog.Id, goal.Id,
            plan.Items.Single(i => i.WeekNumber == 4).Id,
            plan.Items.Single(i => i.WeekNumber == 6).Id);
    }

    private static async Task PauseAsync(Aufbau a, int vonTag, int? bisTag, DogPauseReason grund = DogPauseReason.Injured)
    {
        a.Db.DogPauses.Add(new DogPause
        {
            DogId = a.DogId,
            Reason = grund,
            StartDate = Heute.AddDays(vonTag),
            EndDate = bisTag is { } b ? Heute.AddDays(b) : null,
            Note = "Pfote",
        });
        await a.Db.SaveChangesAsync();
    }

    // --- Anzeige: Pausenzeiträume und Hinweis ---------------------------------

    [Fact]
    public async Task Ziel_OhnePause_KeineZeitraeumeKeinHinweis()
    {
        var a = await AufbauAsync();

        var ziel = (await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!;

        Assert.Empty(ziel.PlanPausen!);
        Assert.False(ziel.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task Ziel_PauseImPlan_LiefertNurDenZeitraum_FuerBesitzerUndTrainerin()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -10, -5);

        foreach (var nutzer in new[] { a.OwnerId, a.TrainerId })
        {
            var ziel = (await a.Ziele.GetByDogAsync(nutzer, a.DogId)).Value!.Single();
            var zeitraum = Assert.Single(ziel.PlanPausen!);
            Assert.Equal(Heute.AddDays(-10), zeitraum.Start);
            Assert.Equal(Heute.AddDays(-5), zeitraum.End);
        }
    }

    [Fact]
    public async Task Ziel_FremdePersonen_SehenKeinZielUndAlsoKeinePause()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -10, -5);

        var result = await a.Ziele.GetByDogAsync(Guid.NewGuid(), a.DogId);

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Ziel_KurzePause_ZeitReichtNochAus_KeinHinweis()
    {
        var a = await AufbauAsync();
        // Drei Tage: Am Zieldatum (Tag 55 nach dem Anlegen) stünde der Plan trotzdem in Woche 8.
        await PauseAsync(a, -3, -1);

        Assert.False((await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task Ziel_LangePause_ZeitReichtNichtMehr_Hinweis()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -14, -1);

        Assert.True((await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task Ziel_OffenePauseSeitKurzem_MahntNochNicht()
    {
        var a = await AufbauAsync();
        // Wie lange es weitergeht, weiß niemand: Es zählt nur, was schon verstrichen ist.
        await PauseAsync(a, -2, null);

        Assert.False((await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task Ziel_OffenePauseSeitWochen_DieBereitsVerlorenenWochenZaehlen()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -14, null);

        Assert.True((await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task Ziel_PauseNachDemZieldatum_HatKeineWirkung()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, 40, 60);

        var ziel = (await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!;

        Assert.Empty(ziel.PlanPausen!);
        Assert.False(ziel.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task Ziel_PauseVorDemPlanbeginn_HatKeineWirkung()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -90, -60);

        var ziel = (await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!;

        Assert.Empty(ziel.PlanPausen!);
        Assert.False(ziel.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task Ziel_Individuell_BekommtKeinenHinweis()
    {
        var a = await AufbauAsync();
        (await a.Db.Goals.SingleAsync()).IsCustom = true;
        await a.Db.SaveChangesAsync();
        await PauseAsync(a, -14, -1);

        Assert.False((await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!.ZieldatumZuKnapp);
    }

    // --- Automatische Anpassung ruht ------------------------------------------

    [Fact]
    public async Task RegenerateDuePlans_OhnePause_BautDieKommendeWocheNeu()
    {
        var a = await AufbauAsync();

        var anzahl = await a.Ziele.RegenerateDuePlansAsync();

        Assert.Equal(1, anzahl);
        // Heute ist Planwoche 5, neu gebaut wird Woche 6.
        Assert.NotNull((await a.Db.TrainingPlanItems.IgnoreQueryFilters().SingleAsync(i => i.Id == a.Woche6ItemId)).DeletedAt);
        Assert.Null((await a.Db.TrainingPlanItems.SingleAsync(i => i.Id == a.Woche4ItemId)).DeletedAt);
    }

    [Fact]
    public async Task RegenerateDuePlans_HundPausiertHeute_PlanBleibtUnangetastet()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -2, 5);

        var anzahl = await a.Ziele.RegenerateDuePlansAsync();

        Assert.Equal(0, anzahl);
        Assert.Empty(a.Meldungen.Created);
        Assert.Null((await a.Db.Goals.SingleAsync()).LastPlanGeneratedAt);
        Assert.Empty(await a.Db.TrainingPlanItems.IgnoreQueryFilters().Where(i => i.DeletedAt != null).ToListAsync());
    }

    [Fact]
    public async Task RegenerateDuePlans_OffenePause_PlanBleibtUnangetastet()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -30, null);

        Assert.Equal(0, await a.Ziele.RegenerateDuePlansAsync());
    }

    [Fact]
    public async Task RegenerateDuePlans_NachDerPause_RechnetOhnePausentage()
    {
        var a = await AufbauAsync();
        // Zwei Wochen Pause, gestern zu Ende: 28 Tage vergangen, 14 davon Pause
        // -> Planwoche 3, neu gebaut wird Woche 4 (nicht Woche 6).
        await PauseAsync(a, -14, -1);

        var anzahl = await a.Ziele.RegenerateDuePlansAsync();

        Assert.Equal(1, anzahl);
        Assert.NotNull((await a.Db.TrainingPlanItems.IgnoreQueryFilters().SingleAsync(i => i.Id == a.Woche4ItemId)).DeletedAt);
        Assert.Null((await a.Db.TrainingPlanItems.SingleAsync(i => i.Id == a.Woche6ItemId)).DeletedAt);
    }

    // --- Zieldatum verschieben ---------------------------------------------------

    [Fact]
    public async Task UpdateTargetDate_VerschiebtUndVerlaengertDenPlan()
    {
        var a = await AufbauAsync();
        var neu = Heute.AddDays(27 + 14);

        var result = await a.Ziele.UpdateTargetDateAsync(a.OwnerId, a.GoalId, neu);

        Assert.True(result.Succeeded);
        Assert.Equal(neu, result.Value!.TargetDate);
        var plan = result.Value.TrainingPlan!;
        // Zwei Wochen mehr als die acht, die da waren.
        Assert.True(plan.Items.Max(i => i.WeekNumber) >= 10);
    }

    [Fact]
    public async Task UpdateTargetDate_BehebtDenHinweis()
    {
        var a = await AufbauAsync();
        await PauseAsync(a, -14, -1);
        Assert.True((await a.Ziele.GetByIdAsync(a.OwnerId, a.GoalId)).Value!.ZieldatumZuKnapp);

        var result = await a.Ziele.UpdateTargetDateAsync(a.OwnerId, a.GoalId, Heute.AddDays(27 + 14));

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.ZieldatumZuKnapp);
    }

    [Fact]
    public async Task UpdateTargetDate_FremdeBekommenNichtGefunden()
    {
        var a = await AufbauAsync();

        var result = await a.Ziele.UpdateTargetDateAsync(Guid.NewGuid(), a.GoalId, Heute.AddDays(60));

        Assert.True(result.IsNotFound);
        Assert.Equal(Heute.AddDays(27), (await a.Db.Goals.SingleAsync()).TargetDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task UpdateTargetDate_DatumInDerVergangenheit_WirdAbgelehnt(int tage)
    {
        var a = await AufbauAsync();

        var result = await a.Ziele.UpdateTargetDateAsync(a.OwnerId, a.GoalId, Heute.AddDays(tage));

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
    }

    [Fact]
    public async Task UpdateTargetDate_BeendetesZiel_WirdAbgelehnt()
    {
        var a = await AufbauAsync();
        (await a.Db.Goals.SingleAsync()).Status = GoalStatus.Cancelled;
        await a.Db.SaveChangesAsync();

        Assert.False((await a.Ziele.UpdateTargetDateAsync(a.OwnerId, a.GoalId, Heute.AddDays(60))).Succeeded);
    }
}
