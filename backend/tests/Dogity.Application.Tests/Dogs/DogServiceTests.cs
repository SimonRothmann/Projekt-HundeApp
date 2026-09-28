using Dogity.Application.Abstractions;
using Dogity.Application.Dogs;
using Dogity.Application.Tests.TestSupport;
using Dogity.Domain.Dogs;
using Microsoft.EntityFrameworkCore;

namespace Dogity.Application.Tests.Dogs;

/// <summary>
/// Testet die Mitbesitzer-Verwaltung von DogService (AddOwnerAsync/
/// RemoveOwnerAsync/GetOwnersAsync und die Einladungen) - insbesondere die
/// Berechtigungs- und Konsistenzregeln (nur Owner darf einladen, Mitbesitz
/// erst nach Zusage, letzter Besitzer bleibt erhalten).
/// </summary>
public class DogServiceTests
{
    private static DogService MakeService(out Dogity.Infrastructure.Persistence.ApplicationDbContext db, out FakeUserLookupService lookup)
        => MakeService(out db, out lookup, out _);

    private static DogService MakeService(
        out Dogity.Infrastructure.Persistence.ApplicationDbContext db,
        out FakeUserLookupService lookup,
        out FakeNotificationService notifications)
    {
        db = InMemoryDbContext.Create();
        lookup = new FakeUserLookupService();
        notifications = new FakeNotificationService();
        return new DogService(db, lookup, notifications);
    }

    /// <summary>Einladen und annehmen - der Weg zu einer echten Mitbesitzer:in.</summary>
    private static async Task<Guid> AddAcceptedCoOwnerAsync(DogService service, FakeUserLookupService lookup, Guid ownerId, Guid dogId)
    {
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test", "Maria", "Mitbesitz");
        Assert.True((await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"))).Succeeded);
        Assert.True((await service.RespondToInvitationAsync(targetId, dogId, accept: true)).Succeeded);
        return targetId;
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
    public async Task AddOwner_ByExistingOwner_SharesDogAfterAcceptance()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);

        var targetId = await AddAcceptedCoOwnerAsync(service, lookup, ownerId, dogId);

        var owners = await service.GetOwnersAsync(ownerId, dogId);
        Assert.Equal(2, owners.Value!.Count);
        var mitbesitz = Assert.Single(owners.Value, o => o.UserId == targetId);
        Assert.False(mitbesitz.IsInvited);
        Assert.Equal("Maria", mitbesitz.FirstName);
        Assert.True(await db.HasDogAccessAsync(targetId, dogId));
        Assert.Contains((await service.GetMyDogsAsync(targetId)).Value!, d => d.Id == dogId);
    }

    // --- Einladung statt sofortigem Mitbesitz (Prüfung 2026-09-28) --------
    // Vorher genügte eine E-Mail-Adresse: Die Person war sofort
    // Mitbesitzer:in, ohne es zu erfahren, und die Besitzerliste verriet
    // ihren Namen.

    [Fact]
    public async Task AddOwner_OnlyInvites_NoAccessAndNoNameBeforeAcceptance()
    {
        var service = MakeService(out var db, out var lookup, out var notifications);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        lookup.Register(ownerId, "anna@dogity.test", "Anna", "Anfang");
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test", "Maria", "Mitbesitz");

        var result = await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        Assert.True(result.Succeeded);
        Assert.False(await db.HasDogAccessAsync(targetId, dogId));
        Assert.Empty((await service.GetMyDogsAsync(targetId)).Value!);
        Assert.False((await service.GetByIdAsync(targetId, dogId)).Succeeded);

        // Die einladende Person sieht die Adresse, die sie selbst eingegeben
        // hat - nicht den Namen dahinter.
        var owners = (await service.GetOwnersAsync(ownerId, dogId)).Value!;
        var eingeladen = Assert.Single(owners, o => o.UserId == targetId);
        Assert.True(eingeladen.IsInvited);
        Assert.Equal("mitbesitzer@dogity.test", eingeladen.Email);
        Assert.Equal("", eingeladen.FirstName);
        Assert.Equal("", eingeladen.LastName);

        // Die eingeladene Person erfährt davon - mit Weg zur Einladung.
        var hinweis = Assert.Single(notifications.Created);
        Assert.Equal(targetId, hinweis.UserId);
        Assert.Contains("Anna Anfang", hinweis.Message);
        Assert.Equal("/dogs", hinweis.LinkPath);

        var einladungen = (await service.GetMyInvitationsAsync(targetId)).Value!;
        var einladung = Assert.Single(einladungen);
        Assert.Equal(dogId, einladung.DogId);
        Assert.Equal("Bello", einladung.DogName);
        Assert.Equal("Anna Anfang", einladung.InvitedByName);
    }

    [Fact]
    public async Task AddOwner_TwiceWhileInvited_FailsWithInvitationMessage()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        lookup.Register(Guid.NewGuid(), "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        var result = await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        Assert.False(result.Succeeded);
        Assert.Contains("eingeladen", result.Errors[0]);
    }

    [Fact]
    public async Task DeclineInvitation_LeavesNoAccess_AndCanBeInvitedAgain()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        Assert.True((await service.RespondToInvitationAsync(targetId, dogId, accept: false)).Succeeded);

        Assert.False(await db.HasDogAccessAsync(targetId, dogId));
        Assert.Empty((await service.GetMyInvitationsAsync(targetId)).Value!);
        Assert.Single((await service.GetOwnersAsync(ownerId, dogId)).Value!);

        // Abgelehnt heißt nicht "für immer" - und es bleibt EINE Zeile
        // (eindeutiger Index auf DogId+UserId, den InMemory nicht prüft).
        Assert.True((await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"))).Succeeded);
        Assert.Equal(1, await db.DogOwners.IgnoreQueryFilters().CountAsync(o => o.DogId == dogId && o.UserId == targetId));
        Assert.Single((await service.GetMyInvitationsAsync(targetId)).Value!);
    }

    [Fact]
    public async Task RespondToInvitation_WithoutInvitation_Fails()
    {
        var service = MakeService(out var db, out _);
        var (_, dogId, _) = await SetupOwnedDogAsync(db, service);
        var fremd = Guid.NewGuid();

        Assert.False((await service.RespondToInvitationAsync(fremd, dogId, accept: true)).Succeeded);
        Assert.False(await db.HasDogAccessAsync(fremd, dogId));
    }

    [Fact]
    public async Task RespondToInvitation_ForDeletedDog_Fails()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));
        await service.DeleteAsync(ownerId, dogId);

