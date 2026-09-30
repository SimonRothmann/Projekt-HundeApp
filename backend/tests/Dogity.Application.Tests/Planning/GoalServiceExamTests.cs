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
/// "Ziel abschließen" (Prüfungsergebnis), nachträgliches Bearbeiten des
/// Ergebnisses, Höchstpunktzahl und Folgestufen.
/// </summary>
public class GoalServiceExamTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = DateOnly.FromDateTime(Jetzt.UtcDateTime);

    private sealed class FesteUhr : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Jetzt;
    }

    private sealed record Setup(Guid OwnerId, Guid DogId, Guid GoalId, Guid PlanId, Guid SportId, Guid RegulationId);

    private static GoalService MakeService(out ApplicationDbContext db)
    {
        db = InMemoryDbContext.Create();
        return new GoalService(db, new FesteUhr(), new FakeNotificationService(), new ExerciseMasteryService(db));
    }

    /// <summary>
    /// Ein Hund mit einem aktiven Ziel auf eine Prüfungsordnung "Prüfung A". Ihre
    /// NEUESTE Fassung hat 3 Übungen à 100 Punkte = 300; eine ältere Fassung
    /// hat eine andere Summe (40) und darf nicht zählen.
    /// </summary>
    private static async Task<Setup> SetupAsync(
        ApplicationDbContext db,
        string regulationName = "Prüfung A",
        bool mitRegulation = true,
        int punkteJeUebung = 100,
        GoalStatus status = GoalStatus.Active,
        DateOnly? zieldatum = null,
        DateTimeOffset? angelegtAm = null,
        bool individuell = false)
    {
        var ownerId = Guid.NewGuid();
        var sport = new Sport { Code = "X", Name = "Sport X" };
        db.Sports.Add(sport);
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });

        Regulation? regulation = null;
        if (mitRegulation)
        {
            regulation = new Regulation { SportId = sport.Id, Name = regulationName };
            db.Regulations.Add(regulation);
            var alt = new RegulationVersion { RegulationId = regulation.Id, VersionLabel = "alt", ValidFrom = new DateOnly(2024, 1, 1) };
            var neu = new RegulationVersion { RegulationId = regulation.Id, VersionLabel = "neu", ValidFrom = new DateOnly(2025, 1, 1) };
            db.RegulationVersions.AddRange(alt, neu);
            for (var i = 0; i < 3; i++)
            {
                var uebung = new Exercise { Name = $"Übung {i}", SportId = sport.Id, Difficulty = ExerciseDifficulty.Beginner };
                db.Exercises.Add(uebung);
                db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = neu.Id, ExerciseId = uebung.Id, MaxPoints = punkteJeUebung });
                if (i == 0)
                    db.RegulationExercises.Add(new RegulationExercise { RegulationVersionId = alt.Id, ExerciseId = uebung.Id, MaxPoints = 40 });
            }
        }

        var goal = new Goal
        {
            DogId = dog.Id,
            SportId = sport.Id,
            RegulationId = regulation?.Id,
            TargetDate = zieldatum ?? Heute.AddDays(-2),
            Status = status,
            IsCustom = individuell,
            CreatedAt = angelegtAm ?? Jetzt.AddDays(-70),
        };
        var plan = new TrainingPlan { GoalId = goal.Id, Goal = goal };
        goal.TrainingPlan = plan;
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return new Setup(ownerId, dog.Id, goal.Id, plan.Id, sport.Id, regulation?.Id ?? Guid.Empty);
    }

    private static CompleteGoalRequest Bestanden(DateOnly? tag = null, int? punkte = null, string? notiz = null) =>
        new(true, tag ?? Heute, punkte, notiz, null);

    // --- bestanden ---------------------------------------------------------

    [Fact]
    public async Task Complete_Bestanden_SetztErreichtUndSpeichertErgebnis()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(Heute.AddDays(-1), 272, "  Fährte top  "));

        Assert.True(result.Succeeded);
        var dto = result.Value!;
        Assert.Equal(GoalStatus.Achieved, dto.Status);
        Assert.Equal(Heute.AddDays(-1), dto.ExamDate);
        Assert.Equal(272, dto.ExamScore);
        Assert.Equal("Fährte top", dto.ExamNote);
        var gespeichert = await db.Goals.AsNoTracking().SingleAsync(g => g.Id == s.GoalId);
        Assert.Equal(GoalStatus.Achieved, gespeichert.Status);
        Assert.Equal(272, gespeichert.ExamScore);
    }

    [Fact]
    public async Task Complete_Bestanden_PunkteUndNotizSindOptional()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var dto = (await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(punkte: null, notiz: "   "))).Value!;

        Assert.Equal(GoalStatus.Achieved, dto.Status);
        Assert.Null(dto.ExamScore);
        Assert.Null(dto.ExamNote);
        Assert.Equal(Heute, dto.ExamDate);
    }

    [Fact]
    public async Task Complete_Bestanden_OhnePruefungstag_Scheitert()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, new CompleteGoalRequest(true, null, 100, null, null));

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
        Assert.Equal(GoalStatus.Active, (await db.Goals.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Complete_Bestanden_PruefungstagInDerZukunft_Scheitert_EinTagToleranzGilt()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var zuSpaet = await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(Heute.AddDays(2)));
        Assert.False(zuSpaet.Succeeded);

        // Morgen ist erlaubt: Das Gerät der Person kann der UTC-Zeit voraus sein.
        var morgen = await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(Heute.AddDays(1)));
        Assert.True(morgen.Succeeded);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public async Task Complete_Bestanden_PunkteLiegenZwischenNullUndHoechstpunktzahl(int punkte, bool erlaubt)
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(punkte: punkte));

        Assert.Equal(erlaubt, result.Succeeded);
    }

    [Fact]
    public async Task Complete_Bestanden_OhneHoechstpunktzahl_PruftNurDieUntergrenze()
    {
        var service = MakeService(out var db);
        // Keine Prüfungsordnung: keine Obergrenze bekannt.
        var s = await SetupAsync(db, mitRegulation: false);

        Assert.False((await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(punkte: -5))).Succeeded);
        Assert.True((await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(punkte: 9999))).Succeeded);
    }

    [Fact]
    public async Task Complete_Bestanden_ZuLangeNotiz_Scheitert()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(notiz: new string('x', Dogity.Application.Common.Textlaengen.PruefungsNotiz + 1)));

        Assert.False(result.Succeeded);
    }

    // --- nicht bestanden ---------------------------------------------------

    [Fact]
    public async Task Complete_NichtBestanden_LaesstZielAktivUndVerschiebtDenTermin()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var neuerTermin = Heute.AddDays(42);

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, new CompleteGoalRequest(false, null, null, null, neuerTermin));

        Assert.True(result.Succeeded);
        var dto = result.Value!;
        Assert.Equal(GoalStatus.Active, dto.Status);
        Assert.Equal(neuerTermin, dto.TargetDate);
        // Nicht bestanden ist keine Leistung: nichts vom Ergebnis wird gespeichert.
        Assert.Null(dto.ExamDate);
        Assert.Null(dto.ExamScore);
        Assert.Null(dto.ExamNote);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Complete_NichtBestanden_NeuesDatumIstPflichtUndLiegtInDerZukunft(bool mitDatum)
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var altesDatum = (await db.Goals.AsNoTracking().SingleAsync()).TargetDate;

        // Ohne Datum fehlt es, mit dem heutigen Tag liegt es nicht in der Zukunft.
        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, new CompleteGoalRequest(false, null, null, null, mitDatum ? Heute : null));

        Assert.False(result.Succeeded);
        Assert.Equal(altesDatum, (await db.Goals.AsNoTracking().SingleAsync()).TargetDate);
    }

    [Fact]
    public async Task Complete_NichtBestanden_PlanWirdUmWochenBisZumNeuenTerminErweitert()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db); // angelegt vor 10 Wochen, Plan leer -> Woche 11 läuft
        var uebung = await db.Exercises.FirstAsync();
        db.TrainingPlanItems.Add(new TrainingPlanItem { TrainingPlanId = s.PlanId, WeekNumber = 9, ExerciseId = uebung.Id, RepetitionsTarget = 2 });
        await db.SaveChangesAsync();

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, new CompleteGoalRequest(false, null, null, null, Heute.AddDays(21)));

        // Neuer Termin in Woche 14 (70+21 Tage seit Anlage): die vergangene
        // Woche 10 bleibt leer, geplant werden die Wochen 11 bis 14.
        var wochen = result.Value!.TrainingPlan!.Items.Select(i => i.WeekNumber).Distinct().OrderBy(w => w).ToList();
        Assert.Equal([9, 11, 12, 13, 14], wochen);
    }

    [Fact]
    public async Task Complete_NichtBestanden_IndividuellerPlanBekommtNurDasDatum()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, mitRegulation: false, individuell: true);

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, new CompleteGoalRequest(false, null, null, null, Heute.AddDays(30)));

        Assert.True(result.Succeeded);
        Assert.Equal(Heute.AddDays(30), result.Value!.TargetDate);
        Assert.Empty(result.Value.TrainingPlan!.Items);
    }

    [Fact]
    public async Task Complete_NichtBestanden_TrainerGefuehrterPlanWirdNichtAutomatischErweitert()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var goal = await db.Goals.SingleAsync();
        goal.PlanManagedByTrainerId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, new CompleteGoalRequest(false, null, null, null, Heute.AddDays(30)));

        Assert.True(result.Succeeded);
        Assert.Equal(Heute.AddDays(30), result.Value!.TargetDate);
        Assert.Empty(result.Value.TrainingPlan!.Items);
    }

    // --- Rechte und Zustand ------------------------------------------------

    [Fact]
    public async Task Complete_Fremde_BekommenNichtGefunden_WieBeimStatuswechsel()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var fremde = Guid.NewGuid();

        var komplett = await service.CompleteAsync(fremde, s.GoalId, Bestanden());
        var status = await service.UpdateStatusAsync(fremde, s.GoalId, GoalStatus.Achieved);

        Assert.False(komplett.Succeeded);
        Assert.True(komplett.IsNotFound);
        Assert.Equal(status.IsNotFound, komplett.IsNotFound);
        Assert.Equal(status.Errors, komplett.Errors);
        Assert.Equal(GoalStatus.Active, (await db.Goals.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Complete_Fremde_BekommenAuchBeiFalschenEingabenNurNichtGefunden()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.CompleteAsync(Guid.NewGuid(), s.GoalId, new CompleteGoalRequest(true, null, -4, null, null));

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Complete_MitbesitzerUndBetreuendeTrainerin_Duerfen()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        var mitbesitzer = Guid.NewGuid();
        db.DogOwners.Add(new DogOwner { DogId = s.DogId, UserId = mitbesitzer, Role = DogOwnerRole.Owner });
        var trainerin = Guid.NewGuid();
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerin, MemberId = Guid.NewGuid(), DogId = s.DogId });
        await db.SaveChangesAsync();

        Assert.True((await service.CompleteAsync(trainerin, s.GoalId, new CompleteGoalRequest(false, null, null, null, Heute.AddDays(14)))).Succeeded);
        Assert.True((await service.CompleteAsync(mitbesitzer, s.GoalId, Bestanden())).Succeeded);
    }

    [Theory]
    [InlineData(GoalStatus.Achieved)]
    [InlineData(GoalStatus.Cancelled)]
    public async Task Complete_NurFuerAktiveZiele(GoalStatus status)
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, status: status);

        var result = await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden());

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
        Assert.Equal(status, (await db.Goals.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateStatus_BleibtUnveraendertErreichbar()
    {
        // Offline eingereihte Anfragen älterer App-Stände nutzen ihn weiter.
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var result = await service.UpdateStatusAsync(s.OwnerId, s.GoalId, GoalStatus.Achieved);

        Assert.True(result.Succeeded);
        Assert.Equal(GoalStatus.Achieved, result.Value!.Status);
        Assert.Null(result.Value.ExamDate);
    }

    // --- Ergebnis nachtragen ----------------------------------------------

    [Fact]
    public async Task UpdateExamResult_TraegtErgebnisFuerAlteErreichteZieleNach()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, status: GoalStatus.Achieved); // ohne Ergebnis

        var result = await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(Heute.AddDays(-30), 251, "nachgetragen"));

        Assert.True(result.Succeeded);
        Assert.Equal(GoalStatus.Achieved, result.Value!.Status);
        Assert.Equal(Heute.AddDays(-30), result.Value.ExamDate);
        Assert.Equal(251, result.Value.ExamScore);
        Assert.Equal("nachgetragen", result.Value.ExamNote);
    }

    [Fact]
    public async Task UpdateExamResult_KorrigiertUndLoeschtPunkteUndNotiz()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);
        await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden(Heute.AddDays(-3), 280, "alt"));

        var result = await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(Heute.AddDays(-4), null, null));

        Assert.Equal(Heute.AddDays(-4), result.Value!.ExamDate);
        Assert.Null(result.Value.ExamScore);
        Assert.Null(result.Value.ExamNote);
    }

    [Fact]
    public async Task UpdateExamResult_GleicheRegelnWieBeimAbschliessen()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, status: GoalStatus.Achieved);

        Assert.False((await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(null, 10, null))).Succeeded);
        Assert.False((await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(Heute.AddDays(2), 10, null))).Succeeded);
        Assert.False((await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(Heute, 301, null))).Succeeded);
        Assert.False((await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(Heute, -1, null))).Succeeded);
        Assert.True((await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(Heute, 300, null))).Succeeded);
    }

    [Theory]
    [InlineData(GoalStatus.Active)]
    [InlineData(GoalStatus.Cancelled)]
    public async Task UpdateExamResult_NurFuerErreichteZiele(GoalStatus status)
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, status: status);

        var result = await service.UpdateExamResultAsync(s.OwnerId, s.GoalId, new UpdateExamResultRequest(Heute, 10, null));

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
    }

    [Fact]
    public async Task UpdateExamResult_FremdeBekommenNichtGefundenUndAendernNichts()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, status: GoalStatus.Achieved);

        var result = await service.UpdateExamResultAsync(Guid.NewGuid(), s.GoalId, new UpdateExamResultRequest(Heute, 10, "x"));

        Assert.True(result.IsNotFound);
        Assert.Null((await db.Goals.AsNoTracking().SingleAsync()).ExamDate);
    }

    // --- Höchstpunktzahl und Folgestufen ----------------------------------

    [Fact]
    public async Task MaxPoints_IstDieSummeDerNeuestenFassung()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db);

        var dto = (await service.GetByIdAsync(s.OwnerId, s.GoalId)).Value!;

        // 3 x 100 der neuen Fassung, nicht die 40 der alten.
        Assert.Equal(300, dto.MaxPoints);
        Assert.Equal(300, (await service.GetByDogAsync(s.OwnerId, s.DogId)).Value!.Single().MaxPoints);
    }

    [Fact]
    public async Task MaxPoints_FehltOhnePruefungsordnungUndBeiSummeNull()
    {
        var service = MakeService(out var db);
        var ohne = await SetupAsync(db, mitRegulation: false);
        Assert.Null((await service.GetByIdAsync(ohne.OwnerId, ohne.GoalId)).Value!.MaxPoints);

        var service2 = MakeService(out var db2);
        var nullPunkte = await SetupAsync(db2, punkteJeUebung: 0);
        Assert.Null((await service2.GetByIdAsync(nullPunkte.OwnerId, nullPunkte.GoalId)).Value!.MaxPoints);
    }

    [Fact]
    public async Task NextStages_ErreichtesZielNenntFolgestufenAusDemKatalog()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, regulationName: "BH");
        // Nur zwei der vier Folgestufen gibt es in dieser Datenbank.
        var igp1 = new Regulation { SportId = s.SportId, Name = "FCI-IGP 1" };
        var ibgh1 = new Regulation { SportId = s.SportId, Name = "IBGH1" };
        db.Regulations.AddRange(igp1, ibgh1);
        await db.SaveChangesAsync();

        var offen = (await service.GetByIdAsync(s.OwnerId, s.GoalId)).Value!;
        Assert.Null(offen.NextStages);

        var dto = (await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden())).Value!;

        Assert.NotNull(dto.NextStages);
        Assert.Equal(["FCI-IGP 1", "IBGH1"], dto.NextStages!.Select(n => n.Name).OrderBy(n => n));
        Assert.Contains(dto.NextStages, n => n.RegulationId == igp1.Id && n.SportId == s.SportId);
    }

    [Fact]
    public async Task NextStages_LetzteStufeHatKeine()
    {
        var service = MakeService(out var db);
        var s = await SetupAsync(db, regulationName: "FCI-IGP 3");

        var dto = (await service.CompleteAsync(s.OwnerId, s.GoalId, Bestanden())).Value!;

        Assert.True(dto.NextStages is null or { Count: 0 });
    }
}
