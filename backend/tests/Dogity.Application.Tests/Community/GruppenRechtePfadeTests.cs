using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Rechtepfade rund um Termine und die Trainer-Auswahl einer Gruppe, je Vereinsrolle:
/// wer darf einen Termin löschen (nur Trainer:innen DER Gruppe - Training allein genügt
/// nicht für fremde Gruppen, Mitgliederverwaltung und Vorstand nie), und wer sieht in der
/// Trainer-Auswahl die E-Mail-Adressen der Vereinstrainer:innen.
/// </summary>
public class GruppenRechtePfadeTests
{
    private sealed record Welt(
        ApplicationDbContext Db,
        GroupTrainingScheduleService Termine,
        GroupService Gruppen,
        Club Verein,
        Group Gruppe,
        Guid Leitung,
        Guid TerminId);

    private static async Task<Welt> AufbauenAsync()
    {
        var db = InMemoryDbContext.Create();
        var nutzer = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var verein = new Club { Name = "Hundesportverein Nord" };
        db.Clubs.Add(verein);
        var leitung = Guid.NewGuid();
        nutzer.Register(leitung, "leitung@example.org", "Lena", "Leitung");
        db.ClubTrainers.Add(new ClubTrainer { ClubId = verein.Id, UserId = leitung, Role = ClubRole.Training });
        var gruppe = new Group { ClubId = verein.Id, TrainerId = leitung, Name = "Dienstagsgruppe" };
        db.Groups.Add(gruppe);
        var termin = new GroupTrainingSession
        {
            ClubId = verein.Id, GroupId = gruppe.Id, Category = GroupTrainingCategory.Puppy,
            StartsAt = DateTimeOffset.UtcNow.AddDays(3), DurationMinutes = 60,
        };
        db.GroupTrainingSessions.Add(termin);
        await db.SaveChangesAsync();
        return new Welt(
            db,
            new GroupTrainingScheduleService(db, nutzer, meldungen),
            new GroupService(db, nutzer, new TrainerRoleService(db, nutzer), meldungen, TimeProvider.System),
            verein, gruppe, leitung, termin.Id);
    }