        Assert.Empty((await service.GetMyInvitationsAsync(targetId)).Value!);
        Assert.False((await service.RespondToInvitationAsync(targetId, dogId, accept: true)).Succeeded);
    }

    [Fact]
    public async Task RemoveOwner_WithdrawsInvitation_EvenWithSingleActiveOwner()
    {
        var service = MakeService(out var db, out var lookup, out var notifications);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = Guid.NewGuid();
        lookup.Register(targetId, "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));
        notifications.Created.Clear();

        var result = await service.RemoveOwnerAsync(ownerId, dogId, targetId);

        Assert.True(result.Succeeded);
        Assert.Empty((await service.GetMyInvitationsAsync(targetId)).Value!);
        // Eine zurückgezogene Einladung ist kein "du wurdest entfernt".
        Assert.Empty(notifications.Created);
    }

    [Fact]
    public async Task RemoveOwner_ActiveCoOwner_IsNotified()
    {
        var service = MakeService(out var db, out var lookup, out var notifications);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        lookup.Register(ownerId, "anna@dogity.test", "Anna", "Anfang");
        var targetId = await AddAcceptedCoOwnerAsync(service, lookup, ownerId, dogId);
        notifications.Created.Clear();

        Assert.True((await service.RemoveOwnerAsync(ownerId, dogId, targetId)).Succeeded);

        var hinweis = Assert.Single(notifications.Created);
        Assert.Equal(targetId, hinweis.UserId);
        Assert.Contains("Anna Anfang", hinweis.Message);
        Assert.Contains("Bello", hinweis.Message);
    }

    [Fact]
    public async Task RemoveOwner_Self_EndsOwnCoOwnershipWithoutNotification()
    {
        var service = MakeService(out var db, out var lookup, out var notifications);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var targetId = await AddAcceptedCoOwnerAsync(service, lookup, ownerId, dogId);
        notifications.Created.Clear();

        var result = await service.RemoveOwnerAsync(targetId, dogId, targetId);

        Assert.True(result.Succeeded);
        Assert.False(await db.HasDogAccessAsync(targetId, dogId));
        Assert.True(await db.HasDogAccessAsync(ownerId, dogId));
        Assert.Empty(notifications.Created);
    }

    [Fact]
    public async Task GetOwners_AsSupervisingTrainer_HidesInvitations()
    {
        var service = MakeService(out var db, out var lookup);
        var (ownerId, dogId, _) = await SetupOwnedDogAsync(db, service);
        var trainerId = Guid.NewGuid();
        db.TrainerAssignments.Add(new Dogity.Domain.Community.TrainerAssignment
        {
            TrainerId = trainerId,
            MemberId = ownerId,
            DogId = dogId,
            StartDate = new DateOnly(2026, 9, 1),
        });
        await db.SaveChangesAsync();
        lookup.Register(Guid.NewGuid(), "mitbesitzer@dogity.test");
        await service.AddOwnerAsync(ownerId, dogId, new AddDogOwnerRequest("mitbesitzer@dogity.test"));

        var owners = await service.GetOwnersAsync(trainerId, dogId);

        Assert.True(owners.Succeeded);
        Assert.DoesNotContain(owners.Value!, o => o.IsInvited);
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
        await AddAcceptedCoOwnerAsync(service, lookup, ownerId, dogId);

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
        var targetId = await AddAcceptedCoOwnerAsync(service, lookup, ownerId, dogId);
        Assert.True(await db.HasDogAccessAsync(targetId, dogId));

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
        var targetId = await AddAcceptedCoOwnerAsync(service, lookup, ownerId, dogId);
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
