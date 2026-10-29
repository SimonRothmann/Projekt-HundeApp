using Dogity.Application.Common;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Dogs;

/// <summary>
/// Trainingspausen eines Hundes: wer sie eintragen und wer sie sehen darf,
/// die Regeln (nur Hündinnen läufig, Ende nicht vor Beginn, keine
/// Überschneidung) und das vorzeitige Beenden. "Heute" ist der Kalendertag der
/// Vereinszeit.
/// </summary>
public class DogPauseServiceTests
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
        DogPauseService Pausen,
        DogService Hunde,
        Guid OwnerId,
        Guid CoOwnerId,
        Guid TrainerId,
        Guid FremdeId,
        Guid DogId,
        Guid BelloId);

    /// <summary>
    /// Eine Hündin "Flitzer" mit Besitzerin, Mitbesitzer und betreuender
    /// Trainerin; ein Rüde "Bello" der Besitzerin; eine fremde Person ohne Bezug.
    /// </summary>
    private static async Task<Aufbau> AufbauAsync()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var uhr = new FesteUhr(Jetzt);
        var ownerId = Guid.NewGuid();
        var coOwnerId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        lookup.Register(trainerId, "tina@dogity.test", "Tina", "Trainer");

        var flitzer = new Dog { Name = "Flitzer", Gender = DogGender.Female };
        var bello = new Dog { Name = "Bello", Gender = DogGender.Male };
        db.Dogs.AddRange(flitzer, bello);
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = coOwnerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = bello.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerId, MemberId = ownerId, DogId = flitzer.Id, StartDate = Heute });
        await db.SaveChangesAsync();

        return new Aufbau(
            db,
            new DogPauseService(db, uhr),
            new DogService(db, lookup, new FakeNotificationService(), uhr),
            ownerId, coOwnerId, trainerId, Guid.NewGuid(), flitzer.Id, bello.Id);
    }

    private static SaveDogPauseRequest Verletzt(DateOnly von, DateOnly? bis = null, string? notiz = null) =>
        new(DogPauseReason.Injured, von, bis, notiz);

    // --- Rechte ---------------------------------------------------------------

    [Fact]
    public async Task Create_BesitzerinUndMitbesitzer_DuerfenEintragen()
    {
        var a = await AufbauAsync();

        var erste = await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(5), "Pfote"));
        var zweite = await a.Pausen.CreateAsync(a.CoOwnerId, a.DogId, new SaveDogPauseRequest(DogPauseReason.Sick, Heute.AddDays(10), null, null));

        Assert.True(erste.Succeeded);
        Assert.True(zweite.Succeeded);
        var gespeichert = await a.Db.DogPauses.SingleAsync(p => p.Id == zweite.Value!.Id);
        Assert.Equal(a.CoOwnerId, gespeichert.CreatedByUserId);
    }

    [Fact]
    public async Task Create_NurEingeladeneMitbesitzerin_BekommtNichtGefunden()
    {
        var a = await AufbauAsync();
        var eingeladen = Guid.NewGuid();
        // Eingeladen, aber noch nicht angenommen: Die Einladung gilt nirgends (siehe DogOwnerStatus.Invited).
        a.Db.DogOwners.Add(new DogOwner { DogId = a.DogId, UserId = eingeladen, Role = DogOwnerRole.Owner, Status = DogOwnerStatus.Invited });
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Pausen.CreateAsync(eingeladen, a.DogId, Verletzt(Heute))).IsNotFound);
        Assert.True((await a.Pausen.GetAsync(eingeladen, a.DogId)).IsNotFound);
    }

    [Fact]
    public async Task Create_BetreuendeTrainerinUndFremde_BekommenNichtGefunden()
    {
        var a = await AufbauAsync();

        var trainerin = await a.Pausen.CreateAsync(a.TrainerId, a.DogId, Verletzt(Heute));
        var fremde = await a.Pausen.CreateAsync(a.FremdeId, a.DogId, Verletzt(Heute));

        Assert.True(trainerin.IsNotFound);
        Assert.True(fremde.IsNotFound);
        Assert.Empty(await a.Db.DogPauses.ToListAsync());
    }

    [Fact]
    public async Task Update_End_Delete_NurFuerBesitzer()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-3)))).Value!;

        foreach (var fremd in new[] { a.TrainerId, a.FremdeId })
        {
            Assert.True((await a.Pausen.UpdateAsync(fremd, a.DogId, pause.Id, Verletzt(Heute))).IsNotFound);
            Assert.True((await a.Pausen.EndAsync(fremd, a.DogId, pause.Id)).IsNotFound);
            Assert.True((await a.Pausen.DeleteAsync(fremd, a.DogId, pause.Id)).IsNotFound);
        }

        var unveraendert = await a.Db.DogPauses.SingleAsync();
        Assert.Null(unveraendert.EndDate);
        Assert.Null(unveraendert.DeletedAt);
    }

    [Fact]
    public async Task GeloeschterHund_KeinZugriff()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-3)))).Value!;
        (await a.Db.Dogs.SingleAsync(d => d.Id == a.DogId)).DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        // Weder eine leere Liste (Besitzerin) noch eine Pause für den gelöschten Hund
        // - und auch die betreuende Trainerin sieht nichts mehr.
        Assert.True((await a.Pausen.GetAsync(a.OwnerId, a.DogId)).IsNotFound);
        Assert.True((await a.Pausen.GetAsync(a.TrainerId, a.DogId)).IsNotFound);
        Assert.True((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute))).IsNotFound);
        Assert.True((await a.Pausen.UpdateAsync(a.OwnerId, a.DogId, pause.Id, Verletzt(Heute))).IsNotFound);
        Assert.True((await a.Pausen.EndAsync(a.OwnerId, a.DogId, pause.Id)).IsNotFound);
        Assert.True((await a.Pausen.DeleteAsync(a.OwnerId, a.DogId, pause.Id)).IsNotFound);
        Assert.Single(await a.Db.DogPauses.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Pause_EinesAnderenHundes_LaesstSichUeberDieseUrlNichtAendern()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute))).Value!;

        // Die Besitzerin besitzt auch Bello - aber die Pause gehört Flitzer.
        var result = await a.Pausen.DeleteAsync(a.OwnerId, a.BelloId, pause.Id);

        Assert.True(result.IsNotFound);
        Assert.Null((await a.Db.DogPauses.SingleAsync()).DeletedAt);
    }

    // --- Sichtbarkeit ----------------------------------------------------------

    [Fact]
    public async Task Get_Besitzer_SiehtGrundUndNotiz()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, null, "Kreuzband"));

        var liste = (await a.Pausen.GetAsync(a.CoOwnerId, a.DogId)).Value!;

        var pause = Assert.Single(liste);
        Assert.Equal(DogPauseReason.Injured, pause.Reason);
        Assert.Equal("Kreuzband", pause.Note);
    }

    [Fact]
    public async Task Get_BetreuendeTrainerin_SiehtNurZeitraumOhneGrundUndNotiz()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(10), "Kreuzband"));

        var liste = (await a.Pausen.GetAsync(a.TrainerId, a.DogId)).Value!;

        var pause = Assert.Single(liste);
        Assert.Equal(Heute, pause.StartDate);
        Assert.Equal(Heute.AddDays(10), pause.EndDate);
        Assert.Null(pause.Reason);
        Assert.Null(pause.Note);
    }

    [Fact]
    public async Task Get_BetreuendeTrainerin_BekommtKeineVergangenenPausen()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-30), Heute.AddDays(-20)));
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-5), Heute.AddDays(-1)));
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(7), Heute.AddDays(9)));

        var trainerin = (await a.Pausen.GetAsync(a.TrainerId, a.DogId)).Value!;
        var besitzerin = (await a.Pausen.GetAsync(a.OwnerId, a.DogId)).Value!;

        // Nur die kommende Pause; die Familie sieht weiter den ganzen Verlauf.
        Assert.Equal(Heute.AddDays(7), Assert.Single(trainerin).StartDate);
        Assert.Equal(3, besitzerin.Count);
    }

    [Fact]
    public async Task Get_Fremde_BekommtNichtGefunden()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute));

        var result = await a.Pausen.GetAsync(a.FremdeId, a.DogId);

        Assert.False(result.Succeeded);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Get_Trainerin_GeendeteBetreuung_BekommtNichtGefunden()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute));
        (await a.Db.TrainerAssignments.SingleAsync()).DeletedAt = Jetzt;
        await a.Db.SaveChangesAsync();

        Assert.True((await a.Pausen.GetAsync(a.TrainerId, a.DogId)).IsNotFound);
    }

    [Fact]
    public async Task Hundeabrufe_BesitzerMitDetails_TrainerinNurMitZeitraum()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, new SaveDogPauseRequest(DogPauseReason.InHeat, Heute.AddDays(-2), Heute.AddDays(19), "Tag 3"));

        var meine = (await a.Hunde.GetMyDogsAsync(a.CoOwnerId)).Value!.Single();
        Assert.Equal(DogPauseReason.InHeat, meine.Pause!.Reason);
        Assert.Equal("Tag 3", meine.Pause.Note);

        var besitzerSicht = (await a.Hunde.GetByIdAsync(a.OwnerId, a.DogId)).Value!;
        Assert.Equal(DogPauseReason.InHeat, besitzerSicht.Pause!.Reason);

        var trainerSicht = (await a.Hunde.GetByIdAsync(a.TrainerId, a.DogId)).Value!;
        Assert.NotNull(trainerSicht.Pause);
        Assert.Equal(Heute.AddDays(19), trainerSicht.Pause.EndDate);
        Assert.Null(trainerSicht.Pause.Reason);
        Assert.Null(trainerSicht.Pause.Note);

        var betreute = (await a.Hunde.GetSupervisedDogsAsync(a.TrainerId)).Value!.Single();
        Assert.Equal(Heute.AddDays(19), betreute.Pause!.EndDate);
        Assert.Null(betreute.Pause.Reason);
        Assert.Null(betreute.Pause.Note);
    }

    [Fact]
    public async Task Hundeabrufe_VergangeneUndZukuenftigePause_IstKeineAktuelle()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-20), Heute.AddDays(-10)));
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(10), Heute.AddDays(20)));

        Assert.Null((await a.Hunde.GetByIdAsync(a.OwnerId, a.DogId)).Value!.Pause);
        Assert.Null((await a.Hunde.GetMyDogsAsync(a.OwnerId)).Value!.Single(d => d.Id == a.DogId).Pause);
    }

    [Fact]
    public async Task MemberDogs_PauseNurBeiBetreutenHunden_OhneGrund()
    {
        var a = await AufbauAsync();
        var lookup = new FakeUserLookupService();
        var gruppen = new GroupService(a.Db, lookup, new TrainerRoleService(a.Db, lookup), new FakeNotificationService(), new FesteUhr(Jetzt));
        var gruppe = new Group { TrainerId = a.TrainerId, Name = "Dienstagsgruppe" };
        a.Db.Groups.Add(gruppe);
        a.Db.GroupMembers.Add(new GroupMember { GroupId = gruppe.Id, UserId = a.OwnerId });
        await a.Db.SaveChangesAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, null, "Kreuzband"));
        await a.Pausen.CreateAsync(a.OwnerId, a.BelloId, Verletzt(Heute, null, "Magen"));

        var hunde = (await gruppen.GetMemberDogsAsync(a.TrainerId, gruppe.Id, a.OwnerId)).Value!;

        var betreut = hunde.Single(d => d.Id == a.DogId);
        Assert.True(betreut.IsTrainerAssigned);
        Assert.NotNull(betreut.Pause);
        Assert.Null(betreut.Pause.Reason);
        Assert.Null(betreut.Pause.Note);
        // Bello betreut sie nicht: kein Einblick, also auch keine Pause.
        Assert.Null(hunde.Single(d => d.Id == a.BelloId).Pause);
    }

    // --- Regeln ----------------------------------------------------------------

    [Fact]
    public async Task Create_LaeufigkeitBeiRuede_WirdAbgelehnt()
    {
        var a = await AufbauAsync();

        var result = await a.Pausen.CreateAsync(a.OwnerId, a.BelloId, new SaveDogPauseRequest(DogPauseReason.InHeat, Heute, null, null));

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
        Assert.Empty(await a.Db.DogPauses.ToListAsync());
    }

    [Fact]
    public async Task Create_LaeufigkeitBeiHuendin_ImOrdnung()
    {
        var a = await AufbauAsync();

        var result = await a.Pausen.CreateAsync(a.OwnerId, a.DogId, new SaveDogPauseRequest(DogPauseReason.InHeat, Heute, Heute.AddDays(21), null));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Update_AufLaeufigkeitBeiRuede_WirdAbgelehnt()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.BelloId, Verletzt(Heute))).Value!;

        var result = await a.Pausen.UpdateAsync(a.OwnerId, a.BelloId, pause.Id, new SaveDogPauseRequest(DogPauseReason.InHeat, Heute, null, null));

        Assert.False(result.Succeeded);
        Assert.Equal(DogPauseReason.Injured, (await a.Db.DogPauses.SingleAsync()).Reason);
    }

    [Fact]
    public async Task Create_EndeVorBeginn_WirdAbgelehnt()
    {
        var a = await AufbauAsync();

        var result = await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(-1)));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_EintagesPause_BeginnGleichEnde_IstErlaubt()
    {
        var a = await AufbauAsync();

        Assert.True((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute))).Succeeded);
    }

    [Fact]
    public async Task Create_UnsinnigesDatumUndUnbekannterGrund_WirdAbgelehnt()
    {
        var a = await AufbauAsync();

        Assert.False((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(new DateOnly(2206, 1, 1)))).Succeeded);
        Assert.False((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(new DateOnly(1999, 1, 1)))).Succeeded);
        Assert.False((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddYears(3)))).Succeeded);
        Assert.False((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, new SaveDogPauseRequest((DogPauseReason)42, Heute, null, null))).Succeeded);
    }

    [Fact]
    public async Task Create_NotizZuLang_WirdAbgelehnt_UndRandLeerzeichenZaehlenNicht()
    {
        var a = await AufbauAsync();

        var zuLang = await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, null, new string('x', Textlaengen.PausenNotiz + 1)));
        var gerade = await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, null, "  " + new string('x', Textlaengen.PausenNotiz) + "  "));

        Assert.False(zuLang.Succeeded);
        Assert.True(gerade.Succeeded);
        Assert.Equal(Textlaengen.PausenNotiz, gerade.Value!.Note!.Length);
    }

    [Fact]
    public async Task Create_LeereNotiz_WirdNull()
    {
        var a = await AufbauAsync();

        var result = await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, null, "   "));

        Assert.Null(result.Value!.Note);
    }

    [Theory]
    [InlineData(0, 5, true)]      // innerhalb
    [InlineData(-5, 0, true)]     // schneidet den Anfang
    [InlineData(10, 12, true)]    // schneidet das Ende (einschließlich)
    [InlineData(11, 15, false)]   // beginnt am Tag nach dem Ende
    [InlineData(-5, -1, false)]   // endet am Tag vor dem Beginn
    public async Task Create_Ueberschneidung_WirdAbgelehnt(int von, int bis, bool ueberschneidet)
    {
        var a = await AufbauAsync();
        // Bestehende Pause: Tag 0 bis 10.
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(10)));

        var result = await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(von), Heute.AddDays(bis)));

        Assert.Equal(!ueberschneidet, result.Succeeded);
    }

    [Fact]
    public async Task Create_OffenePause_BlockiertAllesDanach_NichtsDavor()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute));

        Assert.False((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(100), Heute.AddDays(101)))).Succeeded);
        Assert.False((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-3), null))).Succeeded);
        Assert.True((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-10), Heute.AddDays(-1)))).Succeeded);
    }

    [Fact]
    public async Task Create_NeuePauseBeendetKeineAlteStillschweigend()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute));

        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, new SaveDogPauseRequest(DogPauseReason.Sick, Heute.AddDays(3), null, null));

        var pause = await a.Db.DogPauses.SingleAsync();
        Assert.Null(pause.EndDate);
    }

    [Fact]
    public async Task Create_UeberschneidungMitGeloeschterPause_IstErlaubt()
    {
        var a = await AufbauAsync();
        var alt = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(10)))).Value!;
        Assert.True((await a.Pausen.DeleteAsync(a.OwnerId, a.DogId, alt.Id)).Succeeded);

        Assert.True((await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(5)))).Succeeded);
    }

    [Fact]
    public async Task Update_ZeitraumVerlaengern_UeberschneidetNichtMitSichSelbst()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(5)))).Value!;

        var result = await a.Pausen.UpdateAsync(a.OwnerId, a.DogId, pause.Id, Verletzt(Heute, Heute.AddDays(8), "länger"));

        Assert.True(result.Succeeded);
        Assert.Equal(Heute.AddDays(8), result.Value!.EndDate);
        Assert.Equal("länger", result.Value.Note);
    }

    [Fact]
    public async Task Update_AufZeitraumEinerAnderenPause_WirdAbgelehnt()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-30), Heute.AddDays(-20)));
        var zweite = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute, Heute.AddDays(3)))).Value!;

        var result = await a.Pausen.UpdateAsync(a.OwnerId, a.DogId, zweite.Id, Verletzt(Heute.AddDays(-25), Heute.AddDays(3)));

        Assert.False(result.Succeeded);
    }

    // --- Beenden und Löschen ---------------------------------------------------

    [Fact]
    public async Task End_LaufendePause_LetzterTagIstGestern()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-4)))).Value!;

        var result = await a.Pausen.EndAsync(a.OwnerId, a.DogId, pause.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(Heute.AddDays(-1), (await a.Db.DogPauses.SingleAsync()).EndDate);
        // Ab heute ist der Hund wieder dabei.
        Assert.Null((await a.Hunde.GetByIdAsync(a.OwnerId, a.DogId)).Value!.Pause);
    }

    [Fact]
    public async Task End_PauseMitFesterDauerDieNochLaeuft_WirdVerkuerzt()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-4), Heute.AddDays(10)))).Value!;

        Assert.True((await a.Pausen.EndAsync(a.OwnerId, a.DogId, pause.Id)).Succeeded);

        Assert.Equal(Heute.AddDays(-1), (await a.Db.DogPauses.SingleAsync()).EndDate);
    }

    [Theory]
    [InlineData(0)]   // beginnt heute
    [InlineData(7)]   // steht noch bevor
    public async Task End_PauseDieNochNieGewirktHat_WirdZurueckgenommen(int beginnInTagen)
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(beginnInTagen)))).Value!;

        var result = await a.Pausen.EndAsync(a.OwnerId, a.DogId, pause.Id);

        Assert.True(result.Succeeded);
        Assert.Empty((await a.Pausen.GetAsync(a.OwnerId, a.DogId)).Value!);
    }

    [Fact]
    public async Task End_SchonBeendetePause_WirdAbgelehnt()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute.AddDays(-9), Heute.AddDays(-2)))).Value!;

        var result = await a.Pausen.EndAsync(a.OwnerId, a.DogId, pause.Id);

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
    }

    [Fact]
    public async Task Delete_BlendetDiePauseAus_DieZeileBleibtWeichGeloescht()
    {
        var a = await AufbauAsync();
        var pause = (await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute))).Value!;

        Assert.True((await a.Pausen.DeleteAsync(a.OwnerId, a.DogId, pause.Id)).Succeeded);

        Assert.Empty((await a.Pausen.GetAsync(a.OwnerId, a.DogId)).Value!);
        Assert.NotNull((await a.Db.DogPauses.IgnoreQueryFilters().SingleAsync()).DeletedAt);
    }

    [Fact]
    public async Task Pausen_GeloeschterHund_SindUnsichtbar()
    {
        var a = await AufbauAsync();
        await a.Pausen.CreateAsync(a.OwnerId, a.DogId, Verletzt(Heute));
        Assert.True((await a.Hunde.DeleteAsync(a.OwnerId, a.DogId)).Succeeded);

        Assert.Empty(await a.Db.DogPauses.ToListAsync());
    }
}
