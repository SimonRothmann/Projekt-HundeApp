using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Learning;
using Dogity.Domain.Planning;
using Dogity.Domain.Sports;
using Dogity.Infrastructure.Persistence;

namespace Dogity.Application.Tests.Planning;

/// <summary>
/// Die Prüfungsreife am Ziel: Rechte des Endpunkts, die Kurzfassung an den
/// Ziel-DTOs (gesammelt für alle Ziele), die Theoriezeile nur für die
/// ansehende Person und nie für betreuende Trainer:innen.
/// </summary>
public class GoalServicePruefungsreifeTests
{
    // Mittwoch, 4. November 2026, 10:00 Uhr deutscher Zeit.
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = new(2026, 11, 4);

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    private sealed class Aufbau
    {
        public required ApplicationDbContext Db { get; init; }
        public required GoalService Ziele { get; init; }
        public required Guid OwnerId { get; init; }
        public required Guid CoOwnerId { get; init; }
        public required Guid TrainerId { get; init; }
        public required Guid FremdeId { get; init; }
        public required Dog Hund { get; init; }
        public required Regulation Ordnung { get; init; }
        public required Goal Ziel { get; init; }
        public required List<Exercise> Uebungen { get; init; }
        public required QuizCatalog Erwachsene { get; init; }
        public required QuizCatalog Jugend { get; init; }
    }

    /// <summary>
    /// Eine Prüfungsordnung (Theorie-Katalog gesetzt) mit vier Übungen à 25 Punkten (A bis D, alle Pflicht),
    /// ein Hund mit Besitzer, Mitbesitzerin und betreuender Trainerin, ein laufendes Ziel in 30 Tagen.
    /// Der Hund hat noch nichts trainiert.
    /// </summary>
    private static async Task<Aufbau> AufbauAsync(string? theorieCode = "SWHV-BHVT-ERW", bool mitKatalogen = true)
    {
        var db = InMemoryDbContext.Create();
        var ziele = new GoalService(db, new FesteUhr(Jetzt), new FakeNotificationService(), new ExerciseMasteryService(db));

        var ownerId = Guid.NewGuid();
        var coOwnerId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var sport = new Sport { Name = "Begleithund", Code = "BH" };
        db.Sports.Add(sport);
        var ordnung = new Regulation { SportId = sport.Id, Name = "BH", TheoryCatalogCode = theorieCode };
        db.Regulations.Add(ordnung);
        var fassung = new RegulationVersion { RegulationId = ordnung.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        db.RegulationVersions.Add(fassung);

        var uebungen = new[] { "A", "B", "C", "D" }
            .Select(n => new Exercise { Name = n, SportId = sport.Id, Difficulty = ExerciseDifficulty.Beginner })
            .ToList();
        db.Exercises.AddRange(uebungen);
        for (var i = 0; i < uebungen.Count; i++)
            db.RegulationExercises.Add(new RegulationExercise
            {
                RegulationVersionId = fassung.Id, ExerciseId = uebungen[i].Id, IsMandatory = true, MaxPoints = 25, SortOrder = i,
            });

        var hund = new Dog { Name = "Max", Gender = DogGender.Male };
        db.Dogs.Add(hund);
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = hund.Id, UserId = coOwnerId, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerId, MemberId = ownerId, DogId = hund.Id, StartDate = Heute });

        var ziel = new Goal
        {
            DogId = hund.Id, SportId = sport.Id, RegulationId = ordnung.Id,
            TargetDate = Heute.AddDays(30), Status = GoalStatus.Active, CreatedAt = Jetzt.AddDays(-10),
        };
        db.Goals.Add(ziel);

        var erwachsene = new QuizCatalog { Code = "SWHV-BHVT-ERW", Name = "Erwachsene", Publisher = "swhv", Audience = QuizAudience.Adults, ExamQuestionCount = 25 };
        var jugend = new QuizCatalog { Code = "SWHV-BHVT-JGD", Name = "Jugend", Publisher = "swhv", Audience = QuizAudience.Youth, ExamQuestionCount = 15 };
        if (mitKatalogen) db.QuizCatalogs.AddRange(erwachsene, jugend);

