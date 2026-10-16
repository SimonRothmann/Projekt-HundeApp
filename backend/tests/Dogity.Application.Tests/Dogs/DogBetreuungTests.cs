using Dogity.Application.Abstractions;
using Dogity.Application.Community;
using Dogity.Application.Dogs;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Dogs;

/// <summary>
/// Testet, dass Besitzer:innen die Betreuung ihres Hundes sehen und selbst
/// beenden können (GetSupervisionsAsync/EndSupervisionAsync) - bis dahin
/// konnten das nur die Trainer:in und die Gruppenverwaltung.
/// </summary>
public class DogBetreuungTests
{
    private sealed record Aufbau(
        ApplicationDbContext Db,
        DogService Dogs,
        GroupService Groups,
        FakeNotificationService Notifications,
        Guid OwnerId,
        Guid CoOwnerId,
        Guid TrainerId,
        Guid GroupId,
        Guid DogId);

    /// <summary>Ein Hund mit Besitzer:in und Mitbesitzer:in, betreut von der Leitung einer Gruppe, in der die Besitzer:in Mitglied ist.</summary>
    private static async Task<Aufbau> AufbauAsync()
    {
        var db = InMemoryDbContext.Create();
        var lookup = new FakeUserLookupService();
        var notifications = new FakeNotificationService();
        var dogs = new DogService(db, lookup, notifications);
        var groups = new GroupService(db, lookup, new TrainerRoleService(db, lookup), notifications);

        var ownerId = Guid.NewGuid();
        var coOwnerId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        lookup.Register(ownerId, "anna@dogity.test", "Anna", "Anfang");
        lookup.Register(coOwnerId, "ben@dogity.test", "Ben", "Mitbesitz");
        lookup.Register(trainerId, "tina@dogity.test", "Tina", "Trainer");

        var group = new Group { TrainerId = trainerId, Name = "Dienstagsgruppe" };
        var dog = new Dog { Name = "Bello" };
        db.Groups.Add(group);
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = coOwnerId, Role = DogOwnerRole.Owner });
        db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = ownerId });
        await db.SaveChangesAsync();

        Assert.True((await groups.AssignTrainerToDogAsync(trainerId, group.Id, new AssignTrainerRequest(ownerId, dog.Id))).Succeeded);
        notifications.Created.Clear();
        return new Aufbau(db, dogs, groups, notifications, ownerId, coOwnerId, trainerId, group.Id, dog.Id);
    }

    [Fact]
    public async Task GetSupervisions_Besitzerin_SiehtTrainerinMitNameDatumUndGruppe()
    {
        var a = await AufbauAsync();

        var result = await a.Dogs.GetSupervisionsAsync(a.OwnerId, a.DogId);

        Assert.True(result.Succeeded);
        var betreuung = Assert.Single(result.Value!);
        Assert.Equal(a.TrainerId, betreuung.TrainerId);
        Assert.Equal("Tina Trainer", betreuung.TrainerName);
        Assert.Equal("Dienstagsgruppe", betreuung.GroupName);
        // Der Tag, den die Übernahme gespeichert hat - nicht neu gerechnet, sonst
        // wackelt der Test genau um 00:00 UTC.
        var gespeichert = await a.Db.TrainerAssignments.SingleAsync(t => t.DogId == a.DogId && t.TrainerId == a.TrainerId);
        Assert.Equal(gespeichert.StartDate, betreuung.StartDate);
    }

    [Fact]
    public async Task GetSupervisions_Mitbesitzerin_SiehtDieselbeListe()
    {
        var a = await AufbauAsync();

        var result = await a.Dogs.GetSupervisionsAsync(a.CoOwnerId, a.DogId);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task GetSupervisions_OhneGruppenbezug_LiefertBetreuungOhneGruppennamen()
    {
        var a = await AufbauAsync();
        // Die Besitzer:in verlässt die Gruppe; die Betreuung bleibt bestehen.
        var mitglied = await a.Db.GroupMembers.SingleAsync(m => m.GroupId == a.GroupId && m.UserId == a.OwnerId);
        mitglied.DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        var betreuung = Assert.Single((await a.Dogs.GetSupervisionsAsync(a.OwnerId, a.DogId)).Value!);

        Assert.Null(betreuung.GroupName);
    }

    [Fact]
    public async Task EingeladeneMitbesitzerin_VorDerZusage_SiehtNichtsUndBeendetNichts()
    {
        var a = await AufbauAsync();
        var eingeladen = Guid.NewGuid();
        a.Db.DogOwners.Add(new DogOwner
        {
            DogId = a.DogId, UserId = eingeladen, Role = DogOwnerRole.Owner,
            Status = DogOwnerStatus.Invited, InvitedByUserId = a.OwnerId,
        });
        await a.Db.SaveChangesAsync();

        var sehen = await a.Dogs.GetSupervisionsAsync(eingeladen, a.DogId);
        var beenden = await a.Dogs.EndSupervisionAsync(eingeladen, a.DogId, a.TrainerId);

        Assert.True(sehen.IsNotFound);
        Assert.True(beenden.IsNotFound);
        // Die Betreuung besteht weiter.
        Assert.Single((await a.Dogs.GetSupervisionsAsync(a.OwnerId, a.DogId)).Value!);
    }

    [Fact]
    public async Task GetSupervisions_FremdePersonUndTrainerin_BekommenNichts()
    {
        var a = await AufbauAsync();

        var fremd = await a.Dogs.GetSupervisionsAsync(Guid.NewGuid(), a.DogId);
        var trainerin = await a.Dogs.GetSupervisionsAsync(a.TrainerId, a.DogId);

        Assert.False(fremd.Succeeded);
        Assert.True(fremd.IsNotFound);
        // Die betreuende Trainer:in selbst gehört nicht zur Familie.
        Assert.False(trainerin.Succeeded);
        Assert.True(trainerin.IsNotFound);
    }

    [Fact]
    public void GetSupervisions_Liste_EnthaeltKeineEmailAdresse()
    {
        // Strukturelle Absicherung: Das DTO hat gar kein E-Mail-Feld.
        Assert.DoesNotContain(typeof(DogBetreuungDto).GetProperties(), p => p.Name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EndSupervision_Besitzerin_EntziehtDenZugriffUndBenachrichtigtDieTrainerin()
    {
        var a = await AufbauAsync();
        Assert.True(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));

        var result = await a.Dogs.EndSupervisionAsync(a.OwnerId, a.DogId, a.TrainerId);

        Assert.True(result.Succeeded);
        Assert.False(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));
        Assert.Empty((await a.Dogs.GetSupervisionsAsync(a.OwnerId, a.DogId)).Value!);
        var meldung = Assert.Single(a.Notifications.Created);
        Assert.Equal(a.TrainerId, meldung.UserId);
        Assert.Equal("Anna Anfang hat die Betreuung von Bello beendet.", meldung.Message);
        Assert.Equal("/trainer", meldung.LinkPath);
    }

    [Fact]
    public async Task EndSupervision_Mitbesitzerin_DarfEbenfalls()
    {
        var a = await AufbauAsync();

        var result = await a.Dogs.EndSupervisionAsync(a.CoOwnerId, a.DogId, a.TrainerId);

        Assert.True(result.Succeeded);
        Assert.False(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));
        Assert.Equal("Ben Mitbesitz hat die Betreuung von Bello beendet.", Assert.Single(a.Notifications.Created).Message);
    }

    [Fact]
    public async Task EndSupervision_FremdePerson_ErreichtNichts()
    {
        var a = await AufbauAsync();

        var result = await a.Dogs.EndSupervisionAsync(Guid.NewGuid(), a.DogId, a.TrainerId);

        Assert.False(result.Succeeded);
        Assert.True(result.IsNotFound);
        Assert.True(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));
        Assert.Empty(a.Notifications.Created);
    }

    [Fact]
    public async Task EndSupervision_TrainerinSelbstUeberDiesenWeg_GehtNicht()
    {
        // Die Trainer:in beendet über die Gruppen-Endpunkte, nicht über die
        // Besitzer:innen-Liste: Sie gehört nicht zu DogOwners.
        var a = await AufbauAsync();

        var result = await a.Dogs.EndSupervisionAsync(a.TrainerId, a.DogId, a.TrainerId);

        Assert.False(result.Succeeded);
        Assert.True(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));
    }

    [Fact]
    public async Task EndSupervision_AndererHund_BeendetNichts()
    {
        var a = await AufbauAsync();
        var fremderHund = new Dog { Name = "Rex" };
        a.Db.Dogs.Add(fremderHund);
        a.Db.DogOwners.Add(new DogOwner { DogId = fremderHund.Id, UserId = a.OwnerId, Role = DogOwnerRole.Owner });
        await a.Db.SaveChangesAsync();

        // Die Besitzer:in hat Zugriff auf Rex, aber Tina betreut Bello.
        var result = await a.Dogs.EndSupervisionAsync(a.OwnerId, fremderHund.Id, a.TrainerId);

        Assert.True(result.IsNotFound);
        Assert.True(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));
    }

    [Fact]
    public async Task EndSupervision_ZweitesMal_IstNichtGefundenUndMeldetNichtNochEinmal()
    {
        var a = await AufbauAsync();
        await a.Dogs.EndSupervisionAsync(a.OwnerId, a.DogId, a.TrainerId);
        a.Notifications.Created.Clear();

        var result = await a.Dogs.EndSupervisionAsync(a.OwnerId, a.DogId, a.TrainerId);

        Assert.False(result.Succeeded);
        Assert.True(result.IsNotFound);
        Assert.Empty(a.Notifications.Created);
    }

    [Fact]
    public async Task EndSupervision_TrainerinKannDenHundDanachWiederUebernehmen()
    {
        var a = await AufbauAsync();
        await a.Dogs.EndSupervisionAsync(a.OwnerId, a.DogId, a.TrainerId);

        var erneut = await a.Groups.AssignTrainerToDogAsync(a.TrainerId, a.GroupId, new AssignTrainerRequest(a.OwnerId, a.DogId));

        Assert.True(erneut.Succeeded);
        Assert.True(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));
        Assert.Single((await a.Dogs.GetSupervisionsAsync(a.OwnerId, a.DogId)).Value!);
    }
}
