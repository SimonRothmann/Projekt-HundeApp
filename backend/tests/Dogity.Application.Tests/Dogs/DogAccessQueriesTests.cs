using Dogity.Application.Abstractions;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Community;
using Dogity.Domain.Dogs;
using Dogity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Dogs;

/// <summary>
/// Die zentrale Zugriffsregel für Hunde (DogAccessQueries): Besitzer:in,
/// Mitbesitzer:in, betreuende Trainer:in, Fremde, offene Einladung und
/// gelöschter Hund - für die Einzelabfrage wie für die einbettbaren
/// Hunde-Id-Abfragen.
/// </summary>
public class DogAccessQueriesTests
{
    private sealed record Aufbau(
        ApplicationDbContext Db,
        Guid OwnerId,
        Guid CoOwnerId,
        Guid TrainerId,
        Guid FremdeId,
        Guid EingeladenId,
        Guid DogId,
        Guid BelloId);

    /// <summary>
    /// "Flitzer" mit Besitzerin, Mitbesitzer, betreuender Trainerin und einer
    /// offenen Einladung; "Bello" gehört der Besitzerin allein; die Trainerin
    /// besitzt außerdem "Rocky".
    /// </summary>
    private static async Task<Aufbau> AufbauAsync()
    {
        var db = InMemoryDbContext.Create();
        var ownerId = Guid.NewGuid();
        var coOwnerId = Guid.NewGuid();
        var trainerId = Guid.NewGuid();
        var eingeladenId = Guid.NewGuid();

        var flitzer = new Dog { Name = "Flitzer" };
        var bello = new Dog { Name = "Bello" };
        var rocky = new Dog { Name = "Rocky" };
        db.Dogs.AddRange(flitzer, bello, rocky);
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = coOwnerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = flitzer.Id, UserId = eingeladenId, Role = DogOwnerRole.Owner, Status = DogOwnerStatus.Invited });
        db.DogOwners.Add(new DogOwner { DogId = bello.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        db.DogOwners.Add(new DogOwner { DogId = rocky.Id, UserId = trainerId, Role = DogOwnerRole.Owner });
        db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = trainerId, MemberId = ownerId, DogId = flitzer.Id, StartDate = new DateOnly(2026, 1, 1) });
        await db.SaveChangesAsync();

        return new Aufbau(db, ownerId, coOwnerId, trainerId, Guid.NewGuid(), eingeladenId, flitzer.Id, bello.Id);
    }

    [Fact]
    public async Task Besitzerin_Mitbesitzer_UndTrainerin_HabenZugriff_FremdeUndEingeladeneNicht()
    {
        var a = await AufbauAsync();

        Assert.True(await a.Db.HasDogAccessAsync(a.OwnerId, a.DogId));
        Assert.True(await a.Db.HasDogAccessAsync(a.CoOwnerId, a.DogId));
        Assert.True(await a.Db.HasDogAccessAsync(a.TrainerId, a.DogId));
        Assert.False(await a.Db.HasDogAccessAsync(a.FremdeId, a.DogId));
        Assert.False(await a.Db.HasDogAccessAsync(a.EingeladenId, a.DogId));
        // Die Betreuung gilt für diesen einen Hund, nicht für alle der Besitzerin.
        Assert.False(await a.Db.HasDogAccessAsync(a.TrainerId, a.BelloId));
    }

    [Fact]
    public async Task IstBesitzer_GiltNurFuerBesitzUndMitbesitz_NichtFuerBetreuung()
    {
        var a = await AufbauAsync();

        Assert.True(await a.Db.IstBesitzerAsync(a.OwnerId, a.DogId));
        Assert.True(await a.Db.IstBesitzerAsync(a.CoOwnerId, a.DogId));
        Assert.False(await a.Db.IstBesitzerAsync(a.TrainerId, a.DogId));
        Assert.False(await a.Db.IstBesitzerAsync(a.FremdeId, a.DogId));
        Assert.False(await a.Db.IstBesitzerAsync(a.EingeladenId, a.DogId));
        Assert.False(await a.Db.IstBesitzerAsync(a.OwnerId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GeloeschterHund_HatNirgendsMehrZugriff()
    {
        var a = await AufbauAsync();
        (await a.Db.Dogs.SingleAsync(d => d.Id == a.DogId)).DeletedAt = DateTimeOffset.UtcNow;
        await a.Db.SaveChangesAsync();

        foreach (var person in new[] { a.OwnerId, a.CoOwnerId, a.TrainerId })
        {
            Assert.False(await a.Db.HasDogAccessAsync(person, a.DogId));
            Assert.False(await a.Db.IstBesitzerAsync(person, a.DogId));
            Assert.DoesNotContain(a.DogId, await a.Db.ZugaenglicheHundeIds(person).ToListAsync());
            Assert.DoesNotContain(a.DogId, await a.Db.EigeneHundeIds(person).ToListAsync());
        }
        // Die übrigen Hunde bleiben zugänglich.
        Assert.True(await a.Db.HasDogAccessAsync(a.OwnerId, a.BelloId));
    }

    [Fact]
    public async Task HundeIds_Besitz_UnionBetreuung_JeHundNurEinmal()
    {
        var a = await AufbauAsync();
        // Die Trainerin besitzt Rocky und betreut Flitzer; zusätzlich betreut sie Rocky
        // - ein Hund in beiden Rollen darf nur einmal vorkommen.
        var rocky = await a.Db.Dogs.SingleAsync(d => d.Name == "Rocky");
        a.Db.TrainerAssignments.Add(new TrainerAssignment { TrainerId = a.TrainerId, MemberId = a.TrainerId, DogId = rocky.Id, StartDate = new DateOnly(2026, 1, 1) });
        await a.Db.SaveChangesAsync();

        var zugaenglich = await a.Db.ZugaenglicheHundeIds(a.TrainerId).ToListAsync();
        var eigene = await a.Db.EigeneHundeIds(a.TrainerId).ToListAsync();

        Assert.Equal(new[] { a.DogId, rocky.Id }.Order(), zugaenglich.Order());
        Assert.Equal([rocky.Id], eigene);
        Assert.Equal(new[] { a.DogId, a.BelloId }.Order(), (await a.Db.ZugaenglicheHundeIds(a.OwnerId).ToListAsync()).Order());
        Assert.Empty(await a.Db.ZugaenglicheHundeIds(a.FremdeId).ToListAsync());
        Assert.Empty(await a.Db.ZugaenglicheHundeIds(a.EingeladenId).ToListAsync());
    }

    [Fact]
    public async Task BesitzerIds_ListenDieAktivenBesitzerDesHundes()
    {
        var a = await AufbauAsync();

        var besitzer = await a.Db.BesitzerIds(a.DogId).ToListAsync();

        Assert.Equal(new[] { a.OwnerId, a.CoOwnerId }.Order(), besitzer.Order());
    }

    [Fact]
    public async Task HundeIds_LassenSichInEineAbfrageEinbetten()
    {
        var a = await AufbauAsync();
        var userId = a.TrainerId;
        a.Db.TrainingSessions.Add(new Domain.Training.TrainingSession { UserId = a.OwnerId, DogId = a.DogId, Date = new DateOnly(2026, 10, 1) });
        a.Db.TrainingSessions.Add(new Domain.Training.TrainingSession { UserId = a.OwnerId, DogId = a.BelloId, Date = new DateOnly(2026, 10, 1) });
        await a.Db.SaveChangesAsync();

        var sichtbar = await a.Db.TrainingSessions
            .Where(s => a.Db.ZugaenglicheHundeIds(userId).Contains(s.DogId))
            .Select(s => s.DogId)
            .ToListAsync();

        Assert.Equal([a.DogId], sichtbar);
    }

    // InMemory übersetzt großzügig; gegen Postgres scheitert, was EF nicht in SQL
    // bringt (Client-Auswertung ist in EF Core 10 ein Fehler). ToQueryString
    // übersetzt, ohne sich zu verbinden - so fällt eine nicht übersetzbare
    // Fassung schon hier auf und nicht erst auf dem Server.
    [Fact]
    public void HundeIds_LassenSichNachPostgresUebersetzen()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=uebersetzung;Username=x;Password=x")
            .Options;
        using var db = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();
        var dogId = Guid.NewGuid();

        var zugaenglich = db.TrainingSessions.Where(s => db.ZugaenglicheHundeIds(userId).Contains(s.DogId)).ToQueryString();
        var eigene = db.TrainingSessions.Where(s => db.EigeneHundeIds(userId).Contains(s.DogId)).ToQueryString();
        var besitzer = db.TrainingSessions.Where(s => db.BesitzerIds(dogId).Contains(s.UserId)).ToQueryString();
        var kurz = db.ZugaenglicheHundeIds(userId).Where(id => id == dogId).ToQueryString();

        Assert.Contains("UNION", zugaenglich);
        Assert.Contains("dog_owners", eigene);
        Assert.Contains("dog_owners", besitzer);
        Assert.Contains("trainer_assignments", kurz);
        // Der Soft-Delete-Filter des Hundes trägt "gelöschter Hund = kein Zugriff".
        Assert.Contains("FROM dogs AS d", eigene);
        Assert.Contains("d.\"DeletedAt\" IS NULL AND EXISTS", eigene);
    }
}
