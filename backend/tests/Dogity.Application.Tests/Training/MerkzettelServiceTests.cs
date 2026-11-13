using Dogity.Application.Common;
using Dogity.Application.Planning;
using Dogity.Application.Tests.TestSupport;
using Dogity.Application.Training;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Domain.Sports;
using Dogity.Domain.Training;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Training;

/// <summary>
/// Der Merkzettel: wer ihn sehen und ändern darf (Besitzer:in und Mitbesitzer:in ja, betreuende Trainer:in und Fremde
/// "nicht gefunden"), die Eingabe-Regeln, die Offline-Id als Idempotenz-Schlüssel, der Rhythmus des Hundes und das
/// Abhaken beim Eintragen eines Trainings.
/// </summary>
public class MerkzettelServiceTests
{
    // Mittwoch, 4. November 2026, 10:00 Uhr deutscher Zeit (09:00 UTC).
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = new(2026, 11, 4);

    private sealed class FesteUhr(DateTimeOffset jetzt) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => jetzt;
    }

    private sealed record Aufbau(
        ApplicationDbContext Db,
        MerkzettelService Merkzettel,
        TrainingService Trainings,
        Guid OwnerId,
        Guid CoOwnerId,
        Guid TrainerId,
        Guid FremdeId,
        Guid DogId,
        Guid BelloId,
        Guid UebungId);

    /// <summary>Hündin "Flitzer" mit Besitzerin, Mitbesitzer und betreuender Trainerin; Rüde "Bello" der Besitzerin; eine Übung "Sitz"; eine fremde Person.</summary>
    private static async Task<Aufbau> AufbauAsync()
    {
        var db = InMemoryDbContext.Create();
        var uhr = new FesteUhr(Jetzt);
        var ownerId = Guid.NewGuid();
        var coOwnerId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();

        var flitzer = new Dog { Name = "Flitzer", Gender = DogGender.Female };
        var bello = new Dog { Name = "Bello", Gender = DogGender.Male };
        var sport = new Sport { Code = "TEST", Name = "Testsport" };
        var uebung = new Exercise { SportId = sport.Id, Name = "Sitz", NameEn = "Sit" };
        db.Dogs.AddRange(flitzer, bello);
        db.Sports.Add(sport);
        db.Exercises.Add(uebung);
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = coOwnerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = bello.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerId, MemberId = ownerId, DogId = flitzer.Id, StartDate = Heute });
        await db.SaveChangesAsync();

        return new Aufbau(
            db,
            new MerkzettelService(db, uhr),
            new TrainingService(db, new FakeNotificationService(), new FakeUserLookupService(), new ExerciseMasteryService(db), new FakeWeatherEnrichmentService(), uhr),
            ownerId, coOwnerId, trainerId, Guid.NewGuid(), flitzer.Id, bello.Id, uebung.Id);
    }

    private static SaveTrainingNoteRequest Notiz(
        string text = "Fuß rückwärts", TrainingNoteGoal ziel = TrainingNoteGoal.NextTraining, DateOnly? datum = null,
        Guid? uebung = null, Guid? id = null) =>
        new(text, uebung, ziel, datum, id);

    private static async Task TagebuchAsync(Aufbau a, params (DateOnly Datum, TimeOnly? Zeit)[] tage)
    {
        foreach (var (datum, zeit) in tage)
            a.Db.TrainingSessions.Add(new TrainingSession { UserId = a.OwnerId, DogId = a.DogId, Date = datum, StartTime = zeit, DurationMinutes = 30 });
        await a.Db.SaveChangesAsync();
    }

    // --- Rechte ---------------------------------------------------------------

    [Fact]
    public async Task Besitzerin_UndMitbesitzer_DuerfenMerkenLesenAendernLoeschen_AuchEinanders()
    {
        var a = await AufbauAsync();

        var von1 = await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("Von ihr"));
        var von2 = await a.Merkzettel.CreateAsync(a.CoOwnerId, a.DogId, Notiz("Von ihm"));
        Assert.True(von1.Succeeded);
        Assert.True(von2.Succeeded);

        // Gemeinsamer Hund, gemeinsamer Merkzettel: beide sehen beide Einträge.
        foreach (var person in new[] { a.OwnerId, a.CoOwnerId })
            Assert.Equal(["Von ihr", "Von ihm"], (await a.Merkzettel.GetAsync(person, a.DogId)).Value!.Select(n => n.Text));

        Assert.True((await a.Merkzettel.UpdateAsync(a.CoOwnerId, a.DogId, von1.Value!.Id, Notiz("Geändert"))).Succeeded);
        Assert.True((await a.Merkzettel.SetDoneAsync(a.OwnerId, a.DogId, von2.Value!.Id, done: true)).Succeeded);
        Assert.True((await a.Merkzettel.DeleteAsync(a.CoOwnerId, a.DogId, von1.Value.Id)).Succeeded);
        Assert.Empty((await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!);
    }

    [Fact]
    public async Task Merkzettel_VerfasserinSteht_AmEintrag()
    {
        var a = await AufbauAsync();

        var neu = await a.Merkzettel.CreateAsync(a.CoOwnerId, a.DogId, Notiz());

        Assert.Equal(a.CoOwnerId, (await a.Db.TrainingNotes.SingleAsync(n => n.Id == neu.Value!.Id)).UserId);
    }

    [Fact]
    public async Task Trainerin_BekommtAllesNichtGefunden_AuchBeimLesen()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz())).Value!;

        Assert.True((await a.Merkzettel.GetAsync(a.TrainerId, a.DogId)).IsNotFound);
        Assert.True((await a.Merkzettel.CreateAsync(a.TrainerId, a.DogId, Notiz())).IsNotFound);
        Assert.True((await a.Merkzettel.UpdateAsync(a.TrainerId, a.DogId, eintrag.Id, Notiz("x"))).IsNotFound);
        Assert.True((await a.Merkzettel.SetDoneAsync(a.TrainerId, a.DogId, eintrag.Id, true)).IsNotFound);
        Assert.True((await a.Merkzettel.DeleteAsync(a.TrainerId, a.DogId, eintrag.Id)).IsNotFound);
        Assert.True((await a.Merkzettel.GetNextTrainingAsync(a.TrainerId, a.DogId)).IsNotFound);
        Assert.True((await a.Merkzettel.SetRhythmAsync(a.TrainerId, a.DogId, new SaveTrainingRhythmRequest(4, 18))).IsNotFound);

        Assert.Equal("Fuß rückwärts", (await a.Db.TrainingNotes.SingleAsync()).Text);
    }

    [Fact]
    public async Task Fremde_BekommenAllesNichtGefunden()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz())).Value!;

        Assert.True((await a.Merkzettel.GetAsync(a.FremdeId, a.DogId)).IsNotFound);
        Assert.True((await a.Merkzettel.CreateAsync(a.FremdeId, a.DogId, Notiz())).IsNotFound);
        Assert.True((await a.Merkzettel.UpdateAsync(a.FremdeId, a.DogId, eintrag.Id, Notiz("x"))).IsNotFound);
        Assert.True((await a.Merkzettel.SetDoneAsync(a.FremdeId, a.DogId, eintrag.Id, true)).IsNotFound);
        Assert.True((await a.Merkzettel.DeleteAsync(a.FremdeId, a.DogId, eintrag.Id)).IsNotFound);
        Assert.True((await a.Merkzettel.GetNextTrainingAsync(a.FremdeId, a.DogId)).IsNotFound);
    }

    [Fact]
    public async Task EintragEinesAnderenHundes_LaesstSichUeberDenEigenenHundNichtAendern()
    {
        var a = await AufbauAsync();
        var vonFlitzer = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("Flitzer"))).Value!;

        // Die Besitzerin besitzt Bello auch - die Kennung gehört aber zu Flitzer.
        Assert.True((await a.Merkzettel.UpdateAsync(a.OwnerId, a.BelloId, vonFlitzer.Id, Notiz("x"))).IsNotFound);
        Assert.True((await a.Merkzettel.DeleteAsync(a.OwnerId, a.BelloId, vonFlitzer.Id)).IsNotFound);
        Assert.True((await a.Merkzettel.SetDoneAsync(a.OwnerId, a.BelloId, vonFlitzer.Id, true)).IsNotFound);
    }

    [Fact]
    public async Task GeloeschterHund_ZeigtKeinenMerkzettelMehr()
    {
        var a = await AufbauAsync();
        await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz());

        (await a.Db.Dogs.FirstAsync(d => d.Id == a.DogId)).DeletedAt = Jetzt;
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).IsNotFound);
        Assert.Empty(await a.Db.TrainingNotes.ToListAsync()); // der Filter am Hund blendet den Eintrag mit aus
    }

    // --- Eingabe ---------------------------------------------------------------

    [Fact]
    public async Task Create_TextWirdGetrimmt_Pflicht_UndLaengenGrenze()
    {
        var a = await AufbauAsync();

        var ok = await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("  Fuß  "));
        Assert.Equal("Fuß", ok.Value!.Text);

        Assert.False((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("   "))).Succeeded);
        Assert.True((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(new string('x', Textlaengen.MerkzettelText)))).Succeeded);
        var zuLang = await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(new string('x', Textlaengen.MerkzettelText + 1)));
        Assert.False(zuLang.Succeeded);
        Assert.False(zuLang.IsNotFound);
    }

    [Fact]
    public async Task Create_UngueltigesZiel_DatumOhneTag_FernesDatum_UnbekannteUebung_Abgelehnt()
    {
        var a = await AufbauAsync();

        Assert.False((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(ziel: (TrainingNoteGoal)42))).Succeeded);
        Assert.False((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(ziel: TrainingNoteGoal.Date))).Succeeded);
        Assert.False((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(ziel: TrainingNoteGoal.Date, datum: Heute.AddDays(400)))).Succeeded);
        Assert.False((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(ziel: TrainingNoteGoal.Date, datum: Heute.AddDays(-30)))).Succeeded);
        Assert.False((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(uebung: Guid.NewGuid()))).Succeeded);
        Assert.Empty(await a.Db.TrainingNotes.ToListAsync());
    }

    [Fact]
    public async Task Create_ZielDatum_NaechsteWocheIstDerMontagDesServers_NaechstesTrainingHatKeinDatum()
    {
        var a = await AufbauAsync();

        var datum = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(ziel: TrainingNoteGoal.Date, datum: Heute.AddDays(3)))).Value!;
        // Das Gerät schickt ein beliebiges Datum mit: Bei "Nächste Woche" gilt der Montag nach Vereinszeit.
        var woche = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(ziel: TrainingNoteGoal.NextWeek, datum: Heute))).Value!;
        var naechstes = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(datum: Heute.AddDays(2)))).Value!;

        var zeilen = await a.Db.TrainingNotes.ToDictionaryAsync(n => n.Id);
        Assert.Equal(Heute.AddDays(3), zeilen[datum.Id].TargetDate);
        Assert.Equal(new DateOnly(2026, 11, 9), zeilen[woche.Id].TargetDate);
        Assert.Null(zeilen[naechstes.Id].TargetDate);
    }

    [Fact]
    public async Task Create_MitUebung_LiefertDenNamenAufDeutschUndEnglisch()
    {
        var a = await AufbauAsync();

        var neu = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(uebung: a.UebungId))).Value!;

        Assert.Equal((a.UebungId, "Sitz", "Sit"), (neu.ExerciseId, neu.ExerciseName, neu.ExerciseNameEn));
        Assert.Equal("Sitz", (await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!.Single().ExerciseName);
    }

    [Fact]
    public async Task Update_AendertTextUebungUndZiel()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("Alt"))).Value!;

        var neu = (await a.Merkzettel.UpdateAsync(a.OwnerId, a.DogId, eintrag.Id,
            Notiz("Neu", TrainingNoteGoal.Date, Heute.AddDays(5), a.UebungId))).Value!;

        Assert.Equal(("Neu", TrainingNoteGoal.Date, Heute.AddDays(5), a.UebungId), (neu.Text, neu.Goal, neu.TargetDate, neu.ExerciseId));
        Assert.Single(await a.Db.TrainingNotes.ToListAsync());
    }

    [Fact]
    public async Task Update_NaechsteWoche_BehaeltIhrenMontag_NurEinNeuesZielSetztIhnNeu()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(ziel: TrainingNoteGoal.NextWeek))).Value!;
        // Vor einer Woche gemerkt: Der Montag war der 2.11.
        var zeile = await a.Db.TrainingNotes.SingleAsync(n => n.Id == eintrag.Id);
        zeile.TargetDate = new DateOnly(2026, 11, 2);
        await a.Db.SaveChangesAsync();

        await a.Merkzettel.UpdateAsync(a.OwnerId, a.DogId, eintrag.Id, Notiz("Nur der Text", TrainingNoteGoal.NextWeek));
        Assert.Equal(new DateOnly(2026, 11, 2), (await a.Db.TrainingNotes.SingleAsync()).TargetDate);

        // Erst über ein anderes Ziel und zurück wird es wieder die kommende Woche.
        await a.Merkzettel.UpdateAsync(a.OwnerId, a.DogId, eintrag.Id, Notiz("Nur der Text"));
        await a.Merkzettel.UpdateAsync(a.OwnerId, a.DogId, eintrag.Id, Notiz("Nur der Text", TrainingNoteGoal.NextWeek));
        Assert.Equal(new DateOnly(2026, 11, 9), (await a.Db.TrainingNotes.SingleAsync()).TargetDate);
    }

    // --- Offline: idempotente Id -----------------------------------------------

    [Fact]
    public async Task Create_MitGeraeteId_IstIdempotent_AuchNachLoeschen()
    {
        var a = await AufbauAsync();
        var id = Guid.NewGuid();

        var erste = await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("Einmal", id: id));
        var zweite = await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("Einmal", id: id));
        Assert.Equal(id, erste.Value!.Id);
        Assert.Equal(id, zweite.Value!.Id);
        Assert.Single(await a.Db.TrainingNotes.ToListAsync());

        // Gelöscht, dann wiederholt die Warteschlange die alte Anfrage: kein zweiter Eintrag, kein Schlüsselfehler.
        await a.Merkzettel.DeleteAsync(a.OwnerId, a.DogId, id);
        Assert.True((await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz("Einmal", id: id))).Succeeded);
        Assert.Single(await a.Db.TrainingNotes.IgnoreQueryFilters().ToListAsync());
        Assert.Empty((await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!);
    }

    [Fact]
    public async Task Create_GeraeteIdEinesAnderenHundes_NichtGefunden()
    {
        var a = await AufbauAsync();
        var id = Guid.NewGuid();
        await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz(id: id));

        Assert.True((await a.Merkzettel.CreateAsync(a.OwnerId, a.BelloId, Notiz(id: id))).IsNotFound);
        Assert.Single(await a.Db.TrainingNotes.ToListAsync());
    }

    // --- Abhaken, Liste ---------------------------------------------------------

    [Fact]
    public async Task Abhaken_NimmtDenEintragAusDerListe_RueckgaengigBringtIhnZurueck()
    {
        var a = await AufbauAsync();
        var eintrag = (await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz())).Value!;

        var erledigt = (await a.Merkzettel.SetDoneAsync(a.OwnerId, a.DogId, eintrag.Id, done: true)).Value!;
        Assert.Equal(Jetzt, erledigt.DoneAt);
        Assert.Null(erledigt.TargetDate); // ein erledigter Eintrag hat kein Ziel mehr
        Assert.Empty((await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!);

        Assert.Null((await a.Merkzettel.SetDoneAsync(a.OwnerId, a.DogId, eintrag.Id, done: false)).Value!.DoneAt);
        Assert.Single((await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!);
    }

    [Fact]
    public async Task Liste_NurDieDesHundes_AelteresZuerst()
    {
        var a = await AufbauAsync();
        a.Db.TrainingNotes.AddRange(
            new TrainingNote { DogId = a.DogId, UserId = a.OwnerId, Text = "Neuer", CreatedAt = Jetzt.AddHours(-1) },
            new TrainingNote { DogId = a.DogId, UserId = a.OwnerId, Text = "Älter", CreatedAt = Jetzt.AddDays(-1) },
            new TrainingNote { DogId = a.BelloId, UserId = a.OwnerId, Text = "Bello", CreatedAt = Jetzt.AddDays(-1) });
        await a.Db.SaveChangesAsync();

        Assert.Equal(["Älter", "Neuer"], (await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!.Select(n => n.Text));
    }

    [Fact]
    public async Task Liste_ZielTagDesNaechstenTrainings_KommtAusDemRhythmus_UnbekanntOhne()
    {
        var a = await AufbauAsync();
        await a.Merkzettel.CreateAsync(a.OwnerId, a.DogId, Notiz());
        Assert.Null((await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!.Single().TargetDate);

        // Dienstage 20.10. und 27.10. um 18:00 -> Dienstag, 10.11., 18:00.
        await TagebuchAsync(a, (new DateOnly(2026, 10, 20), new TimeOnly(18, 0)), (new DateOnly(2026, 10, 27), new TimeOnly(18, 0)));

        var dto = (await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!.Single();
        Assert.Equal((new DateOnly(2026, 11, 10), new TimeOnly(18, 0)), (dto.TargetDate!.Value, dto.TargetTime!.Value));
    }

    // --- Nächstes Training und Rhythmus --------------------------------------------

    [Fact]
    public async Task NaechstesTraining_Unbekannt_OhneAlles()
    {
        var a = await AufbauAsync();

        var ergebnis = (await a.Merkzettel.GetNextTrainingAsync(a.OwnerId, a.DogId)).Value!;

        Assert.Equal(new NextTrainingDto(null, null, TrainingsQuelle.Unbekannt), ergebnis);
    }

    [Fact]
    public async Task NaechstesTraining_AusDemTagebuch_MitMedianUhrzeit()
    {
        var a = await AufbauAsync();
        await TagebuchAsync(a, (new DateOnly(2026, 10, 20), new TimeOnly(18, 0)), (new DateOnly(2026, 10, 27), new TimeOnly(19, 0)));

        var ergebnis = (await a.Merkzettel.GetNextTrainingAsync(a.OwnerId, a.DogId)).Value!;

        Assert.Equal(new NextTrainingDto(new DateOnly(2026, 11, 10), new TimeOnly(18, 30), TrainingsQuelle.Tagebuch), ergebnis);
    }

    [Fact]
    public async Task NaechstesTraining_AuchTrainingenDesMitbesitzersZaehlenFuerDenHund()
    {
        var a = await AufbauAsync();
        a.Db.TrainingSessions.AddRange(
            new TrainingSession { UserId = a.OwnerId, DogId = a.DogId, Date = new DateOnly(2026, 10, 20), DurationMinutes = 30 },
            new TrainingSession { UserId = a.CoOwnerId, DogId = a.DogId, Date = new DateOnly(2026, 10, 27), DurationMinutes = 30 });
        await a.Db.SaveChangesAsync();

        var ergebnis = (await a.Merkzettel.GetNextTrainingAsync(a.CoOwnerId, a.DogId)).Value!;

        Assert.Equal(TrainingsQuelle.Tagebuch, ergebnis.Source);
    }

    [Fact]
    public async Task Rhythmus_Speichern_GiltVorDemTagebuch_FuerAlleBesitzer_UndErsetztDenAlten()
    {
        var a = await AufbauAsync();
        await TagebuchAsync(a, (new DateOnly(2026, 10, 20), new TimeOnly(18, 0)), (new DateOnly(2026, 10, 27), new TimeOnly(18, 0)));

        var gespeichert = await a.Merkzettel.SetRhythmAsync(a.OwnerId, a.DogId, new SaveTrainingRhythmRequest(32, 10)); // Samstag, 10 Uhr
        Assert.Equal(new NextTrainingDto(new DateOnly(2026, 11, 7), new TimeOnly(10, 0), TrainingsQuelle.Rhythmus), gespeichert.Value);
        Assert.Equal(TrainingsQuelle.Rhythmus, (await a.Merkzettel.GetNextTrainingAsync(a.CoOwnerId, a.DogId)).Value!.Source);

        await a.Merkzettel.SetRhythmAsync(a.CoOwnerId, a.DogId, new SaveTrainingRhythmRequest(16, null)); // Freitag, ohne Uhrzeit
        var zeile = await a.Db.DogTrainingRhythms.SingleAsync();
        Assert.Equal((16, (int?)null), (zeile.Days, zeile.Hour));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(128, 10)]
    [InlineData(4, -1)]
    [InlineData(4, 24)]
    public async Task Rhythmus_UngueltigeWerte_Abgelehnt(int tage, int stunde)
    {
        var a = await AufbauAsync();

        var ergebnis = await a.Merkzettel.SetRhythmAsync(a.OwnerId, a.DogId, new SaveTrainingRhythmRequest(tage, stunde));

        Assert.False(ergebnis.Succeeded);
        Assert.False(ergebnis.IsNotFound);
        Assert.Empty(await a.Db.DogTrainingRhythms.ToListAsync());
    }

    // --- Training eintragen hakt ab ---------------------------------------------

    private static CreateTrainingSessionRequest TrainingMit(
        Guid hund, IReadOnlyList<Guid>? erledigt, Guid? id = null, DateOnly? tag = null) =>
        new(hund, tag ?? Heute, 30, null, [], Id: id, ErledigteMerkzettelIds: erledigt);

    private async Task<Guid> NotizIdAsync(Aufbau a, Guid hund, string text = "Eintrag") =>
        (await a.Merkzettel.CreateAsync(a.OwnerId, hund, Notiz(text))).Value!.Id;

    [Fact]
    public async Task Training_HaktDieAngegebenenEintraegeAb_DieAnderenBleibenOffen()
    {
        var a = await AufbauAsync();
        var eins = await NotizIdAsync(a, a.DogId, "Eins");
        var zwei = await NotizIdAsync(a, a.DogId, "Zwei");

        var training = (await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, [eins]))).Value!;

        var zeilen = await a.Db.TrainingNotes.ToDictionaryAsync(n => n.Id);
        Assert.Equal((Jetzt, training.Id), (zeilen[eins].DoneAt!.Value, zeilen[eins].DoneInTrainingId!.Value));
        Assert.Null(zeilen[zwei].DoneAt);
        Assert.Equal(["Zwei"], (await a.Merkzettel.GetAsync(a.OwnerId, a.DogId)).Value!.Select(n => n.Text));
    }

    [Fact]
    public async Task Training_NurEintraegeDesEigenenHundes()
    {
        var a = await AufbauAsync();
        var vonFlitzer = await NotizIdAsync(a, a.DogId, "Flitzer");
        var vonBello = await NotizIdAsync(a, a.BelloId, "Bello");

        // Ein Training für Bello darf Flitzers Eintrag nicht abhaken, auch wenn die Besitzerin beide Hunde hat.
        await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.BelloId, [vonFlitzer, vonBello, Guid.NewGuid()]));

        var zeilen = await a.Db.TrainingNotes.ToDictionaryAsync(n => n.Id);
        Assert.Null(zeilen[vonFlitzer].DoneAt);
        Assert.NotNull(zeilen[vonBello].DoneAt);
    }

    [Fact]
    public async Task Training_FremdePersonOhneZugriff_HaktNichtsAb_UndTrainingWirdAbgelehnt()
    {
        var a = await AufbauAsync();
        var eintrag = await NotizIdAsync(a, a.DogId);

        var ergebnis = await a.Trainings.CreateAsync(a.FremdeId, TrainingMit(a.DogId, [eintrag]));

        Assert.True(ergebnis.IsNotFound);
        Assert.Null((await a.Db.TrainingNotes.SingleAsync()).DoneAt);
    }

    [Fact]
    public async Task Training_BetreuendeTrainerin_HaktDenMerkzettelNichtAb()
    {
        var a = await AufbauAsync();
        var eintrag = await NotizIdAsync(a, a.DogId);

        // Sie darf für den betreuten Hund ein Training eintragen - den Merkzettel sieht sie nicht.
        var ergebnis = await a.Trainings.CreateAsync(a.TrainerId, TrainingMit(a.DogId, [eintrag]));

        Assert.True(ergebnis.Succeeded);
        Assert.Null((await a.Db.TrainingNotes.SingleAsync()).DoneAt);
    }

    [Fact]
    public async Task Training_SchonErledigteBleibenUnveraendert()
    {
        var a = await AufbauAsync();
        var eintrag = await NotizIdAsync(a, a.DogId);
        var erstes = (await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, [eintrag], tag: Heute.AddDays(-1)))).Value!;
        var abgehaktAm = (await a.Db.TrainingNotes.SingleAsync()).DoneAt;

        await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, [eintrag], id: Guid.NewGuid()));

        var zeile = await a.Db.TrainingNotes.SingleAsync();
        Assert.Equal(erstes.Id, zeile.DoneInTrainingId);
        Assert.Equal(abgehaktAm, zeile.DoneAt);
    }

    [Fact]
    public async Task Training_AmSelbenTagZusammengefasst_VerweistAufDieTageseinheit()
    {
        var a = await AufbauAsync();
        var erstes = (await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, null))).Value!;
        var eintrag = await NotizIdAsync(a, a.DogId);

        var zweites = (await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, [eintrag]))).Value!;

        Assert.Equal(erstes.Id, zweites.Id); // an die Einheit des Tages angehängt
        Assert.Equal(erstes.Id, (await a.Db.TrainingNotes.SingleAsync()).DoneInTrainingId);
    }

    [Fact]
    public async Task Training_OfflineWiederholung_IstIdempotent_UndHaktNurEinmalAb()
    {
        var a = await AufbauAsync();
        var eintrag = await NotizIdAsync(a, a.DogId);
        var id = Guid.NewGuid();

        var erste = await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, [eintrag], id));
        var zweite = await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, [eintrag], id));

        Assert.Equal(id, erste.Value!.Id);
        Assert.Equal(id, zweite.Value!.Id);
        Assert.Single(await a.Db.TrainingSessions.ToListAsync());
        Assert.Equal(id, (await a.Db.TrainingNotes.SingleAsync()).DoneInTrainingId);
    }

    [Fact]
    public async Task Training_OhneListe_VeraendertNichts()
    {
        var a = await AufbauAsync();
        await NotizIdAsync(a, a.DogId);

        await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, null));
        await a.Trainings.CreateAsync(a.OwnerId, TrainingMit(a.DogId, [], id: Guid.NewGuid()));

        Assert.Null((await a.Db.TrainingNotes.SingleAsync()).DoneAt);
    }
}
