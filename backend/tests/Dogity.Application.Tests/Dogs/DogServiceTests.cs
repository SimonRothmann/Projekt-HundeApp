using Dogity.Application.Dogs;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;

namespace Dogity.Application.Tests.Dogs;

/// <summary>
/// Testet die Mitbesitzer-Verwaltung von DogService (AddOwnerAsync/
/// RemoveOwnerAsync/GetOwnersAsync) - insbesondere die Berechtigungs- und
/// Konsistenzregeln (nur Owner darf teilen, letzter Besitzer bleibt erhalten).
/// </summary>
public class DogServiceTests
{
    private static DogService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db, out FakeUserLookupService lookup)
    {
        db = InMemoryDbContext.Create();
        lookup = new FakeUserLookupService();
        return new DogService(db, lookup);
    }

    private static async Task<(Guid OwnerId, Guid DogId, DogService Service)> SetupOwnedDogAsync(
        Dogity.Infrastructure.Persistence.ApplicationDbContext db, DogService service)
    {
        var ownerId = Guid.NewGuid();
        var dog = new Dog { Name = "Bello" };
        db.Dogs.Add(dog);
        db.DogOwners.Add(new DogOwner { DogId = dog.Id, UserId = ownerId, Role = DogOwnerRole.Owner });
        await db.SaveChangesAsync();
        return (ownerId, dog.Id, service);
    }

    [Fact]
    public async Task AddOwner_ByExistingOwner_SharesDog()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");

        var result = await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        Assert.True(result.Succeeded);
        var owners = await service.GetOwnersAsync(ownerId, dogId);
        Assert.Equal(2, owners.Value!.Count);
        Assert.Contains(owners.Value, o => o.UserId == targetId);
    }

    [Fact]
    public async Task AddOwner_ByNonOwner_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (_, dogId, _) = await SetupOwnedDogAsync(db, service);
        var nonOwnerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");

        var result = await service.AddOwnerAsync(nonOwnerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AddOwner_UnknownEmail_Fails()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        var result = await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("unbekannt@dogity.test"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AddOwner_AlreadyOwner_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        lookup.Register(ownerId, "owner@dogity.test");

        var result = await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("owner@dogity.test"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AddOwner_TargetAlreadyCoOwner_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        var result = await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RemoveOwner_LastOwner_Fails()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        var result = await service.RemoveOwnerAsync(ownerId, dogId, ownerId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RemoveOwner_WithMultipleOwners_SoftDeletesAndDogStaysVisibleForRemaining()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        var result = await service.RemoveOwnerAsync(ownerId, dogId, targetId);

        Assert.True(result.Succeeded);
        var owners = await service.GetOwnersAsync(ownerId, dogId);
        Assert.Single(owners.Value!);
        Assert.Equal(ownerId, owners.Value![0].UserId);

        // Entfernter Mitbesitzer hat keinen Zugriff mehr auf den Hund.
        var deniedAccess = await service.GetOwnersAsync(targetId, dogId);
        Assert.False(deniedAccess.Succeeded);
    }

    [Fact]
    public async Task RemoveOwner_ByNonOwner_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));
        var strangerId = Guid.NewGuid();

        var result = await service.RemoveOwnerAsync(strangerId, dogId, targetId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SetArchived_ByOwner_MarksArchivedButKeepsDogAccessible()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        var result = await service.SetArchivedAsync(ownerId, dogId, archived: true);

        Assert.True(result.Succeeded);
        // Archivierung ist KEIN Soft-Delete: der Hund bleibt abrufbar, nur mit
        // gesetztem ArchivedAt (das Frontend blendet ihn aus der aktiven Liste aus).
        var dog = await service.GetByIdAsync(ownerId, dogId);
        Assert.True(dog.Succeeded);
        Assert.NotNull(dog.Value!.ArchivedAt);
    }

    [Fact]
    public async Task SetArchived_Unarchive_ClearsArchivedAt()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        await service.SetArchivedAsync(ownerId, dogId, archived: true);

        var result = await service.SetArchivedAsync(ownerId, dogId, archived: false);

        Assert.True(result.Succeeded);
        var dog = await service.GetByIdAsync(ownerId, dogId);
        Assert.Null(dog.Value!.ArchivedAt);
    }

    [Fact]
    public async Task SetArchived_ByNonOwner_Fails()
    {
        var service = MakeService(out var db, out _);
        var (_, dogId, _) = await SetupOwnedDogAsync(db, service);
        var strangerId = Guid.NewGuid();

        var result = await service.SetArchivedAsync(strangerId, dogId, archived: true);

        Assert.False(result.Succeeded);
    }
}