        await db.SaveChangesAsync();

        return new Aufbau
        {
            Db = db, Ziele = ziele, OwnerId = ownerId, CoOwnerId = coOwnerId, TrainerId = trainerId, FremdeId = Guid.NewGuid(),
            Hund = hund, Ordnung = ordnung, Ziel = ziel, Uebungen = uebungen, Erwachsene = erwachsene, Jugend = jugend,
        };
    }

    /// <summary>Hält fest, dass der Hund eine Übung sicher kann.</summary>
    private static void MacheSicher(Aufbau a, Exercise uebung, Dog? hund = null) =>
        a.Db.ExerciseMasteries.Add(new ExerciseMastery
        {
            DogId = (hund ?? a.Hund).Id, ExerciseId = uebung.Id, Box = 5, RecentAvgRating = 4.6, SessionCount = 6,
            LastTrainedAt = new DateTimeOffset(Heute.AddDays(-4).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        });

    private static void MacheWackelig(Aufbau a, Exercise uebung) =>
        a.Db.ExerciseMasteries.Add(new ExerciseMastery
        {
            DogId = a.Hund.Id, ExerciseId = uebung.Id, Box = 2, RecentAvgRating = 2.8, SessionCount = 2,
            LastTrainedAt = new DateTimeOffset(Heute.AddDays(-4).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        });

    private static void Pruefung(Aufbau a, Guid nutzer, QuizCatalog katalog, bool bestanden, int vorTagen) =>
        a.Db.QuizExamAttempts.Add(new QuizExamAttempt
        {
            UserId = nutzer, CatalogId = katalog.Id, Passed = bestanden, Points = bestanden ? 40 : 20, MaxPoints = 50,
            QuestionCount = 25, CorrectCount = bestanden ? 20 : 10, SubmittedAt = Jetzt.AddDays(-vorTagen),
        });

    // ---- Rechte ----

    [Fact]
    public async Task Endpunkt_Besitzer_BekommtDieAuswertung()
    {
        var a = await AufbauAsync();
        MacheSicher(a, a.Uebungen[0]);
        MacheWackelig(a, a.Uebungen[1]);
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id);

        Assert.True(ergebnis.Succeeded);
        var reife = ergebnis.Value!;
        Assert.Equal((1, 1, 2), (reife.Sicher, reife.Wackelig, reife.Offen));
        Assert.Equal(Reifeampel.Rot, reife.Ampel); // Pflichtübungen offen
        Assert.Equal(4, reife.Uebungen.Count);
        Assert.Equal(30, reife.Tage);
        Assert.Equal(5, reife.Wochen);
    }

    [Fact]
    public async Task Endpunkt_Mitbesitzerin_DarfAuch()
    {
        var a = await AufbauAsync();

        Assert.True((await a.Ziele.GetPruefungsreifeAsync(a.CoOwnerId, a.Ziel.Id)).Succeeded);
    }

    [Fact]
    public async Task Endpunkt_BetreuendeTrainerin_DarfUndSiehtDieUebungen()
    {
        var a = await AufbauAsync();
        MacheSicher(a, a.Uebungen[0]);
        await a.Db.SaveChangesAsync();

        var ergebnis = await a.Ziele.GetPruefungsreifeAsync(a.TrainerId, a.Ziel.Id);

        Assert.True(ergebnis.Succeeded);
        Assert.Equal(1, ergebnis.Value!.Sicher);
    }

    [Fact]
    public async Task Endpunkt_Fremde_BekommenNichtGefunden()
    {
        var a = await AufbauAsync();

        var ergebnis = await a.Ziele.GetPruefungsreifeAsync(a.FremdeId, a.Ziel.Id);

        Assert.False(ergebnis.Succeeded);
        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task Endpunkt_UnbekanntesZiel_NichtGefunden()
    {
        var a = await AufbauAsync();

        Assert.True((await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, Guid.NewGuid())).IsNotFound);
    }

    [Fact]
    public async Task Endpunkt_EigenesZielErreichtesZielZielOhneOrdnung_KeinePruefungsreife()
    {
        var a = await AufbauAsync();
        var eigenes = new Goal { DogId = a.Hund.Id, SportId = a.Ziel.SportId, IsCustom = true, TargetDate = Heute.AddDays(20), Status = GoalStatus.Active };
        var erreicht = new Goal { DogId = a.Hund.Id, SportId = a.Ziel.SportId, RegulationId = a.Ordnung.Id, TargetDate = Heute.AddDays(-3), Status = GoalStatus.Achieved };
        var ohneOrdnung = new Goal { DogId = a.Hund.Id, SportId = a.Ziel.SportId, TargetDate = Heute.AddDays(20), Status = GoalStatus.Active };
        a.Db.Goals.AddRange(eigenes, erreicht, ohneOrdnung);
        await a.Db.SaveChangesAsync();

        foreach (var ziel in new[] { eigenes, erreicht, ohneOrdnung })
            Assert.True((await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, ziel.Id)).IsNotFound);

        var dtos = (await a.Ziele.GetByDogAsync(a.OwnerId, a.Hund.Id)).Value!;
        Assert.All(dtos.Where(d => d.Id != a.Ziel.Id), d => Assert.Null(d.Pruefungsreife));
    }

    // ---- Kurzfassung an den Ziel-DTOs ----

    [Fact]
    public async Task Zieldto_TraegtDieKurzfassung_UndStimmtMitDemEndpunktUeberein()
    {
        var a = await AufbauAsync();
        MacheSicher(a, a.Uebungen[0]);
        MacheSicher(a, a.Uebungen[1]);
        MacheSicher(a, a.Uebungen[2]);
        MacheWackelig(a, a.Uebungen[3]);
        await a.Db.SaveChangesAsync();
        // Theorie sicher, damit die Ampel für Besitzerin grün sein kann.
        Pruefung(a, a.OwnerId, a.Erwachsene, true, 3);
        Pruefung(a, a.OwnerId, a.Erwachsene, true, 8);
        await a.Db.SaveChangesAsync();

        var kurz = (await a.Ziele.GetByIdAsync(a.OwnerId, a.Ziel.Id)).Value!.Pruefungsreife!;
        var voll = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;

        Assert.Equal(Reifeampel.Gelb, kurz.Ampel); // 75 % sicher
        Assert.Equal((voll.Ampel, voll.Offen, voll.Wackelig, voll.Sicher), (kurz.Ampel, kurz.Offen, kurz.Wackelig, kurz.Sicher));
    }

    [Fact]
    public async Task Hundeliste_RechnetAlleZieleGesammelt_JedesMitSeinerOrdnung()
    {
        var a = await AufbauAsync();
        // Zweites Ziel mit eigener Ordnung (zwei Übungen, beide sicher) für denselben Hund, plus ein zweiter Hund mit Ziel auf der ersten.
        var sport = a.Db.Sports.Single();
        var zweite = new Regulation { SportId = sport.Id, Name = "BH-2" };
        a.Db.Regulations.Add(zweite);
        var fassung = new RegulationVersion { RegulationId = zweite.Id, VersionLabel = "2025", ValidFrom = new DateOnly(2025, 1, 1) };
        a.Db.RegulationVersions.Add(fassung);
        for (var i = 0; i < 2; i++)
            a.Db.RegulationExercises.Add(new RegulationExercise
            {
                RegulationVersionId = fassung.Id, ExerciseId = a.Uebungen[i].Id, IsMandatory = true, MaxPoints = 10, SortOrder = i,
            });
        a.Db.Goals.Add(new Goal
        {
            DogId = a.Hund.Id, SportId = sport.Id, RegulationId = zweite.Id, TargetDate = Heute.AddDays(40),
            Status = GoalStatus.Active, CreatedAt = Jetzt.AddDays(-5),
        });
        MacheSicher(a, a.Uebungen[0]);
        MacheSicher(a, a.Uebungen[1]);
        await a.Db.SaveChangesAsync();

        var dtos = (await a.Ziele.GetByDogAsync(a.OwnerId, a.Hund.Id)).Value!;

        Assert.Equal(2, dtos.Count);
        var erste = dtos.Single(d => d.Id == a.Ziel.Id).Pruefungsreife!;
        var andere = dtos.Single(d => d.Id != a.Ziel.Id).Pruefungsreife!;
        Assert.Equal((2, 2), (erste.Sicher, erste.Offen));   // vier Übungen
        Assert.Equal((2, 0), (andere.Sicher, andere.Offen)); // zwei Übungen
        Assert.Equal(Reifeampel.Gruen, andere.Ampel);
    }

    [Fact]
    public async Task Lader_MehrereHundeMitEigenerMastery_RechnetJedenFuerSich()
    {
        var a = await AufbauAsync();
        var zweiterHund = new Dog { Name = "Bello", Gender = DogGender.Male };
        a.Db.Dogs.Add(zweiterHund);
        a.Db.DogOwners.Add(new DogOwner { DogId = zweiterHund.Id, UserId = a.OwnerId, Role = DogOwnerRole.Owner });
        var ziel2 = new Goal
        {
            DogId = zweiterHund.Id, SportId = a.Ziel.SportId, RegulationId = a.Ordnung.Id, TargetDate = Heute.AddDays(14),
            Status = GoalStatus.Active, CreatedAt = Jetzt.AddDays(-5),
        };
        a.Db.Goals.Add(ziel2);
        foreach (var u in a.Uebungen) MacheSicher(a, u, zweiterHund);
        await a.Db.SaveChangesAsync();

        var ergebnis = await PruefungsreifeLader.LadeAsync(
            a.Db, [a.Ziel, ziel2], Heute, new Dictionary<Guid, List<PausenZeitraum>>(), a.OwnerId);

        Assert.Equal(Reifeampel.Rot, ergebnis[a.Ziel.Id].Ampel);   // Max: nichts trainiert
        Assert.Equal(Reifeampel.Gelb, ergebnis[ziel2.Id].Ampel);   // Bello: alles sicher, aber Theorie offen
        Assert.Equal(4, ergebnis[ziel2.Id].Sicher);
    }

    // ---- Theorie ----

    [Fact]
    public async Task Theorie_Besitzerin_NochKeinePruefung_IstOffen()
    {
        var a = await AufbauAsync();

        var theorie = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!.Theorie;

        Assert.NotNull(theorie);
        Assert.Equal(ReifeZustand.Offen, theorie!.Zustand);
        Assert.Equal("SWHV-BHVT-ERW", theorie.CatalogCode);
        Assert.Equal(0, theorie.Gewertet);
    }

    [Fact]
    public async Task Theorie_EineBestandeneUndEineNicht_IstWackelig()
    {
        var a = await AufbauAsync();
        Pruefung(a, a.OwnerId, a.Erwachsene, false, 10);
        Pruefung(a, a.OwnerId, a.Erwachsene, true, 3);
        await a.Db.SaveChangesAsync();

        var theorie = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!.Theorie!;

        Assert.Equal(ReifeZustand.Wackelig, theorie.Zustand);
        Assert.Equal((2, 1), (theorie.Gewertet, theorie.Bestanden));
    }

    [Fact]
    public async Task Theorie_ZweiBestandene_IstSicher_UndLetzteGeltenNur()
    {
        var a = await AufbauAsync();
        Pruefung(a, a.OwnerId, a.Erwachsene, false, 20); // älter als die letzten beiden: zählt nicht
        Pruefung(a, a.OwnerId, a.Erwachsene, true, 8);
        Pruefung(a, a.OwnerId, a.Erwachsene, true, 2);
        await a.Db.SaveChangesAsync();

        var theorie = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!.Theorie!;

        Assert.Equal(ReifeZustand.Sicher, theorie.Zustand);
    }

    [Fact]
    public async Task Theorie_JugendkatalogZaehltMit_UndDerLinkFuehrtDorthin()
    {
        var a = await AufbauAsync();
        Pruefung(a, a.OwnerId, a.Erwachsene, false, 30);
        Pruefung(a, a.OwnerId, a.Jugend, true, 10);
        Pruefung(a, a.OwnerId, a.Jugend, true, 2);
        await a.Db.SaveChangesAsync();

        var theorie = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!.Theorie!;

        Assert.Equal(ReifeZustand.Sicher, theorie.Zustand);
        Assert.Equal("SWHV-BHVT-JGD", theorie.CatalogCode);
    }

    [Fact]
    public async Task Theorie_ZeigtDenFehlerspeicher()
    {
        var a = await AufbauAsync();
        var frage = new QuizQuestion { CatalogId = a.Erwachsene.Id, Number = "A1", Section = "A", Text = "?", Kind = QuizQuestionKind.SingleChoice };
        a.Db.QuizQuestions.Add(frage);
        a.Db.QuizMasteries.Add(new QuizMastery
        {
            UserId = a.OwnerId, QuestionId = frage.Id, LastAnsweredAt = Jetzt, LastWasCorrect = false,
        });
        await a.Db.SaveChangesAsync();

        var theorie = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!.Theorie!;

        Assert.Equal(1, theorie.InFehlerspeicher);
    }

    [Fact]
    public async Task Theorie_Mitbesitzerin_SiehtIhreEigenenErgebnisse_NichtDieDesBesitzers()
    {
        var a = await AufbauAsync();
        Pruefung(a, a.OwnerId, a.Erwachsene, true, 8);
        Pruefung(a, a.OwnerId, a.Erwachsene, true, 2);
        await a.Db.SaveChangesAsync();

        var besitzer = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!.Theorie!;
        var mitbesitzerin = (await a.Ziele.GetPruefungsreifeAsync(a.CoOwnerId, a.Ziel.Id)).Value!.Theorie!;

        Assert.Equal(ReifeZustand.Sicher, besitzer.Zustand);
        Assert.Equal(ReifeZustand.Offen, mitbesitzerin.Zustand);
    }

    [Fact]
    public async Task Theorie_BetreuendeTrainerin_BekommtSieNicht_AuchNichtWennSieSelbstGeuebtHat()
    {
        var a = await AufbauAsync();
        // Die Trainerin hat selbst Übungsprüfungen gemacht - der Lernstand gehört ihr, nicht der Ansicht des fremden Hundes.
        Pruefung(a, a.TrainerId, a.Erwachsene, true, 8);
        Pruefung(a, a.TrainerId, a.Erwachsene, true, 2);
        await a.Db.SaveChangesAsync();

        var reife = (await a.Ziele.GetPruefungsreifeAsync(a.TrainerId, a.Ziel.Id)).Value!;

        Assert.Null(reife.Theorie);
    }

    [Fact]
    public async Task Theorie_ZaehltFuerDieTrainerAmpelNicht()
    {
        var a = await AufbauAsync();
        foreach (var u in a.Uebungen) MacheSicher(a, u);
        await a.Db.SaveChangesAsync();

        var besitzer = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;
        var trainerin = (await a.Ziele.GetPruefungsreifeAsync(a.TrainerId, a.Ziel.Id)).Value!;

        // Alles sicher: Die Trainerin sieht grün; der Besitzer wartet noch auf seine Theorie.
        Assert.Equal(Reifeampel.Gruen, trainerin.Ampel);
        Assert.Equal(Reifeampel.Gelb, besitzer.Ampel);
    }

    [Fact]
    public async Task Theorie_AuchInDerKurzfassungDerTrainerinNicht()
    {
        var a = await AufbauAsync();
        foreach (var u in a.Uebungen) MacheSicher(a, u);
        await a.Db.SaveChangesAsync();

        var fuerTrainerin = (await a.Ziele.GetByDogAsync(a.TrainerId, a.Hund.Id)).Value!.Single().Pruefungsreife!;
        var fuerBesitzer = (await a.Ziele.GetByDogAsync(a.OwnerId, a.Hund.Id)).Value!.Single().Pruefungsreife!;

        Assert.Equal(Reifeampel.Gruen, fuerTrainerin.Ampel);
        Assert.Equal(Reifeampel.Gelb, fuerBesitzer.Ampel);
    }

    [Fact]
    public async Task Theorie_OrdnungOhneKatalogCode_KeineTheoriezeile()
    {
        var a = await AufbauAsync(theorieCode: null);

        var reife = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;

        Assert.Null(reife.Theorie);
    }

    [Fact]
    public async Task Theorie_KatalogFehltInDieserInstanz_KeineTheoriezeile_UndNichtsBlockiertGruen()
    {
        var a = await AufbauAsync(mitKatalogen: false);
        foreach (var u in a.Uebungen) MacheSicher(a, u);
        await a.Db.SaveChangesAsync();

        var reife = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;

        Assert.Null(reife.Theorie);
        Assert.Equal(Reifeampel.Gruen, reife.Ampel);
    }

    [Fact]
    public async Task Theorie_SachkundeModulAbgeschaltet_KeineTheoriezeile_UndNichtsBlockiertGruen()
    {
        var a = await AufbauAsync();
        foreach (var u in a.Uebungen) MacheSicher(a, u);
        var einstellung = new Dogity.Domain.Preferences.UserPreference { UserId = a.OwnerId };
        a.Db.UserPreferences.Add(einstellung);
        a.Db.UserDisabledModules.Add(new Dogity.Domain.Preferences.UserDisabledModule
        {
            UserPreferenceId = einstellung.Id, ModuleKey = Dogity.Application.Preferences.Modules.Sachkunde,
        });
        await a.Db.SaveChangesAsync();

        var reife = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;

        // Wer die Sachkunde abgewählt hat, kann keine Übungsprüfung ablegen - sie darf auch nicht gefordert werden.
        Assert.Null(reife.Theorie);
        Assert.Equal(Reifeampel.Gruen, reife.Ampel);
    }

    [Theory]
    [InlineData("AT", false)]
    [InlineData("DE", true)]
    [InlineData(null, true)]
    public async Task Theorie_NurImDeutschenGeltungsbereich(string? land, bool erwartetTheorie)
    {
        var a = await AufbauAsync();
        foreach (var u in a.Uebungen) MacheSicher(a, u);
        a.Db.UserPreferences.Add(new Dogity.Domain.Preferences.UserPreference { UserId = a.OwnerId, Country = land });
        await a.Db.SaveChangesAsync();

        var reife = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;

        Assert.Equal(erwartetTheorie, reife.Theorie is not null);
        Assert.Equal(erwartetTheorie ? Reifeampel.Gelb : Reifeampel.Gruen, reife.Ampel);
    }

    // ---- Pause ----

    [Fact]
    public async Task Pause_Heute_ZeigtDieAmpelTrotzdem_UndMeldetDiePause()
    {
        var a = await AufbauAsync();
        MacheSicher(a, a.Uebungen[0]);
        a.Db.DogPauses.Add(new DogPause { DogId = a.Hund.Id, Reason = DogPauseReason.Injured, StartDate = Heute.AddDays(-2), EndDate = Heute.AddDays(5) });
        await a.Db.SaveChangesAsync();

        var reife = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;

        Assert.True(reife.Pausiert);
        Assert.Equal(1, reife.Sicher);
        Assert.Equal(6, reife.PausenTage); // heute bis einschließlich Tag +5
    }

    [Fact]
    public async Task Rot_UndWenigZeit_LoestDenVerschiebenHinweisAus()
    {
        var a = await AufbauAsync();
        a.Ziel.TargetDate = Heute.AddDays(14);
        await a.Db.SaveChangesAsync();

        var reife = (await a.Ziele.GetPruefungsreifeAsync(a.OwnerId, a.Ziel.Id)).Value!;

        Assert.Equal(Reifeampel.Rot, reife.Ampel);
        Assert.True(reife.ZieldatumVerschieben);
        // Auch die Zielkarte weiß es, ohne die Auswertung zu laden.
        Assert.True((await a.Ziele.GetByIdAsync(a.OwnerId, a.Ziel.Id)).Value!.Pruefungsreife!.ZieldatumVerschieben);
    }
}
