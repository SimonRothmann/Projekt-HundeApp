using Dogity.Application.Community;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Community;

/// <summary>
/// Zu- und Absagen zu Gruppenterminen: wer antworten darf, Ändern statt
/// Duplikat, Zählung nur über aktive Mitglieder, Namen nur für Trainer:innen
/// und die Nachrichten bei Absage bzw. abgesagtem Termin.
/// </summary>
public class GroupTrainingSessionResponseTests
{
    private sealed class Szene
    {
        public required GroupTrainingScheduleService Dienst { get; init; }
        public required ApplicationDbContext Db { get; init; }
        public required FakeNotificationService Meldungen { get; init; }
        public required FakeUserLookupService Lookup { get; init; }
        public Guid Trainer { get; init; }
        public Guid ClubId { get; init; }
        public Guid GruppeId { get; init; }
        public Guid TerminId { get; set; }
    }

    private static async Task<Szene> AufbauenAsync(DateTimeOffset? beginn = null)
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var meldungen = new FakeNotificationService();
        var trainer = Guid.NewGuid();
        lookup.Register(trainer, "trainer@dogity.test", "Tina", "Trainer");

        var club = new Club { Name = "Verein" };
        db.Clubs.Add(club);
        db.ClubTrainers.Add(new ClubTrainer { ClubId = club.Id, UserId = trainer });
        var gruppe = new Group { Name = "Dienstagsgruppe", TrainerId = trainer, ClubId = club.Id };
        db.Groups.Add(gruppe);
        var termin = new GroupTrainingSession
        {
            ClubId = club.Id,
            GroupId = gruppe.Id,
            // Mittags, damit das Datum in UTC und in Berlin dasselbe ist.
            StartsAt = beginn ?? DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(12),
        };
        db.GroupTrainingSessions.Add(termin);
        db.GroupTrainingSessionTrainers.Add(new GroupTrainingSessionTrainer { GroupTrainingSessionId = termin.Id, UserId = trainer });
        await db.SaveChangesAsync();