    private static async Task<Guid> PersonAsync(Welt w, ClubRole? rolle)
    {
        var id = Guid.NewGuid();
        if (rolle is { } r) w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = w.Verein.Id, UserId = id, Role = r });
        await w.Db.SaveChangesAsync();
        return id;
    }

    // ------------------------------------------------------------ Termin löschen

    [Fact]
    public async Task Termin_LoeschenDarfDieLeitungDerGruppe()
    {
        var w = await AufbauenAsync();

        var ergebnis = await w.Termine.DeleteSessionAsync(w.Leitung, w.TerminId);

        Assert.True(ergebnis.Succeeded);
        Assert.Empty(w.Db.GroupTrainingSessions);
    }

    [Theory]
    [InlineData(ClubRole.Ausbildungswart)]
    [InlineData(ClubRole.Verwaltung)]
    public async Task Termin_LoeschenDarfWerAlleGruppenVerwaltet(ClubRole rolle)
    {
        var w = await AufbauenAsync();
        var person = await PersonAsync(w, rolle);

        Assert.True((await w.Termine.DeleteSessionAsync(person, w.TerminId)).Succeeded);
    }

    [Theory]
    [InlineData(ClubRole.Training)]
    [InlineData(ClubRole.Mitgliederverwaltung)]
    [InlineData(ClubRole.Vorstand)]
    public async Task Termin_LoeschenDarfNichtJemandOhneZugangZuDieserGruppe(ClubRole rolle)
    {
        var w = await AufbauenAsync();
        var person = await PersonAsync(w, rolle);

        var ergebnis = await w.Termine.DeleteSessionAsync(person, w.TerminId);

        Assert.True(ergebnis.IsNotFound);
        Assert.Single(w.Db.GroupTrainingSessions);
    }

    [Fact]
    public async Task Termin_LoeschenDarfNichtEineFremdePersonOderEinMitglied_UndEinUnbekannterTerminIstNotFound()
    {
        var w = await AufbauenAsync();
        var mitglied = Guid.NewGuid();
        w.Db.GroupMembers.Add(new GroupMember { GroupId = w.Gruppe.Id, UserId = mitglied, Status = GroupMemberStatus.Active });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Termine.DeleteSessionAsync(Guid.NewGuid(), w.TerminId)).IsNotFound);
        Assert.True((await w.Termine.DeleteSessionAsync(mitglied, w.TerminId)).IsNotFound);
        Assert.True((await w.Termine.DeleteSessionAsync(w.Leitung, Guid.NewGuid())).IsNotFound);
        Assert.Single(w.Db.GroupTrainingSessions);
    }

    [Fact]
    public async Task Termin_LoeschenDarfNichtDieLeitungEinerGruppeEinesAnderenVereins()
    {
        var w = await AufbauenAsync();
        var fremderVerein = new Club { Name = "Fremder Verein" };
        var fremdeLeitung = Guid.NewGuid();
        w.Db.Clubs.Add(fremderVerein);
        w.Db.ClubTrainers.Add(new ClubTrainer { ClubId = fremderVerein.Id, UserId = fremdeLeitung, Role = ClubRole.Verwaltung });
        w.Db.Groups.Add(new Group { ClubId = fremderVerein.Id, TrainerId = fremdeLeitung, Name = "Fremde Gruppe" });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Termine.DeleteSessionAsync(fremdeLeitung, w.TerminId)).IsNotFound);
    }

    // ------------------------------------------------------- Trainer-Auswahl

    [Fact]
    public async Task TrainerAuswahl_DieLeitungSiehtNamenOhneAdressen()
    {
        var w = await AufbauenAsync();

        var liste = (await w.Gruppen.GetAssignableTrainersAsync(w.Leitung, w.Gruppe.Id)).Value!;

        var eintrag = Assert.Single(liste);
        Assert.Equal("Lena", eintrag.FirstName);
        // Training allein liest die Mitgliederliste nicht - und damit auch keine Adressen.
        Assert.Null(eintrag.Email);
    }

    [Theory]
    [InlineData(ClubRole.Verwaltung, true)]
    [InlineData(ClubRole.Ausbildungswart, false)]
    [InlineData(ClubRole.Ausbildungswart | ClubRole.Mitgliederverwaltung, true)]
    public async Task TrainerAuswahl_AdressenNurFuerWerDieMitgliederlisteLiest(ClubRole rolle, bool mitAdresse)
    {
        var w = await AufbauenAsync();
        var person = await PersonAsync(w, rolle);

        var liste = (await w.Gruppen.GetAssignableTrainersAsync(person, w.Gruppe.Id)).Value!;

        Assert.NotEmpty(liste);
        Assert.All(liste, t => Assert.Equal(mitAdresse, t.Email is not null));
    }

    [Fact]
    public async Task TrainerAuswahl_ZeigtNurPersonenMitTrainerRolle()
    {
        var w = await AufbauenAsync();
        var verwaltung = await PersonAsync(w, ClubRole.Verwaltung);
        await PersonAsync(w, ClubRole.Vorstand);
        await PersonAsync(w, ClubRole.Mitgliederverwaltung);

        var liste = (await w.Gruppen.GetAssignableTrainersAsync(verwaltung, w.Gruppe.Id)).Value!;

        // Leitung (Training) und Verwaltung - nicht Vorstand und Mitgliederverwaltung.
        Assert.Equal(2, liste.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ClubRole.Training)]
    [InlineData(ClubRole.Mitgliederverwaltung)]
    [InlineData(ClubRole.Vorstand)]
    public async Task TrainerAuswahl_FremdeOhneZugangZurGruppe_NotFound(ClubRole? rolle)
    {
        var w = await AufbauenAsync();
        var person = await PersonAsync(w, rolle);

        var ergebnis = await w.Gruppen.GetAssignableTrainersAsync(person, w.Gruppe.Id);

        Assert.True(ergebnis.IsNotFound);
    }

    [Fact]
    public async Task TrainerAuswahl_EinMitgliedDerGruppeSiehtSieNicht()
    {
        var w = await AufbauenAsync();
        var mitglied = Guid.NewGuid();
        w.Db.GroupMembers.Add(new GroupMember { GroupId = w.Gruppe.Id, UserId = mitglied, Status = GroupMemberStatus.Active });
        await w.Db.SaveChangesAsync();

        Assert.True((await w.Gruppen.GetAssignableTrainersAsync(mitglied, w.Gruppe.Id)).IsNotFound);
    }

    [Fact]
    public async Task TrainerAuswahl_GruppeOhneVerein_IstLeer()
    {
        var w = await AufbauenAsync();
        var eigene = new Group { TrainerId = w.Leitung, Name = "Ohne Verein" };
        w.Db.Groups.Add(eigene);
        await w.Db.SaveChangesAsync();

        Assert.Empty((await w.Gruppen.GetAssignableTrainersAsync(w.Leitung, eigene.Id)).Value!);
    }
}
