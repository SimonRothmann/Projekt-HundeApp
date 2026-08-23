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

    // ---- Profilbild (SetImageAsync/GetImageAsync/DeleteImageAsync) ----

    /// <summary>Kleinstes gültiges JPEG-Fragment - Inhalt egal, der Dienst prüft nur Typ und Größe.</summary>
    private const string Jpeg = "data:image/jpeg;base64,/9j/4AAQSkZJRg==";

    [Fact]
    public async Task SetImage_ThenGet_ReturnsSameDataUrl()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        Assert.True((await service.SetImageAsync(ownerId, dogId, Jpeg)).Succeeded);

        var image = await service.GetImageAsync(ownerId, dogId);
        Assert.True(image.Succeeded);
        Assert.Equal(Jpeg, image.Value!.DataUrl);

        // Und der Hund meldet, dass ein Bild da ist - daran hängt die Anzeige.
        var dog = await service.GetByIdAsync(ownerId, dogId);
        Assert.True(dog.Value!.HasImage);
        var list = await service.GetMyDogsAsync(ownerId);
        Assert.True(list.Value!.Single().HasImage);
    }

    [Fact]
    public async Task SetImage_Twice_ReplacesInsteadOfAdding()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        await service.SetImageAsync(ownerId, dogId, Jpeg);
        const string png = "data:image/png;base64,iVBORw0KGgo=";
        await service.SetImageAsync(ownerId, dogId, png);

        Assert.Equal(png, (await service.GetImageAsync(ownerId, dogId)).Value!.DataUrl);
        Assert.Single(db.DogImages.Where(i => i.DogId == dogId));
    }

    /// <summary>
    /// Der MIME-Typ landet unverändert im Content-Type der Antwort. Wäre er
    /// frei wählbar, machte ein Upload aus dem Bildabruf eine Seite, die der
    /// Browser ausführt.
    /// </summary>
    [Fact]
    public async Task SetImage_RejectsForeignTypesAndGarbage()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        Assert.False((await service.SetImageAsync(ownerId, dogId, "data:text/html;base64,PHNjcmlwdD4=")).Succeeded);
        Assert.False((await service.SetImageAsync(ownerId, dogId, "data:image/svg+xml;base64,PHN2Zz4=")).Succeeded);
        Assert.False((await service.SetImageAsync(ownerId, dogId, "einfach nur Text")).Succeeded);
        Assert.False((await service.SetImageAsync(ownerId, dogId, "data:image/jpeg;base64,!!!keinBase64!!!")).Succeeded);
        Assert.False((await service.SetImageAsync(ownerId, dogId, "")).Succeeded);

        Assert.Empty(db.DogImages);
    }

    [Fact]
    public async Task SetImage_RejectsOversizedImage()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        var tooBig = "data:image/jpeg;base64," + Convert.ToBase64String(new byte[2 * 1024 * 1024 + 1]);

        var result = await service.SetImageAsync(ownerId, dogId, tooBig);

        Assert.False(result.Succeeded);
        Assert.Contains("zu groß", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task DeleteImage_RemovesItAndIsRepeatable()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        await service.SetImageAsync(ownerId, dogId, Jpeg);

        Assert.True((await service.DeleteImageAsync(ownerId, dogId)).Succeeded);
        Assert.False((await service.GetImageAsync(ownerId, dogId)).Succeeded);
        Assert.False((await service.GetByIdAsync(ownerId, dogId)).Value!.HasImage);

        // Nochmals löschen ist kein Fehler - das Ziel ist bereits erreicht.
        Assert.True((await service.DeleteImageAsync(ownerId, dogId)).Succeeded);
    }

    [Fact]
    public async Task Image_NotAccessibleForStrangers()
    {
        var service = MakeService(out var db, out _);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        await service.SetImageAsync(ownerId, dogId, Jpeg);
        var stranger = Guid.NewGuid();

        Assert.False((await service.GetImageAsync(stranger, dogId)).Succeeded);
        Assert.False((await service.SetImageAsync(stranger, dogId, Jpeg)).Succeeded);
        Assert.False((await service.DeleteImageAsync(stranger, dogId)).Succeeded);

        // Und das Bild des Besitzers ist noch da.
        Assert.True((await service.GetImageAsync(ownerId, dogId)).Succeeded);
    }
}