        return new Szene
        {
            Dienst = new GroupTrainingScheduleService(db, lookup, meldungen),
            Db = db, Meldungen = meldungen, Lookup = lookup,
            Trainer = trainer, ClubId = club.Id, GruppeId = gruppe.Id, TerminId = termin.Id,
        };
    }

    private static async Task<Guid> MitgliedAsync(Szene s, string vorname, GroupMemberStatus status = GroupMemberStatus.Active)
    {
        var id = Guid.NewGuid();
        s.Lookup.Register(id, $"{vorname.ToLowerInvariant()}@dogity.test", vorname, "Muster");
        s.Db.GroupMembers.Add(new GroupMember { GroupId = s.GruppeId, UserId = id, Status = status });
        await s.Db.SaveChangesAsync();
        return id;
    }

    private static async Task<GroupTrainingSessionDto> MeinTerminAsync(Szene s, Guid nutzer) =>
        (await s.Dienst.GetMemberScheduleAsync(nutzer, DateOnly.FromDateTime(DateTime.UtcNow))).Value!.Single();

    // ---- Rechte ----

    [Fact]
    public async Task Respond_AktivesMitglied_SpeichertZusage()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");

        var result = await s.Dienst.RespondAsync(anna, s.TerminId, true);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.MyResponse);
        Assert.Equal(1, result.Value.AttendingCount);
        Assert.Equal(0, result.Value.DecliningCount);
    }

    [Fact]
    public async Task Respond_NichtMitglied_BekommtDasselbeWieBeiFehlendemTermin()
    {
        var s = await AufbauenAsync();
        await MitgliedAsync(s, "Anna");

        var fremd = await s.Dienst.RespondAsync(Guid.NewGuid(), s.TerminId, true);
        var fehlend = await s.Dienst.RespondAsync(Guid.NewGuid(), Guid.NewGuid(), true);

        Assert.False(fremd.Succeeded);
        Assert.True(fremd.IsNotFound);
        Assert.Equal(fehlend.Errors, fremd.Errors);
        Assert.Empty(s.Db.GroupTrainingSessionResponses);
    }

    [Theory]
    [InlineData(GroupMemberStatus.Pending)]
    [InlineData(GroupMemberStatus.Invited)]
    public async Task Respond_NichtAktivesMitglied_WirdAbgewiesen(GroupMemberStatus status)
    {
        var s = await AufbauenAsync();
        var bewerber = await MitgliedAsync(s, "Bea", status);

        var result = await s.Dienst.RespondAsync(bewerber, s.TerminId, true);

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Respond_EhemaligesMitglied_WirdAbgewiesen()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        (await s.Db.GroupMembers.FirstAsync(m => m.UserId == anna)).DeletedAt = DateTimeOffset.UtcNow;
        await s.Db.SaveChangesAsync();

        var result = await s.Dienst.RespondAsync(anna, s.TerminId, true);

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task Respond_AbgesagterTermin_WirdAbgewiesen()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        await s.Dienst.CancelSessionAsync(s.Trainer, s.TerminId);

        var result = await s.Dienst.RespondAsync(anna, s.TerminId, true);

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
    }

    [Fact]
    public async Task Respond_BegonnenerTermin_WirdAbgewiesen()
    {
        var s = await AufbauenAsync(DateTimeOffset.UtcNow.AddMinutes(-5));
        var anna = await MitgliedAsync(s, "Anna");

        var result = await s.Dienst.RespondAsync(anna, s.TerminId, true);

        Assert.False(result.Succeeded);
        Assert.False(result.IsNotFound);
    }

    // ---- Ändern statt Duplikat ----

    [Fact]
    public async Task Respond_ZweiteAntwort_AendertDieZeileStattZuDuplizieren()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");

        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        var zweite = await s.Dienst.RespondAsync(anna, s.TerminId, false);

        Assert.False(zweite.Value!.MyResponse);
        var zeile = Assert.Single(s.Db.GroupTrainingSessionResponses);
        Assert.False(zeile.IsAttending);
        Assert.NotNull(zeile.UpdatedAt);
    }

    [Fact]
    public async Task Respond_EntfernteZeile_WirdWiederBelebtStattDupliziert()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        (await s.Db.GroupTrainingSessionResponses.FirstAsync()).DeletedAt = DateTimeOffset.UtcNow;
        await s.Db.SaveChangesAsync();

        var result = await s.Dienst.RespondAsync(anna, s.TerminId, true);

        Assert.True(result.Succeeded);
        var alle = await s.Db.GroupTrainingSessionResponses.IgnoreQueryFilters().ToListAsync();
        var zeile = Assert.Single(alle);
        Assert.Null(zeile.DeletedAt);
    }

    // ---- Zählung und Namen ----

    [Fact]
    public async Task Zaehlung_ZaehltNurAktiveMitglieder_UndRechnetOffenAus()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        var ben = await MitgliedAsync(s, "Ben");
        var carla = await MitgliedAsync(s, "Carla");
        await MitgliedAsync(s, "Dora");
        var weg = await MitgliedAsync(s, "Emil");
        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        await s.Dienst.RespondAsync(ben, s.TerminId, false);
        await s.Dienst.RespondAsync(carla, s.TerminId, true);
        await s.Dienst.RespondAsync(weg, s.TerminId, true);
        // Emil verlässt die Gruppe - seine Zusage darf nicht mehr zählen.
        (await s.Db.GroupMembers.FirstAsync(m => m.UserId == weg)).DeletedAt = DateTimeOffset.UtcNow;
        await s.Db.SaveChangesAsync();

        var dto = await MeinTerminAsync(s, anna);

        Assert.Equal(2, dto.AttendingCount);
        Assert.Equal(1, dto.DecliningCount);
        // "Offen" ist Sache der Trainer:innen: Mitglieder sähen sonst über
        // Zu-, Absagen und Offen die Größe der Gruppe.
        Assert.Equal(0, dto.OpenCount);

        var sichtTrainer = (await s.Dienst.GetClubScheduleAsync(
            s.Trainer, s.ClubId, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, false)).Value!.Single();
        Assert.Equal(1, sichtTrainer.OpenCount);
    }

    [Fact]
    public async Task Mitglieder_SehenKeineNamen_Trainerinnen_Schon()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        var ben = await MitgliedAsync(s, "Ben");
        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        await s.Dienst.RespondAsync(ben, s.TerminId, false);

        var sichtMitglied = await MeinTerminAsync(s, anna);
        var sichtTrainer = (await s.Dienst.GetClubScheduleAsync(
            s.Trainer, s.ClubId, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, false)).Value!.Single();

        Assert.Empty(sichtMitglied.Responses);
        Assert.Equal(1, sichtMitglied.AttendingCount);
        Assert.Equal(2, sichtTrainer.Responses.Count);
        Assert.Contains(sichtTrainer.Responses, r => r.FirstName == "Anna" && r.IsAttending);
        Assert.Contains(sichtTrainer.Responses, r => r.FirstName == "Ben" && !r.IsAttending);
        Assert.Null(sichtTrainer.MyResponse);
    }

    [Fact]
    public async Task Zaehlung_MehrereTermine_LiefertJedemSeineEigenenZahlen()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        var zweiter = new GroupTrainingSession
        {
            ClubId = s.ClubId, GroupId = s.GruppeId, StartsAt = DateTimeOffset.UtcNow.Date.AddDays(10).AddHours(12),
        };
        s.Db.GroupTrainingSessions.Add(zweiter);
        await s.Db.SaveChangesAsync();
        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        await s.Dienst.RespondAsync(anna, zweiter.Id, false);

        var liste = (await s.Dienst.GetMemberScheduleAsync(anna, DateOnly.FromDateTime(DateTime.UtcNow))).Value!;

        Assert.Equal(2, liste.Count);
        Assert.True(liste[0].MyResponse);
        Assert.Equal(1, liste[0].AttendingCount);
        Assert.False(liste[1].MyResponse);
        Assert.Equal(1, liste[1].DecliningCount);
    }

    // ---- Benachrichtigungen ----

    [Fact]
    public async Task Absage_BenachrichtigtDieTrainerinnenDesTermins()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");

        await s.Dienst.RespondAsync(anna, s.TerminId, false);

        var meldung = Assert.Single(s.Meldungen.Created);
        Assert.Equal(s.Trainer, meldung.UserId);
        Assert.StartsWith("Anna hat für Dienstagsgruppe am ", meldung.Message);
        Assert.EndsWith(" abgesagt.", meldung.Message);
        Assert.Equal("/trainer/schedule", meldung.LinkPath);
    }

    [Fact]
    public async Task Zusage_ErzeugtKeineBenachrichtigung()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");

        await s.Dienst.RespondAsync(anna, s.TerminId, true);

        Assert.Empty(s.Meldungen.Created);
    }

    [Fact]
    public async Task Absage_WechselVonZusage_MeldetEinmal_DieselbeAbsageNochmalNicht()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");

        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        await s.Dienst.RespondAsync(anna, s.TerminId, false);
        await s.Dienst.RespondAsync(anna, s.TerminId, false);

        Assert.Single(s.Meldungen.Created);
    }

    [Fact]
    public async Task Absage_OhneTerminTrainerinnen_GehtAnDieAktivenTrainerinnenDerGruppe()
    {
        var s = await AufbauenAsync();
        foreach (var t in await s.Db.GroupTrainingSessionTrainers.ToListAsync())
            t.DeletedAt = DateTimeOffset.UtcNow;
        var mitTrainerin = Guid.NewGuid();
        s.Db.GroupTrainers.Add(new GroupTrainer { GroupId = s.GruppeId, UserId = mitTrainerin, Status = GroupTrainerStatus.Active });
        var eingeladen = Guid.NewGuid();
        s.Db.GroupTrainers.Add(new GroupTrainer { GroupId = s.GruppeId, UserId = eingeladen, Status = GroupTrainerStatus.Invited });
        await s.Db.SaveChangesAsync();
        // Wie im Betrieb (neuer Kontext je Anfrage): der Termin kennt seine
        // entfernten Trainer:innen nicht mehr aus dem Aufbau.
        s.Db.ChangeTracker.Clear();
        var anna = await MitgliedAsync(s, "Anna");

        await s.Dienst.RespondAsync(anna, s.TerminId, false);

        var empfaenger = s.Meldungen.Created.Select(m => m.UserId).ToList();
        Assert.Contains(s.Trainer, empfaenger);       // Hauptverantwortliche
        Assert.Contains(mitTrainerin, empfaenger);
        Assert.DoesNotContain(eingeladen, empfaenger); // offene Einladung gilt nirgends
    }

    [Fact]
    public async Task TerminAbsagen_BenachrichtigtNurDieZugesagtHatten_UndNurEinmal()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        var ben = await MitgliedAsync(s, "Ben");
        var carla = await MitgliedAsync(s, "Carla");
        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        await s.Dienst.RespondAsync(ben, s.TerminId, false);
        s.Meldungen.Created.Clear(); // Ben hat den Trainer schon benachrichtigt - hier zählt nur die Terminabsage.

        await s.Dienst.CancelSessionAsync(s.Trainer, s.TerminId);
        await s.Dienst.CancelSessionAsync(s.Trainer, s.TerminId);

        var meldung = Assert.Single(s.Meldungen.Created);
        Assert.Equal(anna, meldung.UserId);
        Assert.StartsWith("Das Training Dienstagsgruppe am ", meldung.Message);
        Assert.EndsWith(" fällt aus.", meldung.Message);
        Assert.DoesNotContain(s.Meldungen.Created, m => m.UserId == carla);
    }

    [Fact]
    public async Task TerminAbsagen_ZugesagtHattenUndAusgetreten_BekommtNichts()
    {
        var s = await AufbauenAsync();
        var anna = await MitgliedAsync(s, "Anna");
        await s.Dienst.RespondAsync(anna, s.TerminId, true);
        (await s.Db.GroupMembers.FirstAsync(m => m.UserId == anna)).DeletedAt = DateTimeOffset.UtcNow;
        await s.Db.SaveChangesAsync();

        await s.Dienst.CancelSessionAsync(s.Trainer, s.TerminId);

        Assert.Empty(s.Meldungen.Created);
    }
}
